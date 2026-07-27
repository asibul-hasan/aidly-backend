using Microsoft.EntityFrameworkCore;
using AidlyErp.Application.Common.Exceptions;
using AidlyErp.Application.Common.Interfaces;
using AidlyErp.Application.Common.Security;
using AidlyErp.Domain.Hrm;

namespace AidlyErp.Application.Hrm.Services;

// ═══════════════════════════════════════════════════════════════════════════
// Hrm1106Service — Promotion / Transfer (Employee Movement)
// Full port of Java Hrm1106Service (259 lines). Draft → Submitted → Approved
// → Cancelled. On approval, writes new placement onto employee and syncs
// salary structure. Approval-gated via HRM_MOVEMENT.
// ═══════════════════════════════════════════════════════════════════════════

public interface IHrm1106Service
{
    Task<List<HrmEmployeeMovement>> GetListAsync(CancellationToken ct = default);
    Task<List<HrmEmployeeMovement>> GetListByEmployeeAsync(long employeeNo, CancellationToken ct = default);
    Task<HrmEmployeeMovement> GetDetailAsync(long movementNo, CancellationToken ct = default);
    Task<HrmEmployeeMovement> SaveAsync(HrmEmployeeMovement dto, CancellationToken ct = default);
    Task<HrmEmployeeMovement> SubmitAsync(long movementNo, CancellationToken ct = default);
    Task<HrmEmployeeMovement> ApproveAsync(long movementNo, CancellationToken ct = default);
    Task<HrmEmployeeMovement> RejectAsync(long movementNo, string? reason, CancellationToken ct = default);
    Task<HrmEmployeeMovement> CancelAsync(long movementNo, CancellationToken ct = default);
    Task DeleteAsync(long movementNo, CancellationToken ct = default);
    Task ApplyApprovalOutcomeAsync(long movementNo, bool approved, CancellationToken ct = default);
}

public class Hrm1106Service : IHrm1106Service
{
    private readonly IApplicationDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    private readonly IApprovalService _approvalService;
    private readonly IHrm1201Service _salaryStructureService;
    public const string DocType = "HRM_MOVEMENT";
    private const short StDraft = 1, StSubmitted = 2, StApproved = 3, StCancelled = 4;

    public Hrm1106Service(IApplicationDbContext db, ICompanyBranchContext ctx,
        IApprovalService approvalService, IHrm1201Service salaryStructureService)
    {
        _db = db; _ctx = ctx;
        _approvalService = approvalService;
        _salaryStructureService = salaryStructureService;
    }

    // ── Reads ──────────────────────────────────────────────────────────────

    public async Task<List<HrmEmployeeMovement>> GetListAsync(CancellationToken ct = default) =>
        await _db.HrmEmployeeMovements.AsNoTracking()
            .Where(x => x.IsDeleted == 0 && x.BranchNo == Branch())
            .OrderByDescending(x => x.MovementNo).ToListAsync(ct);

    public async Task<List<HrmEmployeeMovement>> GetListByEmployeeAsync(long employeeNo, CancellationToken ct = default) =>
        await _db.HrmEmployeeMovements.AsNoTracking()
            .Where(x => x.EmployeeNo == employeeNo && x.IsDeleted == 0)
            .OrderByDescending(x => x.MovementNo).ToListAsync(ct);

    public async Task<HrmEmployeeMovement> GetDetailAsync(long movementNo, CancellationToken ct = default) =>
        await LoadLiveAsync(movementNo, ct);

    // ── Save ───────────────────────────────────────────────────────────────

    public async Task<HrmEmployeeMovement> SaveAsync(HrmEmployeeMovement dto, CancellationToken ct = default)
    {
        if (dto.MovementNo > 0)
            return await UpdateAsync(dto.MovementNo, dto, ct);
        return await InsertAsync(dto, ct);
    }

