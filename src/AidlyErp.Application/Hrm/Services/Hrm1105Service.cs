using Microsoft.EntityFrameworkCore;
using AidlyErp.Application.Common.Exceptions;
using AidlyErp.Application.Common.Interfaces;
using AidlyErp.Application.Common.Security;
using AidlyErp.Domain.Hrm;

namespace AidlyErp.Application.Hrm.Services;

// ═══════════════════════════════════════════════════════════════════════════
// Hrm1105Service — Overtime
// Full port of Java Hrm1105Service (235 lines). OT amount auto-computation
// (hours × hourly_rate × rate_multiplier), approval-gated via HRM_OT.
// Draft → Approved (or Cancelled).
// ═══════════════════════════════════════════════════════════════════════════

public interface IHrm1105Service
{
    Task<List<HrmOvertime>> GetListAsync(CancellationToken ct = default);
    Task<HrmOvertime> GetDetailAsync(long overtimeNo, CancellationToken ct = default);
    Task<HrmOvertime> SaveAsync(HrmOvertime dto, CancellationToken ct = default);
    Task<HrmOvertime> SubmitAsync(long overtimeNo, CancellationToken ct = default);
    Task<HrmOvertime> ApproveAsync(long overtimeNo, CancellationToken ct = default);
    Task<HrmOvertime> RejectAsync(long overtimeNo, string? reason, CancellationToken ct = default);
    Task<HrmOvertime> CancelAsync(long overtimeNo, CancellationToken ct = default);
    Task DeleteAsync(long overtimeNo, CancellationToken ct = default);
    Task ApplyApprovalOutcomeAsync(long overtimeNo, bool approved, CancellationToken ct = default);
}

public class Hrm1105Service : IHrm1105Service
{
    private readonly IApplicationDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    private readonly IApprovalService _approvalService;
    public const string DocType = "HRM_OT";
    private const short StDraft = 1, StApproved = 2, StCancelled = 3;

    public Hrm1105Service(IApplicationDbContext db, ICompanyBranchContext ctx, IApprovalService approvalService)
    {
        _db = db; _ctx = ctx; _approvalService = approvalService;
    }

    // ── Reads ──────────────────────────────────────────────────────────────

    public async Task<List<HrmOvertime>> GetListAsync(CancellationToken ct = default) =>
        await _db.HrmOvertimes.AsNoTracking()
            .Where(x => x.IsDeleted == 0 && x.BranchNo == Branch())
            .OrderByDescending(x => x.OvertimeNo).ToListAsync(ct);

    public async Task<HrmOvertime> GetDetailAsync(long overtimeNo, CancellationToken ct = default) =>
        await LoadLiveAsync(overtimeNo, ct);

    // ── Save ───────────────────────────────────────────────────────────────

    public async Task<HrmOvertime> SaveAsync(HrmOvertime dto, CancellationToken ct = default)
    {
        if (dto.OvertimeNo > 0)
            return await UpdateAsync(dto.OvertimeNo, dto, ct);
        return await InsertAsync(dto, ct);
    }

    private async Task<HrmOvertime> InsertAsync(HrmOvertime dto, CancellationToken ct)
    {
        long branchNo = ResolveBranch(dto.BranchNo);
        string code = TrimRequired(dto.OvertimeId, "Overtime ID").ToUpperInvariant();
        if (dto.EmployeeNo <= 0) throw new ValidationException("Employee is required");
        if (dto.OtDate == default) throw new ValidationException("Overtime date is required");

        if (await _db.HrmOvertimes.AnyAsync(x => x.OvertimeId == code && x.BranchNo == branchNo && x.IsDeleted == 0, ct))
            throw new ValidationException($"Overtime ID already exists in this branch: {code}");

        decimal hours = Positive(dto.Hours, "Hours");
        decimal rateMultiplier = Positive(Defaulted(dto.RateMultiplier, 1.5m), "Rate multiplier");
        decimal hourlyRate = NonNeg(Defaulted(dto.HourlyRate, 0m), "Hourly rate");

        var entity = new HrmOvertime
        {
            BranchNo = branchNo,
            OvertimeId = code,
            EmployeeNo = dto.EmployeeNo,
            OtDate = dto.OtDate,
            Hours = hours,
            RateMultiplier = rateMultiplier,
            HourlyRate = hourlyRate,
            OtAmount = hours * hourlyRate * rateMultiplier,
            Reason = dto.Reason,
            Status = StDraft,
            IsActive = NormalizeFlag(dto.IsActive, 1, "is_active"),
            IsDeleted = 0,
            CreatedBy = _ctx.CurrentUserNo(),
            CreatedAt = DateTime.UtcNow
        };
        _db.HrmOvertimes.Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity;
    }

