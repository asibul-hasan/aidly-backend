using AidlyErp.Sys.Application.Auth.Services;
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
using Microsoft.Extensions.Logging;

namespace AidlyErp.Sys.Application.Services;

public interface ISys1103Service
{
    Task<List<Sys1103RoleOptionDto>> GetRolesAsync(CancellationToken cancellationToken = default);

    Task<List<Sys1103PermissionRowDto>> GetMatrixAsync(long roleNo, CancellationToken cancellationToken = default);

    Task<List<Sys1103PermissionRowDto>> SaveMatrixAsync(long roleNo, List<Sys1103PermissionRowDto>? rows,
                                                        CancellationToken cancellationToken = default);
}

/// <summary>
/// SYS1103 Role Permission Matrix — grants <c>sys_role_permission</c> for company-scoped role
/// templates. The matrix lists every form the company purchased (<c>sys_enroll_menu</c>) with the
/// selected role's current can_* / perm_scope / record_filter, and saves edits back.
/// </summary>
public class Sys1103Service : ISys1103Service
{
    private const short Deleted = 0;

    private readonly ISysDbContext _db;
    private readonly IUnitOfWork<ISysDbContext> _unitOfWork;
    private readonly ICompanyBranchContext _ctx;
    private readonly IRbacAuthorizationService _rbac;
    private readonly ILogger<Sys1103Service> _logger;

    public Sys1103Service(ISysDbContext db, IUnitOfWork<ISysDbContext> unitOfWork, ICompanyBranchContext ctx,
                          IRbacAuthorizationService rbac, ILogger<Sys1103Service> logger)
    {
        _db = db;
        _unitOfWork = unitOfWork;
        _ctx = ctx;
        _rbac = rbac;
        _logger = logger;
    }

