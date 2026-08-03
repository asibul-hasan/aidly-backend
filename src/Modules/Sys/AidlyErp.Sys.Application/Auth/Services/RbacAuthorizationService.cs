using System.Collections.Concurrent;
using AidlyErp.Sys.Application.Auth.Dto;
using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Abstractions;
using AidlyErp.Shared.Contracts;
using AidlyErp.Sys.Contracts;
using AidlyErp.Sys.Application.Interfaces;
using AidlyErp.Hrm.Contracts;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Shared.Core;
using Microsoft.EntityFrameworkCore;

namespace AidlyErp.Sys.Application.Auth.Services;

/// <summary>Combined access decision for the request authorization filter.</summary>
public readonly record struct FormAccess(bool Allowed, int RecordFilter, short DataScope);

public interface IRbacAuthorizationService
{
    Task<RbacSessionContext> ResolveSessionAsync(long userNo, long? requestedBranchNo,
                                                 CancellationToken cancellationToken = default);

    /// <summary>
    /// Every branch the user can reach, across ALL companies — the list the pre-login company /
    /// branch picker needs.
    ///
    /// <para>This is deliberately NOT <see cref="RbacSessionContext.Branches"/>. That list is
    /// narrowed to the active session's company, which is right for the in-app branch switcher
    /// (you switch branch within a company, not across one) but wrong for discovery: a user with
    /// branches in two companies could only ever be offered whichever company happened to sort
    /// first.</para>
    /// </summary>
    Task<List<BranchDto>> ResolveAccessibleBranchesAsync(long userNo, CancellationToken cancellationToken = default);

    Task<FormPermissionResponse> GetFormPermissionAsync(long userNo, long? companyNo, long? branchNo, string formId,
                                                        CancellationToken cancellationToken = default);

    Task<bool> HasPermissionAsync(long userNo, long? companyNo, long? branchNo, string formId, string? action,
                                  CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the <c>record_filter</c> (1 = ALL, 2 = OWN) for the active form, so services can
    /// narrow lists to the caller's own records.
    /// </summary>
    Task<int> ResolveRecordFilterAsync(long userNo, long? companyNo, long? branchNo, string formId,
                                       CancellationToken cancellationToken = default);

    /// <summary>
    /// Single-resolve, cached access check for the per-request RBAC filter: returns BOTH the
    /// action-allowed decision and the record filter from ONE permission resolution.
    /// </summary>
    Task<FormAccess> ResolveAccessAsync(long userNo, long? companyNo, long? branchNo, string formId, string? action,
                                        CancellationToken cancellationToken = default);

    /// <summary>Invalidate all cached permission snapshots — call after a permission/role/membership change.</summary>
    void EvictPermissionCache();
}

/// <summary>
/// Resolves the <see cref="RbacSessionContext"/> for a user against the SYS re-plan model.
///
/// <list type="number">
///   <item>Branch list + role-per-branch = <c>sys_user_branch → sys_role</c> (role is a
///         <b>company-scoped template</b> assigned on the membership row).</item>
///   <item>Active branch = requested branch, or the user's default, or first available.</item>
///   <item>Roles in active branch = the <c>role_no</c>(s) on the user's <c>sys_user_branch</c>
///         rows for that branch.</item>
///   <item>Permissions = <c>sys_role_permission</c> ⨝ <c>sys_enroll_menu</c> (company-scoped,
///         optional branch override), gated by today.</item>
/// </list>
///
/// <para>The queries below are ported verbatim from the Java repositories' native SQL. Their
/// <c>GROUP BY</c> + <c>MAX</c>/<c>MIN</c> aggregation implements most-permissive grant semantics
/// (MAX for grants, MIN for <c>perm_scope</c>/<c>record_filter</c>) which LINQ would not reproduce
/// faithfully.</para>
/// </summary>
public class RbacAuthorizationService : IRbacAuthorizationService
{
    // ── RBAC permission cache (short-TTL, dependency-free) ─────────────────────
    // Permissions barely change within a session, but the heavy resolveFormPermission join would
    // otherwise run on EVERY authenticated request. TTL is kept short to bound staleness after a
    // permission change; EvictPermissionCache clears it explicitly.
    private const long PermTtlMillis = 30_000;
    private const int PermCacheMax = 20_000;

