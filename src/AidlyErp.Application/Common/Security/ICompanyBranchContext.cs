using AidlyErp.Domain.Common;

namespace AidlyErp.Application.Common.Security;

/// <summary>
/// Request-scoped tenant context, populated from JWT claims by the tenant middleware and
/// discarded with the request scope.
///
/// <para>Per the SYS re-plan it carries the active company/branch, the acting user/session,
/// and the user's <see cref="Domain.Common.AccessScope"/> — the latter drives the tenant query
/// filter (branch-narrowed for BRANCH scope, company-wide for COMPANY/GLOBAL).</para>
///
/// <para>The Java original used <c>ThreadLocal</c> statics cleared by a servlet filter; the
/// .NET equivalent is a DI-scoped instance, which gives the same per-request isolation without
/// the manual cleanup (and is async-safe, which <c>ThreadLocal</c> is not).</para>
/// </summary>
public interface ICompanyBranchContext
{
    long? CompanyNo { get; }
    long? BranchNo { get; }
    long? UserNo { get; }
    long? SessionNo { get; }
    string? UserId { get; }
    string? UserName { get; }

    /// <summary>Raw <c>sys_user.access_scope</c> code (1=BRANCH, 2=COMPANY, 3=GLOBAL).</summary>
    int AccessScope { get; }

    /// <summary>Typed view of <see cref="AccessScope"/>; defaults to the safest scope (BRANCH).</summary>
    AccessScope Scope { get; }

    /// <summary>True when the current scope sees across branches (COMPANY/GLOBAL).</summary>
    bool IsCompanyWide { get; }

    bool IsAuthenticated { get; }

    /// <summary>
    /// Per-form permission bitmasks carried in the JWT (formId → mask); see
    /// <see cref="PermissionBits"/>. <c>null</c> for legacy tokens, which fall back to the
    /// DB-backed resolver.
    /// </summary>
    IReadOnlyDictionary<string, int>? PermBits { get; }

    void SetContext(long? companyNo, long? branchNo, long? userNo, string? userId, string? userName, int accessScope = 1);

    void SetSessionNo(long? sessionNo);

    void SetPermBits(IReadOnlyDictionary<string, int>? permBits);

    void Clear();
}

public class CompanyBranchContext : ICompanyBranchContext
{
    public long? CompanyNo { get; private set; }
    public long? BranchNo { get; private set; }
    public long? UserNo { get; private set; }
    public long? SessionNo { get; private set; }
    public string? UserId { get; private set; }
    public string? UserName { get; private set; }
    public int AccessScope { get; private set; } = 1;

    public AccessScope Scope => AccessScopeExtensions.FromCode(AccessScope);

    public bool IsCompanyWide => Scope.IsCompanyWide();

    public bool IsAuthenticated => UserNo.HasValue && UserNo > 0;

    public IReadOnlyDictionary<string, int>? PermBits { get; private set; }

    public void SetContext(long? companyNo, long? branchNo, long? userNo, string? userId, string? userName, int accessScope = 1)
    {
        CompanyNo = companyNo;
        BranchNo = branchNo;
        UserNo = userNo;
        UserId = userId;
        UserName = userName;
        AccessScope = accessScope;
    }

    public void SetSessionNo(long? sessionNo) => SessionNo = sessionNo;

    public void SetPermBits(IReadOnlyDictionary<string, int>? permBits) => PermBits = permBits;

    public void Clear()
    {
        CompanyNo = null;
        BranchNo = null;
        UserNo = null;
        SessionNo = null;
        UserId = null;
        UserName = null;
        AccessScope = 1;
        PermBits = null;
    }
}
