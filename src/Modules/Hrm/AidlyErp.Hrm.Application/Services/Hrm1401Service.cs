using Microsoft.EntityFrameworkCore;
using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Abstractions;
using AidlyErp.Shared.Contracts;
using AidlyErp.Sys.Contracts;
using AidlyErp.Hrm.Application.Interfaces;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Hrm.Domain;

namespace AidlyErp.Hrm.Application.Services;

// ═══════════════════════════════════════════════════════════════════════════
// Hrm1401Service — Job Requisition
// Port of Java Hrm1401Service with budget validation, vacancies minimum,
// cancel guards closed, TrimRequired, approval engine integration.
// ═══════════════════════════════════════════════════════════════════════════

public interface IHrm1401Service
{
    Task<List<HrmJobRequisition>> GetListAsync(CancellationToken ct = default);
    Task<List<HrmJobRequisition>> GetListByBranchAsync(long branchNo, CancellationToken ct = default);
    Task<HrmJobRequisition> GetDetailAsync(long requisitionNo, CancellationToken ct = default);
    Task<HrmJobRequisition> SaveAsync(HrmJobRequisition dto, CancellationToken ct = default);
    Task<HrmJobRequisition> SubmitAsync(long requisitionNo, CancellationToken ct = default);
    Task<HrmJobRequisition> ApproveAsync(long requisitionNo, CancellationToken ct = default);
    Task<HrmJobRequisition> RejectAsync(long requisitionNo, string? reason, CancellationToken ct = default);
    Task<HrmJobRequisition> HoldAsync(long requisitionNo, CancellationToken ct = default);
    Task<HrmJobRequisition> ResumeAsync(long requisitionNo, CancellationToken ct = default);
    Task<HrmJobRequisition> CloseAsync(long requisitionNo, CancellationToken ct = default);
    Task<HrmJobRequisition> CancelAsync(long requisitionNo, CancellationToken ct = default);
    Task DeleteAsync(long requisitionNo, CancellationToken ct = default);
    Task ApplyApprovalOutcomeAsync(long requisitionNo, bool approved, CancellationToken ct = default);
}

public class Hrm1401Service : IHrm1401Service
{
    private readonly IHrmDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    private readonly IApprovalService _approvalService;
    public const string DocType = "HRM_REQUISITION";
    private const short StDraft = 1, StPending = 2, StApproved = 3, StRejected = 4, StOnHold = 5, StClosed = 6, StCancelled = 7;

    public Hrm1401Service(IHrmDbContext db, ICompanyBranchContext ctx, IApprovalService approvalService)
    {
        _db = db;
        _ctx = ctx;
        _approvalService = approvalService;
    }

    public async Task<List<HrmJobRequisition>> GetListAsync(CancellationToken ct = default) =>
        await _db.HrmJobRequisitions.AsNoTracking().Where(x => x.IsDeleted == 0).OrderByDescending(x => x.RequisitionNo).ToListAsync(ct);

    public async Task<List<HrmJobRequisition>> GetListByBranchAsync(long branchNo, CancellationToken ct = default) =>
        await _db.HrmJobRequisitions.AsNoTracking().Where(x => x.BranchNo == branchNo && x.IsDeleted == 0).OrderByDescending(x => x.RequisitionNo).ToListAsync(ct);

