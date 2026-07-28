using AidlyErp.Shared.Core;

namespace AidlyErp.Shared.Core.Security;

/// <summary>
/// Stateless helpers for reading the current-request security context. Use these instead of
/// duplicating the same lookup in every service.
///
/// <para>The Java <c>SecurityUtils</c> is a static class reading Spring's
/// <c>SecurityContextHolder</c>; in .NET the per-request state lives on the DI-scoped
/// <see cref="ICompanyBranchContext"/>, so the same helpers are exposed as extension methods
/// on it. Semantics — including the fallbacks and defaults — are identical.</para>
/// </summary>
public static class SecurityUtils
{
    /// <summary>
    /// The <c>user_no</c> of the currently authenticated user, or <c>0</c> for
    /// unauthenticated/anonymous requests.
    /// </summary>
    public static long CurrentUserNo(this ICompanyBranchContext? context) =>
        context?.UserNo ?? 0L;

    /// <summary>Active company of the current request.</summary>
    public static long? CurrentCompanyNo(this ICompanyBranchContext? context) =>
        context?.CompanyNo;

    /// <summary>Active branch of the current request.</summary>
    public static long? CurrentBranchNo(this ICompanyBranchContext? context) =>
        context?.BranchNo;

    /// <summary>Active session of the current request.</summary>
    public static long? CurrentSessionNo(this ICompanyBranchContext? context) =>
        context?.SessionNo;

    /// <summary>Data-access scope of the current user; defaults to <see cref="AccessScope.Branch"/>.</summary>
    public static AccessScope CurrentAccessScope(this ICompanyBranchContext? context) =>
        context?.Scope ?? AccessScope.Branch;
}
