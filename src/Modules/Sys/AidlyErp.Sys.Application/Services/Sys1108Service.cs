using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Abstractions;
using AidlyErp.Shared.Contracts;
using AidlyErp.Sys.Contracts;
using AidlyErp.Sys.Application.Interfaces;
using AidlyErp.Hrm.Contracts;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Sys.Application.Dto;
using AidlyErp.Sys.Domain;
using Microsoft.EntityFrameworkCore;

namespace AidlyErp.Sys.Application.Services;

public interface ISys1108Service
{
    Task<List<Sys1108ScopeDto>> GetListAsync(CancellationToken cancellationToken = default);

    Task<List<SysLookupDto>> GetEnrolledMenuOptionsAsync(CancellationToken cancellationToken = default);

    Task<Sys1108ScopeDto> SaveAsync(Sys1108ScopeDto dto, CancellationToken cancellationToken = default);

    Task DeleteAsync(long scopeNo, CancellationToken cancellationToken = default);
}

/// <summary>
/// SYS1108 Approval Workflow Setup (<c>sys_approval_scope</c>) — company-scoped escalation
/// rules: Scope → Steps → Approvers (employees).
/// </summary>
public class Sys1108Service : ISys1108Service
{
    private const short Deleted = 0;

    private readonly ISysDbContext _db;
    private readonly IUnitOfWork<ISysDbContext> _unitOfWork;
    private readonly ICompanyBranchContext _ctx;

    public Sys1108Service(ISysDbContext db, IUnitOfWork<ISysDbContext> unitOfWork, ICompanyBranchContext ctx)
    {
        _db = db;
        _unitOfWork = unitOfWork;
        _ctx = ctx;
    }

    public async Task<List<Sys1108ScopeDto>> GetListAsync(CancellationToken cancellationToken = default)
    {
        var companyNo = Company();

        var scopes = await _db.ApprovalScopes
            .AsNoTracking()
            .Where(s => s.CompanyNo == companyNo && s.IsDeleted == Deleted)
            .OrderBy(s => s.ScopeNo)
            .ToListAsync(cancellationToken);

        var result = new List<Sys1108ScopeDto>(scopes.Count);
        foreach (var scope in scopes)
        {
            result.Add(await ToDtoAsync(scope, cancellationToken));
        }

        return result;
    }

    /// <summary>
    /// Menus the company is actually enrolled in. The catalog query LEFT-joins the enrolment, so
    /// unenrolled menus come back with a null <c>enroll_menu_no</c> and are filtered out here —
    /// matching the Java behaviour exactly.
    /// </summary>
    public async Task<List<SysLookupDto>> GetEnrolledMenuOptionsAsync(CancellationToken cancellationToken = default)
    {
        var rows = await FindMenuCatalogForCompanyAsync(Company(), cancellationToken);

        return rows
            .Where(r => r.EnrollMenuNo != null)
            .Select(r => new SysLookupDto(r.MenuNo ?? 0, $"{r.FormName} ({r.FormId})"))
            .ToList();
    }