    private static readonly ConcurrentDictionary<string, PermSnapshot> PermCache = new();

    private readonly ISysDbContext _db;

    public RbacAuthorizationService(ISysDbContext db) => _db = db;

    // =========================================================================
    // Session resolution
    // =========================================================================

    /// <inheritdoc />
    public async Task<List<BranchDto>> ResolveAccessibleBranchesAsync(long userNo,
                                                                      CancellationToken cancellationToken = default)
    {
        var (_, branchOrder, branchMap) = await CollectBranchesAsync(userNo, cancellationToken);
        return branchOrder.Select(no => branchMap[no]).ToList();
    }

    /// <summary>
    /// The user's branches from both sources, de-duplicated and in priority order (default branch
    /// first). Shared by session resolution and by discovery so the two can never disagree about
    /// what a user can reach.
    /// </summary>
    private async Task<(List<RoleAccessProjection> RoleRows, List<long> Order, Dictionary<long, BranchDto> Map)>
        CollectBranchesAsync(long userNo, CancellationToken cancellationToken)
    {
        var roleRows = await FindRoleAccessForUserAsync(userNo, cancellationToken);

        var branchMap = new Dictionary<long, BranchDto>();
        var branchOrder = new List<long>();

        foreach (var r in roleRows)
        {
            if (r.BranchNo == null || branchMap.ContainsKey(r.BranchNo.Value)) continue;
            branchMap[r.BranchNo.Value] = ToBranchDto(r);
            branchOrder.Add(r.BranchNo.Value);
        }

        // Augment with branches the user is a member of via sys_user_branch (even if the join
        // above filtered a row out for any reason).
        foreach (var ub in await FindBranchesForUserAsync(userNo, cancellationToken))
        {
            if (ub.BranchNo == null || branchMap.ContainsKey(ub.BranchNo.Value)) continue;
            branchMap[ub.BranchNo.Value] = new BranchDto
            {
                BranchNo = ub.BranchNo,
                BranchName = ub.BranchName,
                CompanyNo = ub.CompanyNo,
                CompanyName = ub.CompanyName,
                IsActive = 1
            };
            branchOrder.Add(ub.BranchNo.Value);
        }

        return (roleRows, branchOrder, branchMap);
    }