    public async Task<HrmJobRequisition> GetDetailAsync(long requisitionNo, CancellationToken ct = default) =>
        await _db.HrmJobRequisitions.AsNoTracking().FirstOrDefaultAsync(x => x.RequisitionNo == requisitionNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Job requisition not found: requisitionNo={requisitionNo}");

    public async Task<HrmJobRequisition> SaveAsync(HrmJobRequisition dto, CancellationToken ct = default)
    {
        if (dto.RequisitionNo > 0)
        {
            var entity = await _db.HrmJobRequisitions.FirstOrDefaultAsync(x => x.RequisitionNo == dto.RequisitionNo && x.IsDeleted == 0, ct)
                ?? throw new NotFoundException($"Job requisition not found: requisitionNo={dto.RequisitionNo}");
            if (entity.Status != StDraft) throw new ValidationException("Only a Draft requisition can be edited");

            if (dto.PositionTitle != null) entity.PositionTitle = HrmValidation.TrimRequired(dto.PositionTitle, "Position title");
            entity.DepartmentNo = dto.DepartmentNo;
            entity.DesignationNo = dto.DesignationNo;
            entity.NoOfVacancies = Math.Max(dto.NoOfVacancies, 1);
            entity.EmploymentType = dto.EmploymentType;
            entity.BudgetMin = ValidateNonNegative(dto.BudgetMin, "Budget min");
            entity.BudgetMax = ValidateNonNegative(dto.BudgetMax, "Budget max");
            if (entity.BudgetMin.HasValue && entity.BudgetMax.HasValue && entity.BudgetMin > entity.BudgetMax)
                throw new ValidationException("Budget min cannot exceed budget max");
            entity.JobDescription = dto.JobDescription;
            entity.RequiredByDate = dto.RequiredByDate;
            entity.Remarks = dto.Remarks;
            entity.UpdatedBy = _ctx.CurrentUserNo(); entity.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return entity;
        }
        else
        {
            string positionTitle = HrmValidation.TrimRequired(dto.PositionTitle, "Position title");
            int vacancies = Math.Max(dto.NoOfVacancies, 1);

            long branchNo = HrmValidation.ResolveBranch(dto.BranchNo, _ctx);

            // Budget validation
            var budgetMin = ValidateNonNegative(dto.BudgetMin, "Budget min");
            var budgetMax = ValidateNonNegative(dto.BudgetMax, "Budget max");
            if (budgetMin.HasValue && budgetMax.HasValue && budgetMin > budgetMax)
                throw new ValidationException("Budget min cannot exceed budget max");

            var entity = new HrmJobRequisition
            {
                BranchNo = branchNo,
                PositionTitle = positionTitle,
                DepartmentNo = dto.DepartmentNo,
                DesignationNo = dto.DesignationNo,
                NoOfVacancies = vacancies,
                EmploymentType = dto.EmploymentType,
                BudgetMin = budgetMin,
                BudgetMax = budgetMax,
                JobDescription = dto.JobDescription,
                RequiredByDate = dto.RequiredByDate,
                Remarks = dto.Remarks,
                Status = StDraft,
                IsActive = 1,
                IsDeleted = 0,
                CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow
            };
            _db.HrmJobRequisitions.Add(entity);
            await _db.SaveChangesAsync(ct);
            return entity;
        }
    }

    public async Task<HrmJobRequisition> SubmitAsync(long requisitionNo, CancellationToken ct = default)
    {
        var entity = await _db.HrmJobRequisitions.FirstOrDefaultAsync(x => x.RequisitionNo == requisitionNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Job requisition not found: requisitionNo={requisitionNo}");
        if (entity.Status != StDraft) throw new ValidationException("Only a Draft requisition can be submitted");
        if (entity.DepartmentNo <= 0 || entity.DesignationNo <= 0)
            throw new ValidationException("Department and designation are required before submitting");

        decimal amount = entity.BudgetMax ?? 0;
        var outcome = await _approvalService.RaiseAsync(DocType, entity.RequisitionNo, $"REQ-{entity.RequisitionNo}", amount, ct);
        if (outcome.AutoApproved)
        {
            await ApplyApprovalOutcomeAsync(entity.RequisitionNo, true, ct);
        }
        else
        {
            entity.ApprovalRequestNo = outcome.ApprovalRequestNo;
            entity.Status = StPending;
            entity.UpdatedBy = _ctx.CurrentUserNo(); entity.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        return entity;
    }

    public async Task<HrmJobRequisition> ApproveAsync(long requisitionNo, CancellationToken ct = default)
    {
        var entity = await _db.HrmJobRequisitions.FirstOrDefaultAsync(x => x.RequisitionNo == requisitionNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Job requisition not found: requisitionNo={requisitionNo}");
        if (entity.Status is not (StDraft or StPending)) throw new ValidationException("Only a Draft/Pending requisition can be approved");
        if (!entity.ApprovalRequestNo.HasValue) throw new ValidationException("No approval request — submit first");
        await _approvalService.ActAsync(entity.ApprovalRequestNo.Value, true, null, ct);
        return entity;
    }

    public async Task<HrmJobRequisition> RejectAsync(long requisitionNo, string? reason, CancellationToken ct = default)
    {
        var entity = await _db.HrmJobRequisitions.FirstOrDefaultAsync(x => x.RequisitionNo == requisitionNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Job requisition not found: requisitionNo={requisitionNo}");
        if (entity.Status is not (StDraft or StPending)) throw new ValidationException("Only a Draft/Pending requisition can be rejected");
        if (!entity.ApprovalRequestNo.HasValue) throw new ValidationException("No approval request — submit first");
        entity.Remarks = reason;
        await _approvalService.ActAsync(entity.ApprovalRequestNo.Value, false, reason, ct);
        return entity;
    }

    public async Task ApplyApprovalOutcomeAsync(long requisitionNo, bool approved, CancellationToken ct = default)
    {
        var entity = await _db.HrmJobRequisitions.FirstOrDefaultAsync(x => x.RequisitionNo == requisitionNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Job requisition not found: requisitionNo={requisitionNo}");
        if (approved)
        {
            entity.Status = StApproved;
            entity.ApprovedBy = _ctx.CurrentUserNo();
            entity.ApprovedAt = DateTime.UtcNow;
        }
        else
        {
            entity.Status = StDraft; // Rejection returns to Draft for revise & resubmit
            entity.ApprovalRequestNo = null;
        }
        entity.UpdatedBy = _ctx.CurrentUserNo(); entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<HrmJobRequisition> HoldAsync(long requisitionNo, CancellationToken ct = default)
    {
        var entity = await _db.HrmJobRequisitions.FirstOrDefaultAsync(x => x.RequisitionNo == requisitionNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Job requisition not found: requisitionNo={requisitionNo}");
        if (entity.Status != StApproved) throw new ValidationException("Only an Approved requisition can be put on hold");
        entity.Status = StOnHold;
        entity.UpdatedBy = _ctx.CurrentUserNo(); entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<HrmJobRequisition> ResumeAsync(long requisitionNo, CancellationToken ct = default)
    {
        var entity = await _db.HrmJobRequisitions.FirstOrDefaultAsync(x => x.RequisitionNo == requisitionNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Job requisition not found: requisitionNo={requisitionNo}");
        if (entity.Status != StOnHold) throw new ValidationException("Only a Hold requisition can be resumed");
        entity.Status = StApproved;
        entity.UpdatedBy = _ctx.CurrentUserNo(); entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<HrmJobRequisition> CloseAsync(long requisitionNo, CancellationToken ct = default)
    {
        var entity = await _db.HrmJobRequisitions.FirstOrDefaultAsync(x => x.RequisitionNo == requisitionNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Job requisition not found: requisitionNo={requisitionNo}");
        if (entity.Status is not (StApproved or StOnHold)) throw new ValidationException("Only an Approved/Hold requisition can be closed");
        entity.Status = StClosed;
        entity.UpdatedBy = _ctx.CurrentUserNo(); entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<HrmJobRequisition> CancelAsync(long requisitionNo, CancellationToken ct = default)
    {
        var entity = await _db.HrmJobRequisitions.FirstOrDefaultAsync(x => x.RequisitionNo == requisitionNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Job requisition not found: requisitionNo={requisitionNo}");
        if (entity.Status is StClosed or StCancelled) throw new ValidationException("A closed/cancelled requisition cannot be cancelled");
        entity.Status = StCancelled;
        entity.UpdatedBy = _ctx.CurrentUserNo(); entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task DeleteAsync(long requisitionNo, CancellationToken ct = default)
    {
        var entity = await _db.HrmJobRequisitions.FirstOrDefaultAsync(x => x.RequisitionNo == requisitionNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Job requisition not found: requisitionNo={requisitionNo}");
        if (entity.Status != StDraft) throw new ValidationException("Only a Draft requisition can be deleted");
        entity.PerformSoftDelete(_ctx.CurrentUserNo());
        await _db.SaveChangesAsync(ct);
    }

    private static decimal? ValidateNonNegative(decimal? value, string label)
    {
        if (value.HasValue && value.Value < 0) throw new ValidationException($"{label} cannot be negative");
        return value;
    }
}