    public Task<Sys1108ScopeDto> SaveAsync(Sys1108ScopeDto dto, CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(async ct =>
        {
            ApprovalScope scope;

            if (dto.ScopeNo != null)
            {
                scope = await _db.ApprovalScopes.FirstOrDefaultAsync(s => s.ScopeNo == dto.ScopeNo, ct)
                        ?? throw new NotFoundException("Approval scope not found");

                if (scope.IsDeleted != Deleted)
                {
                    throw new NotFoundException("Approval scope deleted");
                }
            }
            else
            {
                scope = new ApprovalScope { CompanyNo = Company() };
                _db.ApprovalScopes.Add(scope);
            }

            scope.WorkflowName = dto.WorkflowName;
            scope.BranchNo = dto.BranchNo;
            scope.DepartmentNo = dto.DepartmentNo;
            scope.MenuNo = dto.MenuNo ?? 0;
            scope.IsActive = dto.IsActive ?? 1;

            await _db.SaveChangesAsync(ct);

            // Recreate steps and approvers (delete and insert, for simplicity). Only on update —
            // a fresh scope has nothing to clear.
            if (dto.ScopeNo != null)
            {
                var oldSteps = await _db.ApprovalSteps
                    .Where(s => s.ScopeNo == scope.ScopeNo)
                    .ToListAsync(ct);

                var oldStepNos = oldSteps.Select(s => s.StepNo).ToList();

                if (oldStepNos.Count > 0)
                {
                    var oldApprovers = await _db.StepApprovers
                        .Where(a => oldStepNos.Contains(a.StepNo))
                        .ToListAsync(ct);

                    _db.StepApprovers.RemoveRange(oldApprovers);
                }

                _db.ApprovalSteps.RemoveRange(oldSteps);
                await _db.SaveChangesAsync(ct);
            }

            foreach (var stepDto in dto.Steps)
            {
                var step = new ApprovalStep
                {
                    ScopeNo = scope.ScopeNo,
                    StepNumber = stepDto.StepNumber ?? 0,
                    StepType = stepDto.StepType ?? 1,
                    NextStepNo = stepDto.NextStepNo,
                    StepName = stepDto.StepName,
                    IsFinal = stepDto.IsFinal ?? 0,
                    IsActive = stepDto.IsActive ?? 1
                };

                _db.ApprovalSteps.Add(step);
                await _db.SaveChangesAsync(ct);   // need the generated step_no for the approvers

                foreach (var approverDto in stepDto.Approvers)
                {
                    _db.StepApprovers.Add(new StepApprover
                    {
                        StepNo = step.StepNo,
                        EmpNo = approverDto.EmpNo,
                        IsActive = approverDto.IsActive ?? 1
                    });
                }

                await _db.SaveChangesAsync(ct);
            }

            return await ToDtoAsync(scope, ct);
        }, cancellationToken);

    public Task DeleteAsync(long scopeNo, CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(async ct =>
        {
            var scope = await _db.ApprovalScopes.FirstOrDefaultAsync(s => s.ScopeNo == scopeNo, ct)
                        ?? throw new NotFoundException("Approval scope not found");

            scope.PerformSoftDelete(_ctx.CurrentUserNo());
            await _db.SaveChangesAsync(ct);
        }, cancellationToken);

    // ── helpers ───────────────────────────────────────────────────────────────

    private long Company() =>
        _ctx.CompanyNo ?? throw new ValidationException("No active company in context");

    private async Task<Sys1108ScopeDto> ToDtoAsync(ApprovalScope e, CancellationToken ct)
    {
        var dto = new Sys1108ScopeDto
        {
            ScopeNo = e.ScopeNo,
            WorkflowName = e.WorkflowName,
            BranchNo = e.BranchNo,
            DepartmentNo = e.DepartmentNo,
            MenuNo = e.MenuNo,
            IsActive = e.IsActive,
            RowVersion = e.RowVersion
        };

        var steps = await _db.ApprovalSteps
            .AsNoTracking()
            .Where(s => s.ScopeNo == e.ScopeNo)
            .OrderBy(s => s.StepNumber)
            .ToListAsync(ct);

        var stepNos = steps.Select(s => s.StepNo).ToList();

        // One query for every approver across all steps, then group — not a query per step.
        var approversByStep = stepNos.Count == 0
            ? new Dictionary<long, List<StepApprover>>()
            : (await _db.StepApprovers
                    .AsNoTracking()
                    .Where(a => stepNos.Contains(a.StepNo))
                    .ToListAsync(ct))
                .GroupBy(a => a.StepNo)
                .ToDictionary(g => g.Key, g => g.ToList());

        dto.Steps = steps.Select(step => new Sys1108StepDto
        {
            StepNo = step.StepNo,
            StepNumber = step.StepNumber,
            StepType = step.StepType,
            NextStepNo = step.NextStepNo,
            StepName = step.StepName,
            IsFinal = step.IsFinal,
            IsActive = step.IsActive,
            Approvers = (approversByStep.TryGetValue(step.StepNo, out var list) ? list : new List<StepApprover>())
                .Select(a => new Sys1108ApproverDto
                {
                    ApproverNo = a.ApproverNo,
                    EmpNo = a.EmpNo,
                    IsActive = a.IsActive
                }).ToList()
        }).ToList();

        return dto;
    }

    /// <summary>
    /// Every menu with the company's enrolment LEFT-joined on. Ported verbatim from
    /// <c>EnrollMenuRepository.findMenuCatalogForCompany</c> — the <c>NULLS LAST</c> ordering and
    /// the branch-agnostic (<c>em.branch_no IS NULL</c>) join condition are both load-bearing.
    /// </summary>
    private Task<List<MenuEnrollRow>> FindMenuCatalogForCompanyAsync(long companyNo, CancellationToken ct) =>
        _db.Database.SqlQueryRaw<MenuEnrollRow>(
            """
            SELECT m.menu_no            AS "MenuNo",
                   m.form_id            AS "FormId",
                   m.form_name          AS "FormName",
                   mo.module_name       AS "ModuleName",
                   mo.module_code       AS "ModuleCode",
                   sm.submodule_name    AS "SubmoduleName",
                   mo.order_sl          AS "ModuleSerial",
                   em.enroll_menu_no    AS "EnrollMenuNo",
                   em.is_lifetime       AS "IsLifetime",
                   em.enroll_start_date AS "EnrollStartDate",
                   em.enroll_end_date   AS "EnrollEndDate"
            FROM   sys_menu m
            JOIN   sys_submodule sm ON sm.submodule_no = m.submodule_no
                                AND sm.is_active = 1 AND sm.is_deleted = 0
            JOIN   sys_module    mo ON mo.module_no = sm.module_no
                                AND mo.is_active = 1 AND mo.is_deleted = 0
            LEFT JOIN sys_enroll_menu em ON em.menu_no = m.menu_no
                                AND em.company_no = {0}
                                AND em.branch_no IS NULL
                                AND em.is_deleted = 0
            WHERE  m.is_active = 1 AND m.is_deleted = 0
            ORDER  BY mo.order_sl NULLS LAST, mo.module_name,
                      sm.order_sl NULLS LAST, sm.submodule_name,
                      m.order_sl  NULLS LAST, m.form_name
            """, companyNo).ToListAsync(ct);

    /// <summary>Projection for <see cref="FindMenuCatalogForCompanyAsync"/>; names match the SQL aliases.</summary>
    private sealed class MenuEnrollRow
    {
        public long? MenuNo { get; set; }
        public string? FormId { get; set; }
        public string? FormName { get; set; }
        public string? ModuleName { get; set; }
        public string? ModuleCode { get; set; }
        public string? SubmoduleName { get; set; }
        public int? ModuleSerial { get; set; }
        public long? EnrollMenuNo { get; set; }
        public short? IsLifetime { get; set; }
        public DateOnly? EnrollStartDate { get; set; }
        public DateOnly? EnrollEndDate { get; set; }
    }
}