    public async Task<RbacSessionContext> ResolveSessionAsync(long userNo, long? requestedBranchNo,
                                                              CancellationToken cancellationToken = default)
    {
        var roleRows = await FindRoleAccessForUserAsync(userNo, cancellationToken);

        var branchMap = new Dictionary<long, BranchDto>();
        var branchOrder = new List<long>();

        foreach (var r in roleRows)
        {
            if (r.BranchNo == null || branchMap.ContainsKey(r.BranchNo.Value)) continue;
            branchMap[r.BranchNo.Value] = ToBranchDto(r);
            branchOrder.Add(r.BranchNo.Value);
        }

        // Augment with branches the user is a member of via sys_user_branch (even if the join
        // above filtered a row out for any reason).
        foreach (var ub in await FindBranchesForUserAsync(userNo, cancellationToken))
        {
            if (ub.BranchNo == null || branchMap.ContainsKey(ub.BranchNo.Value)) continue;
            branchMap[ub.BranchNo.Value] = new BranchDto
            {
                BranchNo = ub.BranchNo,
                BranchName = ub.BranchName,
                CompanyNo = ub.CompanyNo,
                CompanyName = ub.CompanyName,
                IsActive = 1
            };
            branchOrder.Add(ub.BranchNo.Value);
        }

        if (branchMap.Count == 0)
        {
            throw new ValidationException("NO_ACTIVE_BRANCH_FOR_USER");
        }

        var branchNo = requestedBranchNo != null && branchMap.ContainsKey(requestedBranchNo.Value)
            ? requestedBranchNo.Value
            : branchOrder[0];

        if (!branchMap.TryGetValue(branchNo, out var active))
        {
            throw new ValidationException("BRANCH_ACCESS_DENIED");
        }

        // Roles assigned at the active branch.
        var roles = roleRows
            .Where(r => r.BranchNo == branchNo)
            .Select(r => new AuthRoleDto(r.RoleNo ?? 0, r.RoleName, (short)(r.IsDefault ?? 0)))
            .ToList();

        var roleNos = roles.Select(r => r.RoleNo).ToList();

        // Single menu/permission resolve — feeds the menu, the permission config AND the compact
        // bitmask map baked into the JWT (so per-request RBAC needs no further DB round trip).
        var permRows = roleNos.Count == 0
            ? new List<ResolvedMenuPermission>()
            : await ResolveMenuPermissionsAsync(roleNos, active.CompanyNo, branchNo,
                                                DateOnly.FromDateTime(DateTime.Today), cancellationToken);

        var menu = ToMenuItems(permRows);
        var permissions = BuildPermissionConfig(menu);
        var permBits = ToPermBits(permRows);

        var companyBranches = branchOrder
            .Select(no => branchMap[no])
            .Where(b => b.CompanyNo == active.CompanyNo)
            .ToList();

        return new RbacSessionContext
        {
            CompanyNo = active.CompanyNo,
            CompanyName = active.CompanyName,
            BranchNo = active.BranchNo,
            BranchName = active.BranchName,
            RoleNos = roleNos,
            Roles = roles,
            Branches = companyBranches,
            Menu = menu,
            Permissions = permissions,
            PermBits = permBits
        };
    }

    // =========================================================================
    // Per-form permission
    // =========================================================================

    public async Task<FormPermissionResponse> GetFormPermissionAsync(long userNo, long? companyNo, long? branchNo,
                                                                     string formId,
                                                                     CancellationToken cancellationToken = default)
    {
        var roleNos = await FindRoleNosForUserInBranchAsync(userNo, branchNo, cancellationToken);
        if (roleNos.Count == 0)
        {
            return Empty(formId);
        }

        var perm = await ResolveFormPermissionAsync(roleNos, companyNo, branchNo, formId,
                                                    DateOnly.FromDateTime(DateTime.Today), cancellationToken);

        if (perm == null || !AsBool(perm.CanView))
        {
            return Empty(formId);
        }

        return new FormPermissionResponse
        {
            FormId = formId,
            FormName = perm.FormName,
            CanView = AsBool(perm.CanView),
            CanInsert = AsBool(perm.CanInsert),
            CanUpdate = AsBool(perm.CanUpdate),
            CanDelete = AsBool(perm.CanDelete),
            CanApprove = AsBool(perm.CanApprove),
            CanPost = AsBool(perm.CanPost),
            CanCancel = AsBool(perm.CanCancel),
            CanExport = AsBool(perm.CanExport)
        };
    }

    public async Task<bool> HasPermissionAsync(long userNo, long? companyNo, long? branchNo, string formId,
                                               string? action, CancellationToken cancellationToken = default)
    {
        var permission = await GetFormPermissionAsync(userNo, companyNo, branchNo, formId, cancellationToken);

        return (action ?? "view").ToLowerInvariant() switch
        {
            "insert" or "create" => permission.CanInsert,
            "post" => permission.CanInsert, // → can_post column when surfaced on the DTO
            "update" or "edit" or "put" or "patch" or "submit" => permission.CanUpdate,
            "delete" => permission.CanDelete,
            "cancel" or "void" => permission.CanDelete, // → can_cancel
            "approve" or "reject" => permission.CanApprove,
            "export" => permission.CanView,
            _ => permission.CanView
        };
    }

