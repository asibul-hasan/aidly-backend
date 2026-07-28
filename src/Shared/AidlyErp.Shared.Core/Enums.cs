namespace AidlyErp.Shared.Core;

/// <summary>
/// Data-access scope for a user/session, per the SYS re-plan (sys-db.md §1.3, §3.4).
///
/// <list type="bullet">
///   <item><see cref="Branch"/>  — restricted to the active branch (and branches the user is assigned to).</item>
///   <item><see cref="Company"/> — all branches within the active company.</item>
///   <item><see cref="Global"/>  — all companies the user can access (owner / auditor of sister concerns).</item>
/// </list>
///
/// Maps to <c>sys_user.access_scope</c> (1=BRANCH, 2=COMPANY, 3=GLOBAL). The tenant filter
/// drops the branch predicate when the scope is company-wide.
/// </summary>
public enum AccessScope : short
{
    Branch = 1,
    Company = 2,
    Global = 3
}

public static class AccessScopeExtensions
{
    /// <summary>COMPANY and GLOBAL see across branches; BRANCH does not.</summary>
    public static bool IsCompanyWide(this AccessScope scope) =>
        scope == AccessScope.Company || scope == AccessScope.Global;

    /// <summary>True only for GLOBAL — visibility spans companies (cross-company consolidation).</summary>
    public static bool IsCrossCompany(this AccessScope scope) => scope == AccessScope.Global;

    public static int GetCode(this AccessScope scope) => (int)scope;

    /// <summary>
    /// Null-safe mapping from the <c>sys_user.access_scope</c> SMALLINT; defaults to the
    /// safest scope (BRANCH).
    /// </summary>
    public static AccessScope FromCode(int? code) => code switch
    {
        2 => AccessScope.Company,
        3 => AccessScope.Global,
        _ => AccessScope.Branch
    };

    public static AccessScope FromCode(long? code) => FromCode(code.HasValue ? (int)code.Value : null);

    public static AccessScope FromCode(short? code) => FromCode(code.HasValue ? (int)code.Value : null);
}

/// <summary>
/// Compact bitmask encoding of a user's per-form permission, carried inside the JWT so the
/// per-request RBAC check is a pure in-memory lookup (zero DB round trips).
///
/// <para>Layout (one <c>int</c> per form id):</para>
/// <code>
///   bit0 (1)  VIEW
///   bit1 (2)  INSERT  (create / post)
///   bit2 (4)  UPDATE  (edit / put / patch / submit)
///   bit3 (8)  DELETE  (cancel / void)
///   bit4 (16) APPROVE (reject)
///   bit5 (32) OWN     record_filter == OWN (record-level narrowing to created_by = caller)
/// </code>
///
/// <para>The action → required-bit mapping mirrors the legacy DB-backed resolver
/// (<c>RbacAuthorizationService.ResolveAccess</c>) so behaviour is identical — only the
/// data source moved from a per-request query to a signed token claim.</para>
/// </summary>
public static class PermissionBits
{
    public const int View = 1;
    public const int Insert = 2;
    public const int Update = 4;
    public const int Delete = 8;
    public const int Approve = 16;
    public const int Own = 32;

    /// <summary>Builds the mask for one form from its resolved permission flags.</summary>
    public static int Encode(bool canView, bool canInsert, bool canUpdate,
                             bool canDelete, bool canApprove, bool ownOnly)
    {
        var mask = 0;
        if (canView) mask |= View;
        if (canInsert) mask |= Insert;
        if (canUpdate) mask |= Update;
        if (canDelete) mask |= Delete;
        if (canApprove) mask |= Approve;
        if (ownOnly) mask |= Own;
        return mask;
    }

    /// <summary>The single permission bit an HTTP action requires (matches the legacy resolver's switch).</summary>
    public static int RequiredBit(string? action) =>
        (action ?? "view").ToLowerInvariant() switch
        {
            "insert" or "create" or "post" => Insert,
            "update" or "edit" or "put" or "patch" or "submit" => Update,
            "delete" or "cancel" or "void" => Delete,
            "approve" or "reject" => Approve,
            "export" => View,
            _ => View // view / unknown
        };

    /// <summary>True when <paramref name="mask"/> grants the permission required by <paramref name="action"/>.</summary>
    public static bool Allows(int mask, string? action) => (mask & RequiredBit(action)) != 0;

    /// <summary>True when the form's mask grants only OWN-record visibility.</summary>
    public static bool IsOwnOnly(int mask) => (mask & Own) != 0;
}

/// <summary>
/// Names for the tenant query filter (SYS re-plan §3.4 — "the cardinal rule").
/// EF Core applies the predicate as a global query filter on <see cref="BaseEntity"/>
/// descendants; <c>TenantFilters.RunUnfiltered</c> is expressed in EF via
/// <c>IgnoreQueryFilters()</c> for deliberate, audited cross-tenant reads.
/// </summary>
public static class TenantFilters
{
    public const string TenantFilter = "tenantFilter";
    public const string ParamCompanyNo = "companyNo";
    public const string ParamBranchNo = "branchNo";
    public const string ParamCompanyScope = "companyScope";
}
