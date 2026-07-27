using Microsoft.EntityFrameworkCore;
using AidlyErp.Application.Common.Exceptions;
using AidlyErp.Application.Common.Interfaces;
using AidlyErp.Application.Common.Security;
using AidlyErp.Domain.Hrm;

namespace AidlyErp.Application.Hrm.Services;

// ═══════════════════════════════════════════════════════════════════════════
// Hrm1103Service — Attendance Adjustment
// Port of Java Hrm1103Service. Status constants match Java exactly:
// ST_PENDING=1, ST_APPROVED=2, ST_REJECTED=3 (no Draft state).
// On insert, immediately raises approval — auto-approve if no workflow.
// ═══════════════════════════════════════════════════════════════════════════

public interface IHrm1103Service
{
    Task<List<HrmAttendanceAdjustment>> GetListAsync(CancellationToken ct = default);
    Task<List<HrmAttendanceAdjustment>> GetListByEmployeeAsync(long employeeNo, CancellationToken ct = default);
    Task<HrmAttendanceAdjustment> GetDetailAsync(long adjustmentNo, CancellationToken ct = default);
    Task<HrmAttendanceAdjustment> SaveAsync(HrmAttendanceAdjustment dto, CancellationToken ct = default);
    Task<HrmAttendanceAdjustment> ApproveAsync(long adjustmentNo, CancellationToken ct = default);
    Task<HrmAttendanceAdjustment> RejectAsync(long adjustmentNo, string? reason, CancellationToken ct = default);
    Task DeleteAsync(long adjustmentNo, CancellationToken ct = default);
    Task ApplyApprovalOutcomeAsync(long adjustmentNo, bool approved, CancellationToken ct = default);
}

public class Hrm1103Service : IHrm1103Service
{
    private readonly IApplicationDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    private readonly IApprovalService _approvalService;
    public const string DocType = "HRM_ATT_ADJ";
    private const short StPending = 1, StApproved = 2, StRejected = 3;
    private const short SrcAdjustment = 3;

    public Hrm1103Service(IApplicationDbContext db, ICompanyBranchContext ctx, IApprovalService approvalService)
    {
        _db = db;
        _ctx = ctx;
        _approvalService = approvalService;
    }

    public async Task<List<HrmAttendanceAdjustment>> GetListAsync(CancellationToken ct = default) =>
        await _db.HrmAttendanceAdjustments.AsNoTracking().Where(x => x.IsDeleted == 0).OrderByDescending(x => x.AdjustmentNo).ToListAsync(ct);

    public async Task<List<HrmAttendanceAdjustment>> GetListByEmployeeAsync(long employeeNo, CancellationToken ct = default) =>
        await _db.HrmAttendanceAdjustments.AsNoTracking().Where(x => x.EmployeeNo == employeeNo && x.IsDeleted == 0).OrderByDescending(x => x.AttDate).ToListAsync(ct);

    public async Task<HrmAttendanceAdjustment> GetDetailAsync(long adjustmentNo, CancellationToken ct = default) =>
        await _db.HrmAttendanceAdjustments.AsNoTracking().FirstOrDefaultAsync(x => x.AdjustmentNo == adjustmentNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Adjustment not found: adjustmentNo={adjustmentNo}");

    public async Task<HrmAttendanceAdjustment> SaveAsync(HrmAttendanceAdjustment dto, CancellationToken ct = default)
    {
        if (dto.AdjustmentNo > 0)
        {
            // Update — only Pending adjustments are editable (matches Java: e.getStatus() != ST_PENDING)
            var entity = await _db.HrmAttendanceAdjustments.FirstOrDefaultAsync(x => x.AdjustmentNo == dto.AdjustmentNo && x.IsDeleted == 0, ct)
                ?? throw new NotFoundException($"Adjustment not found: adjustmentNo={dto.AdjustmentNo}");
            if (entity.Status != StPending) throw new ValidationException("Only a Pending adjustment can be edited");

            if (dto.RequestedStatus != 0) entity.RequestedStatus = ValidateAttStatus(dto.RequestedStatus);
            if (dto.RequestedIn.HasValue) entity.RequestedIn = dto.RequestedIn;
            if (dto.RequestedOut.HasValue) entity.RequestedOut = dto.RequestedOut;
            if (dto.Reason != null) entity.Reason = dto.Reason;
            if (dto.IsActive is 0 or 1) entity.IsActive = dto.IsActive;
            entity.UpdatedBy = _ctx.CurrentUserNo(); entity.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return entity;
        }
        else
        {
            // Insert — validate, save as Pending, then raise approval immediately
            long branchNo = ResolveBranch(dto.BranchNo);
            long employeeNo = RequireEmployee(dto.EmployeeNo);
            ValidateAttDate(dto.AttDate);
            short status = ValidateAttStatus(dto.RequestedStatus);

            var entity = new HrmAttendanceAdjustment
            {
                BranchNo = branchNo,
                EmployeeNo = employeeNo,
                AttDate = dto.AttDate,
                RequestedStatus = status,
                RequestedIn = dto.RequestedIn,
                RequestedOut = dto.RequestedOut,
                Reason = dto.Reason,
                Status = StPending,
                IsActive = dto.IsActive is 0 or 1 ? dto.IsActive : (short)1,
                IsDeleted = 0,
                CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow
            };
            _db.HrmAttendanceAdjustments.Add(entity);
            await _db.SaveChangesAsync(ct);

            // Raise approval — auto-approve if no workflow configured
            var outcome = await _approvalService.RaiseAsync(DocType, entity.AdjustmentNo, $"ADJ-{entity.AdjustmentNo}", 0, ct);
            if (outcome.AutoApproved)
            {
                await ApplyApprovalOutcomeAsync(entity.AdjustmentNo, true, ct);
            }
            else
            {
                entity.ApprovalRequestNo = outcome.ApprovalRequestNo;
                entity.UpdatedBy = _ctx.CurrentUserNo(); entity.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);
            }
            return entity;
        }
    }