    private async Task<HrmOvertime> UpdateAsync(long no, HrmOvertime dto, CancellationToken ct)
    {
        var entity = await LoadLiveAsync(no, ct);
        if (entity.Status != StDraft) throw new ValidationException("Only a Draft overtime claim can be edited");

        if (dto.OtDate != default) entity.OtDate = dto.OtDate;
        if (dto.Hours != 0) entity.Hours = Positive(dto.Hours, "Hours");
        if (dto.RateMultiplier != 0) entity.RateMultiplier = Positive(dto.RateMultiplier, "Rate multiplier");
        if (dto.HourlyRate != 0) entity.HourlyRate = NonNeg(dto.HourlyRate, "Hourly rate");
        if (dto.Reason != null) entity.Reason = dto.Reason;
        if (dto.IsActive != 0) entity.IsActive = NormalizeFlag(dto.IsActive, 1, "is_active");
        entity.OtAmount = entity.Hours * entity.HourlyRate * entity.RateMultiplier;

        entity.UpdatedBy = _ctx.CurrentUserNo(); entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return entity;
    }

    // ── Workflow ───────────────────────────────────────────────────────────

    public async Task<HrmOvertime> SubmitAsync(long overtimeNo, CancellationToken ct = default)
    {
        var entity = await LoadLiveAsync(overtimeNo, ct);
        if (entity.Status != StDraft) throw new ValidationException("Only a Draft overtime claim can be submitted");

        var outcome = await _approvalService.RaiseAsync(DocType, entity.OvertimeNo, $"OT-{entity.OvertimeNo}", entity.OtAmount, ct);
        if (outcome.AutoApproved)
        {
            await ApplyApprovalOutcomeAsync(entity.OvertimeNo, true, ct);
        }
        else
        {
            entity.ApprovalRequestNo = outcome.ApprovalRequestNo;
            entity.UpdatedBy = _ctx.CurrentUserNo(); entity.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        return await LoadLiveAsync(overtimeNo, ct);
    }

    public async Task<HrmOvertime> ApproveAsync(long overtimeNo, CancellationToken ct = default)
    {
        var entity = await LoadLiveAsync(overtimeNo, ct);
        if (entity.Status != StDraft) throw new ValidationException("Only a submitted (Draft) overtime claim can be approved");
        if (entity.ApprovalRequestNo == null) throw new ValidationException("No approval request — submit first");
        await _approvalService.ActAsync(entity.ApprovalRequestNo.Value, true, null, ct);
        return await LoadLiveAsync(overtimeNo, ct);
    }

    public async Task<HrmOvertime> RejectAsync(long overtimeNo, string? reason, CancellationToken ct = default)
    {
        var entity = await LoadLiveAsync(overtimeNo, ct);
        if (entity.ApprovalRequestNo == null) throw new ValidationException("No approval request — submit first");
        await _approvalService.ActAsync(entity.ApprovalRequestNo.Value, false, reason, ct);
        return await LoadLiveAsync(overtimeNo, ct);
    }

    public async Task ApplyApprovalOutcomeAsync(long overtimeNo, bool approved, CancellationToken ct = default)
    {
        var entity = await LoadLiveAsync(overtimeNo, ct);
        if (approved)
        {
            entity.Status = StApproved;
            entity.ApprovedBy = _ctx.CurrentUserNo();
            entity.ApprovedAt = DateTime.UtcNow;
        }
        else
        {
            entity.ApprovalRequestNo = null;
        }
        entity.UpdatedBy = _ctx.CurrentUserNo(); entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<HrmOvertime> CancelAsync(long overtimeNo, CancellationToken ct = default)
    {
        var entity = await LoadLiveAsync(overtimeNo, ct);
        if (entity.Status == StCancelled) throw new ValidationException("Overtime claim is already cancelled");
        entity.Status = StCancelled;
        entity.UpdatedBy = _ctx.CurrentUserNo(); entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task DeleteAsync(long overtimeNo, CancellationToken ct = default)
    {
        var entity = await LoadLiveAsync(overtimeNo, ct);
        if (entity.Status != StDraft) throw new ValidationException("Only a Draft overtime claim can be deleted");
        entity.PerformSoftDelete(_ctx.CurrentUserNo());
        await _db.SaveChangesAsync(ct);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private async Task<HrmOvertime> LoadLiveAsync(long no, CancellationToken ct) =>
        await _db.HrmOvertimes.FirstOrDefaultAsync(x => x.OvertimeNo == no && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Overtime claim not found: no={no}");

    private long Branch()
    {
        var b = _ctx.BranchNo;
        if (b == null) throw new ValidationException("No active branch in context");
        return b.Value;
    }

    private long ResolveBranch(long? branchNoFromDto)
    {
        var branchNo = branchNoFromDto ?? _ctx.BranchNo;
        if (branchNo == null) throw new ValidationException("Branch is required");
        return branchNo.Value;
    }

    private static decimal Positive(decimal v, string field)
    {
        if (v <= 0) throw new ValidationException($"{field} must be greater than zero");
        return v;
    }

    private static decimal NonNeg(decimal v, string field)
    {
        if (v < 0) throw new ValidationException($"{field} cannot be negative");
        return v;
    }

    private static decimal Defaulted(decimal? v, decimal dflt) => v ?? dflt;

    private static short NormalizeFlag(short value, short defaultValue, string fieldName)
    {
        if (value == defaultValue) return value;
        if (value is 0 or 1) return value;
        throw new ValidationException($"{fieldName} must be 0 or 1");
    }

    private static string TrimRequired(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ValidationException($"{fieldName} is required");
        return value.Trim();
    }
}