    public async Task<int> ResolveRecordFilterAsync(long userNo, long? companyNo, long? branchNo, string formId,
                                                    CancellationToken cancellationToken = default) =>
        (await PermissionSnapshotAsync(userNo, companyNo, branchNo, formId, cancellationToken)).RecordFilter;

    public async Task<FormAccess> ResolveAccessAsync(long userNo, long? companyNo, long? branchNo, string formId,
                                                     string? action, CancellationToken cancellationToken = default)
    {
        var p = await PermissionSnapshotAsync(userNo, companyNo, branchNo, formId, cancellationToken);

        if (p.Denied)
        {
            return new FormAccess(false, p.RecordFilter, p.DataScope);
        }

        var allowed = (action ?? "view").ToLowerInvariant() switch
        {
            "insert" or "create" or "post" => p.CanInsert,
            "update" or "edit" or "put" or "patch" or "submit" => p.CanUpdate,
            "delete" or "cancel" or "void" => p.CanDelete,
            "approve" or "reject" => p.CanApprove,
            "export" => p.CanView,
            _ => p.CanView
        };

        return new FormAccess(allowed, p.RecordFilter, p.DataScope);
    }

    public void EvictPermissionCache() => PermCache.Clear();

    // =========================================================================
    // Cached snapshot
    // =========================================================================

    private readonly record struct PermSnapshot(bool Denied, bool CanView, bool CanInsert, bool CanUpdate,
                                                bool CanDelete, bool CanApprove, int RecordFilter, short DataScope, long ExpiresAt);

    /// <summary>Cache-aware resolve. On a hit, NO database round trip happens.</summary>
    private async Task<PermSnapshot> PermissionSnapshotAsync(long userNo, long? companyNo, long? branchNo,
                                                             string formId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var key = $"{userNo}|{companyNo}|{branchNo}|{formId}";

        if (PermCache.TryGetValue(key, out var cached) && cached.ExpiresAt > now)
        {
            return cached;
        }

        var fresh = await LoadPermissionSnapshotAsync(userNo, companyNo, branchNo, formId,
                                                      now + PermTtlMillis, cancellationToken);

        // Entries are short-lived; a crude bound is fine.
        if (PermCache.Count >= PermCacheMax) PermCache.Clear();
        PermCache[key] = fresh;

        return fresh;
    }

    private async Task<PermSnapshot> LoadPermissionSnapshotAsync(long userNo, long? companyNo, long? branchNo,
                                                                 string formId, long expiresAt,
                                                                 CancellationToken cancellationToken)
    {
        var roleNos = await FindRoleNosForUserInBranchAsync(userNo, branchNo, cancellationToken);
        if (roleNos.Count == 0)
        {
            return new PermSnapshot(true, false, false, false, false, false, 1, DataScopeConstants.Default, expiresAt);
        }

        var perm = await ResolveFormPermissionAsync(roleNos, companyNo, branchNo, formId,
                                                    DateOnly.FromDateTime(DateTime.Today), cancellationToken);

        if (perm == null || !AsBool(perm.CanView))
        {
            return new PermSnapshot(true, false, false, false, false, false, 1, DataScopeConstants.Default, expiresAt);
        }

        var rf = perm.RecordFilter ?? 1;

        return new PermSnapshot(false, true, AsBool(perm.CanInsert), AsBool(perm.CanUpdate),
                                AsBool(perm.CanDelete), AsBool(perm.CanApprove), rf, DataScopeConstants.Normalize(perm.DataScope), expiresAt);
    }

    // =========================================================================
    // Projection → DTO
    // =========================================================================

    private static List<MenuItemDto> ToMenuItems(IEnumerable<ResolvedMenuPermission> rows) =>
        rows.Select(row => new MenuItemDto
        {
            FormId = row.FormId,
            Type = row.Type,
            Name = row.FormName,
            Icon = row.Icon,
            Route = row.Route,
            Module = row.ModuleName,
            Code = row.ModuleCode,
            Submodule = row.SubmoduleName,
            ModuleIcon = row.ModuleIcon,
            SubmoduleIcon = row.SubmoduleIcon,
            ModuleSerial = row.ModuleSerial,
            CanInsert = AsBool(row.CanInsert),
            CanUpdate = AsBool(row.CanUpdate),
            CanView = AsBool(row.CanView),
            CanDelete = AsBool(row.CanDelete),
            CanApprove = AsBool(row.CanApprove)
        }).ToList();

