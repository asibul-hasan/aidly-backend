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

    /// <summary>Workflow list; a null filter means "all" for that column.</summary>
    Task<List<Sys1108ScopeDto>> GetListAsync(long? branchNo, long? departmentNo, long? menuNo,
                                             CancellationToken cancellationToken = default);

    /// <summary>Steps of one workflow, in approval order.</summary>
    Task<List<Sys1108StepDto>> GetStepsAsync(long scopeNo, CancellationToken cancellationToken = default);

    /// <summary>Approvers of one step.</summary>
    Task<List<Sys1108ApproverDto>> GetApproversAsync(long stepNo, CancellationToken cancellationToken = default);

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

    public Task<List<Sys1108ScopeDto>> GetListAsync(CancellationToken cancellationToken = default) =>
        GetListAsync(null, null, null, cancellationToken);

    /// <summary>
    /// Workflow list with optional branch / department / menu filters.
    ///
    /// <para>Returns the scope rows ONLY — steps and approvers load per selection via
    /// <see cref="GetStepsAsync"/> / <see cref="GetApproversAsync"/>. Nesting them here meant a
    /// query per scope plus one per step (a classic N+1) on every list load, for children the
    /// screen shows one workflow at a time anyway.</para>
    /// </summary>
    public async Task<List<Sys1108ScopeDto>> GetListAsync(long? branchNo, long? departmentNo, long? menuNo,
                                                          CancellationToken cancellationToken = default)
    {
        var companyNo = Company();

        var scopes = await _db.ApprovalScopes
            .AsNoTracking()
            .Where(s => s.CompanyNo == companyNo && s.IsDeleted == Deleted)
            .Where(s => branchNo == null || s.BranchNo == branchNo)
            .Where(s => departmentNo == null || s.DepartmentNo == departmentNo)
            .Where(s => menuNo == null || s.MenuNo == menuNo)
            .OrderBy(s => s.ScopeNo)
            .ToListAsync(cancellationToken);

        if (scopes.Count == 0) return new List<Sys1108ScopeDto>();

        // One grouped count for the whole page rather than a count per row — the list needs to
        // know which workflows still have children (their delete is hidden in the UI).
        var scopeNos = scopes.Select(s => s.ScopeNo).ToList();
        var stepCounts = await _db.ApprovalSteps
            .AsNoTracking()
            .Where(s => scopeNos.Contains(s.ScopeNo))
            .GroupBy(s => s.ScopeNo)
            .Select(g => new { ScopeNo = g.Key, Count = (long)g.Count() })
            .ToDictionaryAsync(x => x.ScopeNo, x => x.Count, cancellationToken);

        return scopes.Select(s => new Sys1108ScopeDto
        {
            ScopeNo = s.ScopeNo,
            WorkflowName = s.WorkflowName,
            BranchNo = s.BranchNo,
            DepartmentNo = s.DepartmentNo,
            MenuNo = s.MenuNo,
            IsActive = s.IsActive,
            RowVersion = s.RowVersion,
            StepCount = stepCounts.TryGetValue(s.ScopeNo, out var c) ? c : 0,
        }).ToList();
    }

    public async Task<List<Sys1108StepDto>> GetStepsAsync(long scopeNo, CancellationToken cancellationToken = default)
    {
        await RequireOwnScopeAsync(scopeNo, cancellationToken);

        var steps = await _db.ApprovalSteps
            .AsNoTracking()
            .Where(s => s.ScopeNo == scopeNo)
            .OrderBy(s => s.StepNumber)
            .ToListAsync(cancellationToken);

        if (steps.Count == 0) return new List<Sys1108StepDto>();

        var stepNos = steps.Select(s => s.StepNo).ToList();
        var approverCounts = await _db.StepApprovers
            .AsNoTracking()
            .Where(a => stepNos.Contains(a.StepNo))
            .GroupBy(a => a.StepNo)
            .Select(g => new { StepNo = g.Key, Count = (long)g.Count() })
            .ToDictionaryAsync(x => x.StepNo, x => x.Count, cancellationToken);

        return steps.Select(s => new Sys1108StepDto
        {
            StepNo = s.StepNo,
            StepNumber = s.StepNumber,
            StepType = s.StepType,
            NextStepNo = s.NextStepNo,
            StepName = s.StepName,
            IsFinal = s.IsFinal,
            IsActive = s.IsActive,
            ApproverCount = approverCounts.TryGetValue(s.StepNo, out var c) ? c : 0,
        }).ToList();
    }

    public async Task<List<Sys1108ApproverDto>> GetApproversAsync(long stepNo, CancellationToken cancellationToken = default)
    {
        var step = await _db.ApprovalSteps.AsNoTracking()
            .FirstOrDefaultAsync(s => s.StepNo == stepNo, cancellationToken)
            ?? throw new NotFoundException("Approval step not found");

        await RequireOwnScopeAsync(step.ScopeNo, cancellationToken);

        return await _db.StepApprovers
            .AsNoTracking()
            .Where(a => a.StepNo == stepNo)
            .OrderBy(a => a.ApproverNo)
            .Select(a => new Sys1108ApproverDto
            {
                ApproverNo = a.ApproverNo,
                EmpNo = a.EmpNo,
                IsActive = a.IsActive,
            })
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// A scope_no / step_no arriving from the client must be proven to belong to the caller's
    /// company — without this the child endpoints would read another company's workflow by id.
    /// </summary>
    private async Task RequireOwnScopeAsync(long scopeNo, CancellationToken cancellationToken)
    {
        var companyNo = Company();
        var exists = await _db.ApprovalScopes.AsNoTracking()
            .AnyAsync(s => s.ScopeNo == scopeNo && s.CompanyNo == companyNo && s.IsDeleted == Deleted,
                      cancellationToken);

        if (!exists) throw new NotFoundException("Approval scope not found");
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