    public async Task<List<Sys1103RoleOptionDto>> GetRolesAsync(CancellationToken cancellationToken = default)
    {
        var companyNo = _ctx.CompanyNo ?? throw new ValidationException("No active company in context");

        return await _db.Roles
            .AsNoTracking()
            .Where(r => r.CompanyNo == companyNo && r.IsDeleted == Deleted)
            .OrderBy(r => r.RoleNo)
            .Select(r => new Sys1103RoleOptionDto(r.RoleNo, r.RoleId, r.RoleName, r.IsSystemRole))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Sys1103PermissionRowDto>> GetMatrixAsync(long roleNo,
                                                                    CancellationToken cancellationToken = default)
    {
        var role = await LoadRoleAsync(roleNo, cancellationToken);

        // A role is always company-scoped; -1 can match nothing if the column is somehow null.
        var rows = await FindRoleMatrixAsync(roleNo, role.CompanyNo ?? -1L, DateOnly.FromDateTime(DateTime.Today),
                                             cancellationToken);

        return rows.Select(ToRowDto).ToList();
    }

    public Task<List<Sys1103PermissionRowDto>> SaveMatrixAsync(long roleNo, List<Sys1103PermissionRowDto>? rows,
                                                               CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(async ct =>
        {
            await LoadRoleAsync(roleNo, ct);

            if (rows == null)
            {
                return await GetMatrixAsync(roleNo, ct);
            }

            var existing = await _db.RolePermissions
                .Where(rp => rp.RoleNo == roleNo && rp.IsDeleted == Deleted)
                .ToListAsync(ct);

            // A menu can appear only once per role; keep the first if the data has duplicates.
            var byMenu = new Dictionary<long, RolePermission>();
            foreach (var rp in existing) byMenu.TryAdd(rp.MenuNo, rp);

            foreach (var row in rows)
            {
                if (row.MenuNo == null) continue;

                if (!byMenu.TryGetValue(row.MenuNo.Value, out var rp))
                {
                    rp = new RolePermission
                    {
                        RoleNo = roleNo,
                        MenuNo = row.MenuNo.Value
                    };
                    _db.RolePermissions.Add(rp);
                    byMenu[row.MenuNo.Value] = rp;
                }

                rp.CanView = Flag(row.CanView);
                rp.CanInsert = Flag(row.CanInsert);
                rp.CanUpdate = Flag(row.CanUpdate);
                rp.CanDelete = Flag(row.CanDelete);
                rp.CanApprove = Flag(row.CanApprove);
                rp.CanPost = Flag(row.CanPost);
                rp.CanCancel = Flag(row.CanCancel);
                rp.CanExport = Flag(row.CanExport);
                rp.PermScope = Scope(row.PermScope);
                rp.RecordFilter = RecordFilterOf(row.RecordFilter);
                rp.DataScope = DataScopeOf(row.DataScope);
                rp.IsActive = 1;
            }

            await _db.SaveChangesAsync(ct);

            // Permissions just changed — drop the RBAC cache so the new grants take effect
            // immediately rather than after the 30-second snapshot TTL.
            _rbac.EvictPermissionCache();

            _logger.LogInformation("Saved {Count} permission row(s) for roleNo={RoleNo}", rows.Count, roleNo);

            return await GetMatrixAsync(roleNo, ct);
        }, cancellationToken);

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task<Role> LoadRoleAsync(long roleNo, CancellationToken ct) =>
        await _db.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.RoleNo == roleNo && r.IsDeleted == Deleted, ct)
        ?? throw new NotFoundException("Role not found: roleNo=" + roleNo);

    /// <summary>
    /// Full permission matrix for a role: every menu the company is enrolled in (purchased),
    /// LEFT-joined to the role's current <c>sys_role_permission</c> grants so ungranted forms still
    /// appear with zeroed flags. Deduped per menu (a menu enrolled at several branches shows once);
    /// roles are company-scoped so grants apply company-wide.
    ///
    /// <para>Ported verbatim from <c>RolePermissionRepository.findRoleMatrix</c> — the
    /// <c>GROUP BY</c> + <c>MAX</c> aggregation and <c>NULLS LAST</c> ordering are load-bearing.</para>
    /// </summary>
    private Task<List<MatrixRow>> FindRoleMatrixAsync(long roleNo, long companyNo, DateOnly today,
                                                      CancellationToken ct) =>
        _db.Database.SqlQueryRaw<MatrixRow>(
            """
            SELECT m.menu_no                          AS "MenuNo",
                   m.form_id                          AS "FormId",
                   m.form_name                        AS "FormName",
                   mo.module_code                     AS "ModuleCode",
                   mo.module_name                     AS "ModuleName",
                   sm.submodule_name                  AS "SubmoduleName",
                   mo.order_sl                        AS "ModuleSerial",
                   MAX(COALESCE(rp.can_view, 0))      AS "CanView",
                   MAX(COALESCE(rp.can_insert, 0))    AS "CanInsert",
                   MAX(COALESCE(rp.can_update, 0))    AS "CanUpdate",
                   MAX(COALESCE(rp.can_delete, 0))    AS "CanDelete",
                   MAX(COALESCE(rp.can_approve, 0))   AS "CanApprove",
                   MAX(COALESCE(rp.can_post, 0))      AS "CanPost",
                   MAX(COALESCE(rp.can_cancel, 0))    AS "CanCancel",
                   MAX(COALESCE(rp.can_export, 0))    AS "CanExport",
                   MAX(COALESCE(rp.perm_scope, 2))    AS "PermScope",
                   MAX(COALESCE(rp.record_filter, 1)) AS "RecordFilter",
                   -- MIN mirrors the RBAC resolver: lower = wider, so the widest grant wins.
                   MIN(COALESCE(rp.data_scope, 1))    AS "DataScope"
            FROM   sys_menu m
            JOIN   sys_submodule sm ON sm.submodule_no = m.submodule_no
                                AND sm.is_active = 1 AND sm.is_deleted = 0
            JOIN   sys_module    mo ON mo.module_no = sm.module_no
                                AND mo.is_active = 1 AND mo.is_deleted = 0
            JOIN   sys_enroll_menu em ON em.menu_no = m.menu_no
                                AND em.company_no = {1}
                                AND em.is_active = 1 AND em.is_deleted = 0
                                AND ( em.is_lifetime = 1
                                   OR ( COALESCE(em.enroll_start_date, DATE '1900-01-01') <= {2}
                                    AND COALESCE(em.enroll_end_date,   DATE '9999-12-31') >= {2} ) )
            LEFT JOIN sys_role_permission rp ON rp.menu_no = m.menu_no
                                AND rp.role_no = {0}
                                AND rp.is_active = 1 AND rp.is_deleted = 0
            WHERE  m.is_active = 1 AND m.is_deleted = 0
            GROUP  BY m.menu_no, m.form_id, m.form_name, mo.module_code, mo.module_name,
                      sm.submodule_name, mo.order_sl, sm.order_sl, m.order_sl
            ORDER  BY mo.order_sl NULLS LAST, mo.module_name,
                      MIN(sm.order_sl) NULLS LAST, sm.submodule_name,
                      MIN(m.order_sl)  NULLS LAST, m.form_name
            """, roleNo, companyNo, today).ToListAsync(ct);

    private static Sys1103PermissionRowDto ToRowDto(MatrixRow r) => new()
    {
        MenuNo = r.MenuNo,
        FormId = r.FormId,
        FormName = r.FormName,
        ModuleCode = r.ModuleCode,
        ModuleName = r.ModuleName,
        SubmoduleName = r.SubmoduleName,
        CanView = S(r.CanView),
        CanInsert = S(r.CanInsert),
        CanUpdate = S(r.CanUpdate),
        CanDelete = S(r.CanDelete),
        CanApprove = S(r.CanApprove),
        CanPost = S(r.CanPost),
        CanCancel = S(r.CanCancel),
        CanExport = S(r.CanExport),
        PermScope = r.PermScope != null ? (short)r.PermScope.Value : (short)2,
        RecordFilter = r.RecordFilter != null ? (short)r.RecordFilter.Value : (short)1,
        DataScope = r.DataScope != null ? (short)r.DataScope.Value : DataScopeConstants.Branch
    };

    /// <summary>Any positive value reads as granted; everything else as denied.</summary>
    private static short S(int? v) => (short)(v is > 0 ? 1 : 0);

    /// <summary>Only an explicit 1 grants; anything else (including null) denies.</summary>
    private static short Flag(short? v) => (short)(v == 1 ? 1 : 0);

    /// <summary>1 = COMPANY; anything else falls back to 2 = BRANCH.</summary>
    private static short Scope(short? v) => (short)(v == 1 ? 1 : 2);

    /// <summary>2 = OWN; anything else falls back to 1 = ALL.</summary>
    private static short RecordFilterOf(short? v) => (short)(v == 2 ? 2 : 1);

    /// <summary>2 = DEPARTMENT, 3 = EMPLOYEE; anything else falls back to 1 = BRANCH (no narrowing).</summary>
    private static short DataScopeOf(short? v) => v switch
    {
        DataScopeConstants.Department => DataScopeConstants.Department,
        DataScopeConstants.Employee => DataScopeConstants.Employee,
        _ => DataScopeConstants.Branch
    };

    /// <summary>Projection for <see cref="FindRoleMatrixAsync"/>; names match the SQL aliases.</summary>
    private sealed class MatrixRow
    {
        public long? MenuNo { get; set; }
        public string? FormId { get; set; }
        public string? FormName { get; set; }
        public string? ModuleCode { get; set; }
        public string? ModuleName { get; set; }
        public string? SubmoduleName { get; set; }
        public int? ModuleSerial { get; set; }
        public int? CanView { get; set; }
        public int? CanInsert { get; set; }
        public int? CanUpdate { get; set; }
        public int? CanDelete { get; set; }
        public int? CanApprove { get; set; }
        public int? CanPost { get; set; }
        public int? CanCancel { get; set; }
        public int? CanExport { get; set; }
        public int? PermScope { get; set; }
        public int? RecordFilter { get; set; }
        public int? DataScope { get; set; }
    }
}