    /// <summary>
    /// Compact per-form permission map for the JWT (formId → <see cref="PermissionBits"/> mask).
    /// Only view-able forms are included (the resolve query already requires <c>can_view = 1</c>),
    /// so the claim is bounded by the user's actual menu size.
    /// </summary>
    private static Dictionary<string, int> ToPermBits(IEnumerable<ResolvedMenuPermission> rows)
    {
        var bits = new Dictionary<string, int>();

        foreach (var row in rows)
        {
            if (row.FormId == null) continue;

            var ownOnly = row.RecordFilter != null
                          && row.RecordFilter == CurrentPermissionContext.RecordFilterOwn;

            var mask = PermissionBits.Encode(
                AsBool(row.CanView),
                AsBool(row.CanInsert),
                AsBool(row.CanUpdate),
                AsBool(row.CanDelete),
                AsBool(row.CanApprove),
                ownOnly,
                DataScopeConstants.Normalize(row.DataScope));

            // Multiple menu rows can share a form id across submodules — OR the permission bits
            // together. The scope field is NOT a flag set: OR-ing 2 bits would invent a scope
            // (BRANCH|EMPLOYEE = EMPLOYEE), so it is merged separately, widest (lowest) wins —
            // matching the MIN() the SQL uses to aggregate across roles.
            if (bits.TryGetValue(row.FormId, out var existing))
            {
                var widest = Math.Min(PermissionBits.DataScopeOf(existing), PermissionBits.DataScopeOf(mask));
                var merged = (existing | mask) & ~PermissionBits.ScopeMask;
                bits[row.FormId] = merged | (widest << PermissionBits.ScopeShift);
            }
            else
            {
                bits[row.FormId] = mask;
            }
        }

        return bits;
    }

    private static PermissionConfigDto BuildPermissionConfig(IEnumerable<MenuItemDto> menu)
    {
        var modules = new Dictionary<string, ModulePermissionDto>();
        var order = new List<string>();

        foreach (var item in menu)
        {
            var code = item.Code ?? string.Empty;

            if (!modules.TryGetValue(code, out var module))
            {
                module = new ModulePermissionDto { Module = code, Enabled = true };
                modules[code] = module;
                order.Add(code);
            }

            if (item.FormId != null)
            {
                module.Forms[item.FormId] = new FormPermissionDto(
                    item.CanView, item.CanInsert, item.CanUpdate, item.CanDelete, item.CanApprove);
            }
        }

        return new PermissionConfigDto(order.Select(c => modules[c]).ToList());
    }

    private static BranchDto ToBranchDto(RoleAccessProjection row) => new()
    {
        CompanyNo = row.CompanyNo,
        CompanyName = row.CompanyName,
        BranchNo = row.BranchNo,
        BranchName = row.BranchName,
        IsActive = 1
    };

    private static FormPermissionResponse Empty(string formId) => new()
    {
        FormId = formId,
        CanView = false,
        CanInsert = false,
        CanUpdate = false,
        CanDelete = false,
        CanApprove = false,
        CanPost = false,
        CanCancel = false,
        CanExport = false
    };

    private static bool AsBool(int? value) => value is > 0;

    // =========================================================================
    // Queries — ported verbatim from the Java repositories' native SQL
    // =========================================================================