    public async Task<HrmAttendanceAdjustment> ApproveAsync(long adjustmentNo, CancellationToken ct = default)
    {
        var adj = await _db.HrmAttendanceAdjustments.FirstOrDefaultAsync(x => x.AdjustmentNo == adjustmentNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Adjustment not found: adjustmentNo={adjustmentNo}");
        if (adj.Status != StPending) throw new ValidationException("Only a Pending adjustment can be approved");
        if (!adj.ApprovalRequestNo.HasValue) throw new ValidationException("No approval request — submit first");
        await _approvalService.ActAsync(adj.ApprovalRequestNo.Value, true, null, ct);
        return adj;
    }

    public async Task<HrmAttendanceAdjustment> RejectAsync(long adjustmentNo, string? reason, CancellationToken ct = default)
    {
        var adj = await _db.HrmAttendanceAdjustments.FirstOrDefaultAsync(x => x.AdjustmentNo == adjustmentNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Adjustment not found: adjustmentNo={adjustmentNo}");
        if (adj.Status != StPending) throw new ValidationException("Only a Pending adjustment can be rejected");
        if (!adj.ApprovalRequestNo.HasValue) throw new ValidationException("No approval request — submit first");
        adj.Reason = reason;
        await _approvalService.ActAsync(adj.ApprovalRequestNo.Value, false, reason, ct);
        return adj;
    }

    public async Task ApplyApprovalOutcomeAsync(long adjustmentNo, bool approved, CancellationToken ct = default)
    {
        var adj = await _db.HrmAttendanceAdjustments.FirstOrDefaultAsync(x => x.AdjustmentNo == adjustmentNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Adjustment not found: adjustmentNo={adjustmentNo}");

        if (!approved)
        {
            adj.Status = StRejected;
            adj.ApprovedBy = _ctx.CurrentUserNo();
            adj.ApprovedAt = DateTime.UtcNow;
            adj.UpdatedBy = _ctx.CurrentUserNo(); adj.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return;
        }

        // Upsert attendance row — unconditional like Java (source = Adjustment)
        var attDate = adj.AttDate.ToDateTime(TimeOnly.MinValue);
        var attendance = await _db.HrmAttendances.FirstOrDefaultAsync(
            x => x.EmployeeNo == adj.EmployeeNo && x.AttDate == attDate && x.IsDeleted == 0, ct);

        if (attendance != null)
        {
            attendance.Status = adj.RequestedStatus;
            attendance.InTime = adj.RequestedIn;
            attendance.OutTime = adj.RequestedOut;
            attendance.Source = SrcAdjustment;
            attendance.UpdatedBy = _ctx.CurrentUserNo(); attendance.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            attendance = new HrmAttendance
            {
                EmployeeNo = adj.EmployeeNo,
                AttDate = attDate,
                Status = adj.RequestedStatus,
                InTime = adj.RequestedIn,
                OutTime = adj.RequestedOut,
                Source = SrcAdjustment,
                BranchNo = adj.BranchNo,
                CompanyNo = _ctx.CompanyNo,
                IsLocked = 0,
                IsDeleted = 0,
                CreatedBy = _ctx.CurrentUserNo(),
                CreatedAt = DateTime.UtcNow
            };
            _db.HrmAttendances.Add(attendance);
        }

        adj.Status = StApproved;
        adj.ApprovedBy = _ctx.CurrentUserNo();
        adj.ApprovedAt = DateTime.UtcNow;
        adj.UpdatedBy = _ctx.CurrentUserNo(); adj.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(long adjustmentNo, CancellationToken ct = default)
    {
        var entity = await _db.HrmAttendanceAdjustments.FirstOrDefaultAsync(x => x.AdjustmentNo == adjustmentNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Adjustment not found: adjustmentNo={adjustmentNo}");
        if (entity.Status != StPending) throw new ValidationException("Only a Pending adjustment can be deleted");
        entity.PerformSoftDelete(_ctx.CurrentUserNo());
        await _db.SaveChangesAsync(ct);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private long ResolveBranch(long? branchNoFromDto)
    {
        var branchNo = branchNoFromDto ?? _ctx.BranchNo;
        if (branchNo == null) throw new ValidationException("Branch is required");
        return branchNo.Value;
    }

    private static long RequireEmployee(long employeeNo)
    {
        if (employeeNo <= 0) throw new ValidationException("Employee is required");
        return employeeNo;
    }

    private static void ValidateAttDate(DateOnly date)
    {
        if (date == default) throw new ValidationException("Attendance date is required");
    }

    private static short ValidateAttStatus(short status)
    {
        if (status is < 1 or > 8) throw new ValidationException("Requested status must be 1–8");
        return status;
    }
}