    private async Task<HrmEmployeeMovement> InsertAsync(HrmEmployeeMovement dto, CancellationToken ct)
    {
        long branchNo = ResolveBranch(dto.BranchNo);
        string code = TrimRequired(dto.MovementId, "Movement ID").ToUpperInvariant();
        var emp = await RequireEmployeeAsync(dto.EmployeeNo, ct);
        if (dto.MovementType == 0) throw new ValidationException("Movement type is required");
        if (dto.EffectiveDate == default) throw new ValidationException("Effective date is required");

        if (await _db.HrmEmployeeMovements.AnyAsync(x => x.MovementId == code && x.BranchNo == branchNo && x.IsDeleted == 0, ct))
            throw new ValidationException($"Movement ID already exists in this branch: {code}");

        var entity = new HrmEmployeeMovement
        {
            BranchNo = branchNo,
            MovementId = code,
            EmployeeNo = emp.EmployeeNo,
            MovementType = dto.MovementType,
            EffectiveDate = dto.EffectiveDate,
            // Snapshot current placement
            FromDesignationNo = emp.DesignationNo,
            FromDepartmentNo = emp.DepartmentNo,
            FromBranchNo = emp.BranchNo,
            FromGradeNo = dto.FromGradeNo,
            Status = StDraft,
            IsActive = NormalizeFlag(dto.IsActive, 1, "is_active"),
            IsDeleted = 0,
            CreatedBy = _ctx.CurrentUserNo(),
            CreatedAt = DateTime.UtcNow
        };
        ApplyTargets(entity, dto);

        _db.HrmEmployeeMovements.Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity;
    }

    private async Task<HrmEmployeeMovement> UpdateAsync(long no, HrmEmployeeMovement dto, CancellationToken ct)
    {
        var entity = await LoadLiveAsync(no, ct);
        if (entity.Status != StDraft) throw new ValidationException("Only a Draft movement can be edited");

        if (dto.MovementType != 0) entity.MovementType = dto.MovementType;
        if (dto.EffectiveDate != default) entity.EffectiveDate = dto.EffectiveDate;
        ApplyTargets(entity, dto);
        if (dto.Reason != null) entity.Reason = dto.Reason;
        if (dto.IsActive != 0) entity.IsActive = NormalizeFlag(dto.IsActive, 1, "is_active");

        entity.UpdatedBy = _ctx.CurrentUserNo(); entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return entity;
    }

    private static void ApplyTargets(HrmEmployeeMovement e, HrmEmployeeMovement dto)
    {
        if (dto.ToDesignationNo != null) e.ToDesignationNo = dto.ToDesignationNo;
        if (dto.ToGradeNo != null) e.ToGradeNo = dto.ToGradeNo;
        if (dto.ToDepartmentNo != null) e.ToDepartmentNo = dto.ToDepartmentNo;
        if (dto.ToBranchNo != null) e.ToBranchNo = dto.ToBranchNo;
        if (dto.NewSalary != null) e.NewSalary = dto.NewSalary;
        if (dto.Reason != null) e.Reason = dto.Reason;
    }

    // ── Workflow ───────────────────────────────────────────────────────────