    private Task<List<RoleAccessProjection>> FindRoleAccessForUserAsync(long userNo, CancellationToken ct) =>
        RelationalDatabaseFacadeExtensions.SqlQueryRaw<RoleAccessProjection>(
            _db.Database,
            """
            SELECT ub.user_branch_no AS "UserBranchNo",
                   r.role_no         AS "RoleNo",
                   r.role_name       AS "RoleName",
                   b.branch_no       AS "BranchNo",
                   b.branch_name     AS "BranchName",
                   b.company_no      AS "CompanyNo",
                   c.company_name    AS "CompanyName",
                   COALESCE(ub.is_default, 0) AS "IsDefault"
            FROM   sys_user_branch ub
            JOIN   sys_role    r ON r.role_no   = ub.role_no
                                AND r.is_active = 1 AND r.is_deleted = 0
            JOIN   sys_branch  b ON b.branch_no = ub.branch_no
                                AND b.is_active = 1 AND b.is_deleted = 0
            JOIN   sys_company c ON c.company_no = b.company_no
                                AND c.is_active = 1 AND c.is_deleted = 0
            WHERE  ub.user_no   = {0}
              AND  ub.is_active = 1
              AND  ub.is_deleted = 0
            ORDER  BY "IsDefault" DESC, c.company_name, b.branch_name, r.role_name
            """, userNo).ToListAsync(ct);

    private Task<List<UserBranchProjection>> FindBranchesForUserAsync(long userNo, CancellationToken ct) =>
        RelationalDatabaseFacadeExtensions.SqlQueryRaw<UserBranchProjection>(
            _db.Database,
            """
            SELECT c.company_no   AS "CompanyNo",
                   c.company_name AS "CompanyName",
                   b.branch_no    AS "BranchNo",
                   b.branch_name  AS "BranchName",
                   ub.is_default  AS "IsDefault"
            FROM   sys_user_branch ub
            JOIN   sys_branch  b ON b.branch_no = ub.branch_no
                                AND b.is_active = 1 AND b.is_deleted = 0
            JOIN   sys_company c ON c.company_no = b.company_no
                                AND c.is_active = 1 AND c.is_deleted = 0
            WHERE  ub.user_no = {0}
              AND  ub.is_active = 1 AND ub.is_deleted = 0
            ORDER  BY ub.is_default DESC, c.company_name, b.branch_name
            """, userNo).ToListAsync(ct);

    private Task<List<long>> FindRoleNosForUserInBranchAsync(long userNo, long? branchNo, CancellationToken ct) =>
        RelationalDatabaseFacadeExtensions.SqlQueryRaw<long>(
            _db.Database,
            """
            SELECT r.role_no AS "Value"
            FROM   sys_user_branch ub
            JOIN   sys_role r ON r.role_no = ub.role_no
                              AND r.is_active = 1 AND r.is_deleted = 0
            WHERE  ub.user_no   = {0}
              AND  ub.branch_no = {1}
              AND  ub.is_active = 1
              AND  ub.is_deleted = 0
            """, userNo, branchNo ?? -1L).ToListAsync(ct);

    private Task<List<ResolvedMenuPermission>> ResolveMenuPermissionsAsync(
        IReadOnlyCollection<long> roleNos, long? companyNo, long? branchNo, DateOnly today, CancellationToken ct) =>
        RelationalDatabaseFacadeExtensions.SqlQueryRaw<ResolvedMenuPermission>(
            _db.Database,
            """
            SELECT m.form_id                          AS "FormId",
                   LOWER(COALESCE(m.menu_type,'form')) AS "Type",
                   m.form_name                        AS "FormName",
                   m.icon_name                        AS "Icon",
                   m.route_path                       AS "Route",
                   mo.module_name                     AS "ModuleName",
                   mo.module_code                     AS "ModuleCode",
                   sm.submodule_name                  AS "SubmoduleName",
                   mo.module_icon                     AS "ModuleIcon",
                   sm.submodule_icon                  AS "SubmoduleIcon",
                   mo.order_sl                        AS "ModuleSerial",
                   MAX(COALESCE(rp.can_view, 0))      AS "CanView",
                   MAX(COALESCE(rp.can_insert, 0))    AS "CanInsert",
                   MAX(COALESCE(rp.can_update, 0))    AS "CanUpdate",
                   MAX(COALESCE(rp.can_delete, 0))    AS "CanDelete",
                   MAX(COALESCE(rp.can_approve, 0))   AS "CanApprove",
                   MAX(COALESCE(rp.can_post, 0))      AS "CanPost",
                   MAX(COALESCE(rp.can_cancel, 0))    AS "CanCancel",
                   MAX(COALESCE(rp.can_export, 0))    AS "CanExport",
                   0                                  AS "CanPrint",
                   MIN(COALESCE(rp.perm_scope, 2))    AS "PermScope",
                   MIN(COALESCE(rp.record_filter, 1)) AS "RecordFilter",
                   MIN(COALESCE(rp.data_scope, 1))    AS "DataScope"
            FROM   sys_role_permission rp
            JOIN   sys_role  r  ON r.role_no   = rp.role_no
                                AND r.is_active = 1 AND r.is_deleted = 0
            JOIN   sys_menu  m  ON m.menu_no   = rp.menu_no
                                AND m.is_active = 1 AND m.is_deleted = 0
            JOIN   sys_submodule sm ON sm.submodule_no = m.submodule_no
                                AND sm.is_active = 1 AND sm.is_deleted = 0
            JOIN   sys_module    mo ON mo.module_no  = sm.module_no
                                AND mo.is_active = 1 AND mo.is_deleted = 0
            JOIN   sys_enroll_menu em ON em.menu_no = m.menu_no
                                AND em.company_no = {1}
                                AND (em.branch_no IS NULL OR em.branch_no = {2})
                                AND em.is_active = 1 AND em.is_deleted = 0
                                AND ( em.is_lifetime = 1
                                   OR ( COALESCE(em.enroll_start_date, DATE '1900-01-01') <= {3}
                                    AND COALESCE(em.enroll_end_date,   DATE '9999-12-31') >= {3} ) )
            WHERE  rp.role_no = ANY({0})
              AND  rp.is_active = 1 AND rp.is_deleted = 0
            GROUP  BY m.form_id, m.menu_type, m.form_name, m.icon_name, m.route_path,
                      mo.module_name, mo.module_code, sm.submodule_name,
                      mo.module_icon, sm.submodule_icon, mo.order_sl
            HAVING MAX(COALESCE(rp.can_view, 0)) = 1
            ORDER  BY mo.order_sl NULLS LAST, mo.module_name,
                      MIN(sm.order_sl) NULLS LAST, sm.submodule_name,
                      MIN(m.order_sl)  NULLS LAST, m.form_name
            """, roleNos.ToArray(), companyNo ?? -1L, branchNo ?? -1L, today).ToListAsync(ct);