    public async Task<HrmEmployeeMovement> SubmitAsync(long movementNo, CancellationToken ct = default)
    {
        var entity = await LoadLiveAsync(movementNo, ct);
        if (entity.Status != StDraft) throw new ValidationException("Only a Draft movement can be submitted");

        decimal amount = entity.NewSalary ?? 0m;
        var outcome = await _approvalService.RaiseAsync(DocType, entity.MovementNo, $"MOV-{entity.MovementNo}", amount, ct);
        if (outcome.AutoApproved)
        {
            await ApplyApprovalOutcomeAsync(entity.MovementNo, true, ct);
        }
        else
        {
            entity.Status = StSubmitted;
            entity.ApprovalRequestNo = outcome.ApprovalRequestNo;
            entity.UpdatedBy = _ctx.CurrentUserNo(); entity.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        return await LoadLiveAsync(movementNo, ct);
    }

    public async Task<HrmEmployeeMovement> ApproveAsync(long movementNo, CancellationToken ct = default)
    {
        var entity = await LoadLiveAsync(movementNo, ct);
        if (entity.Status != StSubmitted) throw new ValidationException("Only a Submitted movement can be approved");
        if (entity.ApprovalRequestNo == null) throw new ValidationException("No approval request — submit first");
        await _approvalService.ActAsync(entity.ApprovalRequestNo.Value, true, null, ct);
        return await LoadLiveAsync(movementNo, ct);
    }

    public async Task<HrmEmployeeMovement> RejectAsync(long movementNo, string? reason, CancellationToken ct = default)
    {
        var entity = await LoadLiveAsync(movementNo, ct);
        if (entity.Status != StSubmitted) throw new ValidationException("Only a Submitted movement can be rejected");
        if (entity.ApprovalRequestNo == null) throw new ValidationException("No approval request — submit first");
        await _approvalService.ActAsync(entity.ApprovalRequestNo.Value, false, reason, ct);
        return await LoadLiveAsync(movementNo, ct);
    }

    public async Task ApplyApprovalOutcomeAsync(long movementNo, bool approved, CancellationToken ct = default)
    {
        var e = await LoadLiveAsync(movementNo, ct);
        if (!approved)
        {
            e.Status = StDraft;
            e.ApprovalRequestNo = null;
            e.UpdatedBy = _ctx.CurrentUserNo(); e.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return;
        }

        var emp = await RequireEmployeeAsync(e.EmployeeNo, ct);
        if (e.ToDesignationNo != null) emp.DesignationNo = e.ToDesignationNo.Value;
        if (e.ToGradeNo != null) emp.GradeNo = e.ToGradeNo;
        if (e.ToDepartmentNo != null) emp.DepartmentNo = e.ToDepartmentNo.Value;
        if (e.ToBranchNo != null) emp.BranchNo = e.ToBranchNo;
        if (e.NewSalary != null) emp.Salary = e.NewSalary.Value;
        emp.UpdatedBy = _ctx.CurrentUserNo(); emp.UpdatedAt = DateTime.UtcNow;

        await _salaryStructureService.SyncRevisionFromMovementAsync(e, emp, ct);

        e.Status = StApproved;
        e.ApprovedBy = _ctx.CurrentUserNo();
        e.ApprovedAt = DateTime.UtcNow;
        e.UpdatedBy = _ctx.CurrentUserNo(); e.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<HrmEmployeeMovement> CancelAsync(long movementNo, CancellationToken ct = default)
    {
        var entity = await LoadLiveAsync(movementNo, ct);
        if (entity.Status is StApproved or StCancelled)
            throw new ValidationException("An approved/cancelled movement cannot be cancelled");
        entity.Status = StCancelled;
        entity.UpdatedBy = _ctx.CurrentUserNo(); entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task DeleteAsync(long movementNo, CancellationToken ct = default)
    {
        var entity = await LoadLiveAsync(movementNo, ct);
        if (entity.Status != StDraft) throw new ValidationException("Only a Draft movement can be deleted");
        entity.PerformSoftDelete(_ctx.CurrentUserNo());
        await _db.SaveChangesAsync(ct);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private async Task<HrmEmployeeMovement> LoadLiveAsync(long no, CancellationToken ct) =>
        await _db.HrmEmployeeMovements.FirstOrDefaultAsync(x => x.MovementNo == no && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Movement not found: no={no}");

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

    private async Task<HrmEmployee> RequireEmployeeAsync(long employeeNo, CancellationToken ct)
    {
        if (employeeNo <= 0) throw new ValidationException("Employee is required");
        return await _db.HrmEmployees.FirstOrDefaultAsync(x => x.EmployeeNo == employeeNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Employee not found: employeeNo={employeeNo}");
    }

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