    private async Task<ResolvedMenuPermission?> ResolveFormPermissionAsync(
        IReadOnlyCollection<long> roleNos, long? companyNo, long? branchNo, string formId, DateOnly today,
        CancellationToken ct)
    {
        var rows = await RelationalDatabaseFacadeExtensions.SqlQueryRaw<ResolvedMenuPermission>(
            _db.Database,
            """
            SELECT MAX(COALESCE(rp.can_view, 0))      AS "CanView",
                   MAX(COALESCE(rp.can_insert, 0))    AS "CanInsert",
                   MAX(COALESCE(rp.can_update, 0))    AS "CanUpdate",
                   MAX(COALESCE(rp.can_delete, 0))    AS "CanDelete",
                   MAX(COALESCE(rp.can_approve, 0))   AS "CanApprove",
                   MAX(COALESCE(rp.can_post, 0))      AS "CanPost",
                   MAX(COALESCE(rp.can_cancel, 0))    AS "CanCancel",
                   MAX(COALESCE(rp.can_export, 0))    AS "CanExport",
                   0                                  AS "CanPrint",
                   MIN(COALESCE(rp.perm_scope, 2))    AS "PermScope",
                   MIN(COALESCE(rp.record_filter, 1)) AS "RecordFilter",
                   MIN(COALESCE(rp.data_scope, 1))    AS "DataScope",
                   m.form_id                          AS "FormId",
                   LOWER(COALESCE(m.menu_type,'form')) AS "Type",
                   m.form_name                        AS "FormName",
                   m.icon_name                        AS "Icon",
                   m.route_path                       AS "Route",
                   mo.module_name                     AS "ModuleName",
                   mo.module_code                     AS "ModuleCode",
                   sm.submodule_name                  AS "SubmoduleName",
                   mo.module_icon                     AS "ModuleIcon",
                   sm.submodule_icon                  AS "SubmoduleIcon",
                   mo.order_sl                        AS "ModuleSerial"
            FROM   sys_role_permission rp
            JOIN   sys_role  r  ON r.role_no   = rp.role_no
                                AND r.is_active = 1 AND r.is_deleted = 0
            JOIN   sys_menu  m  ON m.menu_no   = rp.menu_no
                                AND m.form_id  = {3}
                                AND m.is_active = 1 AND m.is_deleted = 0
            JOIN   sys_submodule sm ON sm.submodule_no = m.submodule_no
                                AND sm.is_active = 1 AND sm.is_deleted = 0
            JOIN   sys_module    mo ON mo.module_no  = sm.module_no
                                AND mo.is_active = 1 AND mo.is_deleted = 0
            JOIN   sys_enroll_menu em ON em.menu_no = m.menu_no
                                AND em.company_no = {1}
                                AND (em.branch_no IS NULL OR em.branch_no = {2})
                                AND em.is_active = 1 AND em.is_deleted = 0
                                AND ( em.is_lifetime = 1
                                   OR ( COALESCE(em.enroll_start_date, DATE '1900-01-01') <= {4}
                                    AND COALESCE(em.enroll_end_date,   DATE '9999-12-31') >= {4} ) )
            WHERE  rp.role_no = ANY({0})
              AND  rp.is_active = 1 AND rp.is_deleted = 0
            GROUP BY m.form_id, m.menu_type, m.form_name, m.icon_name, m.route_path,
                     mo.module_name, mo.module_code, sm.submodule_name,
                     mo.module_icon, sm.submodule_icon, mo.order_sl
            """, roleNos.ToArray(), companyNo ?? -1L, branchNo ?? -1L, formId, today).ToListAsync(ct);

        return rows.FirstOrDefault();
    }
}

// =============================================================================
// Query projections — property names must match the SQL column aliases
// =============================================================================

public class RoleAccessProjection
{
    public long? UserBranchNo { get; set; }
    public long? RoleNo { get; set; }
    public string? RoleName { get; set; }
    public long? BranchNo { get; set; }
    public string? BranchName { get; set; }
    public long? CompanyNo { get; set; }
    public string? CompanyName { get; set; }
    public short? IsDefault { get; set; }
}

public class UserBranchProjection
{
    public long? CompanyNo { get; set; }
    public string? CompanyName { get; set; }
    public long? BranchNo { get; set; }
    public string? BranchName { get; set; }
    public short? IsDefault { get; set; }
}

public class ResolvedMenuPermission
{
    public string? FormId { get; set; }
    public string? Type { get; set; }
    public string? FormName { get; set; }
    public string? Icon { get; set; }
    public string? Route { get; set; }
    public string? ModuleName { get; set; }
    public string? ModuleCode { get; set; }
    public string? SubmoduleName { get; set; }
    public string? ModuleIcon { get; set; }
    public string? SubmoduleIcon { get; set; }
    public int? ModuleSerial { get; set; }
    public int? CanView { get; set; }
    public int? CanInsert { get; set; }
    public int? CanUpdate { get; set; }
    public int? CanDelete { get; set; }
    public int? CanApprove { get; set; }
    public int? CanPost { get; set; }
    public int? CanCancel { get; set; }
    public int? CanExport { get; set; }
    public int? CanPrint { get; set; }
    public int? PermScope { get; set; }
    public int? RecordFilter { get; set; }
    public short? DataScope { get; set; }
}
