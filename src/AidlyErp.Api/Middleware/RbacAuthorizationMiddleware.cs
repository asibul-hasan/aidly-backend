using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using AidlyErp.Application.Auth.Services;
using AidlyErp.Application.Common.Interfaces;
using AidlyErp.Application.Common.Security;
using AidlyErp.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace AidlyErp.Api.Middleware;

/// <summary>
/// Per-request RBAC gate. Port of <c>core.auth.interceptor.RbacAuthorizationInterceptor</c>.
///
/// <para>The decision is normally a pure in-memory bitmask lookup against the permissions carried
/// in the JWT (<c>perms</c> claim) — <b>zero database queries</b>. The only lookup that could touch
/// the DB is mapping the URL → form id, and that is memoized per normalized path so a given
/// endpoint shape hits the DB at most once. Tokens issued before this feature (no <c>perms</c>
/// claim) fall back to the cached DB resolver for backward compatibility.</para>
///
/// <para>Must run <b>after</b> <see cref="TenantContextMiddleware"/> — it reads the company/branch
/// and permission bits that middleware populates from the validated token.</para>
/// </summary>
public class RbacAuthorizationMiddleware
{
    public const string FormIdHeader = "X-Form-Id";

    private const string RouteFormMismatch = "__ROUTE_FORM_MISMATCH__";
    private const string NoForm = ""; // cache sentinel: "this path maps to no form"
    private const int PathCacheMax = 10_000;

    private static readonly Regex NumericSegment = new(@"/\d+", RegexOptions.Compiled);

    /// <summary>Normalized request path → form id (or <see cref="NoForm"/>).</summary>
    private static readonly ConcurrentDictionary<string, string> PathFormCache = new();

    private readonly RequestDelegate _next;

    public RbacAuthorizationMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context,
                                  IRbacAuthorizationService rbac,
                                  IApplicationDbContext db,
                                  ICompanyBranchContext tenant,
                                  ICurrentPermissionContext permissionContext)
    {
        if (IsAuthOrPublicRequest(context.Request))
        {
            await _next(context);
            return;
        }

        var formId = await ResolveFormIdAsync(context.Request, db, context.RequestAborted);
        if (string.IsNullOrWhiteSpace(formId))
        {
            await _next(context); // unmapped endpoint → not RBAC-gated by form
            return;
        }

        var userNo = tenant.UserNo;
        var companyNo = tenant.CompanyNo;
        var branchNo = tenant.BranchNo;
        var companyWide = tenant.IsCompanyWide;

        // BRANCH-scoped users need an active branch; COMPANY/GLOBAL may operate without one.
        var contextOk = userNo != null && companyNo != null && (branchNo != null || companyWide);
        if (!contextOk)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        var action = ResolveAction(context.Request);

        // ── Fast path: decide from the signed JWT permission claim — NO database query ──
        var permBits = tenant.PermBits;
        if (permBits != null)
        {
            var mask = permBits.TryGetValue(formId, out var m) ? m : 0;

            // Must have VIEW on the form AND the bit the action requires
            // (a mismatched form id yields mask 0 → deny).
            var allowedByBits = (mask & PermissionBits.View) != 0 && PermissionBits.Allows(mask, action);
            if (!allowedByBits)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            var recordFilter = PermissionBits.IsOwnOnly(mask)
                ? CurrentPermissionContext.RecordFilterOwn
                : CurrentPermissionContext.RecordFilterAll;

            permissionContext.Set(formId, recordFilter, userNo);
            await _next(context);
            return;
        }

        // ── Fallback (legacy token without the perms claim): cached DB resolver ──
        var access = await rbac.ResolveAccessAsync(userNo!.Value, companyNo, branchNo, formId, action,
                                                   context.RequestAborted);
        if (!access.Allowed)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        permissionContext.Set(formId, access.RecordFilter, userNo);
        await _next(context);
    }

    private static async Task<string?> ResolveFormIdAsync(HttpRequest request, IApplicationDbContext db,
                                                          CancellationToken cancellationToken)
    {
        var requestPath = NormalizePath(request.Path.Value);
        var withoutApiPrefix = StripApiPrefix(requestPath);

        var mappedFormId = await CachedFormIdForPathAsync(requestPath, withoutApiPrefix, db, cancellationToken);

        var headerFormId = request.Headers[FormIdHeader].FirstOrDefault()?.Trim();
        if (string.IsNullOrEmpty(headerFormId)) headerFormId = null;

        // The URL→form mapping is authoritative; the header may only supply a form id when the
        // path maps to none (nothing to spoof against). A header that contradicts the path is
        // rejected — RouteFormMismatch resolves to mask 0 and therefore denies.
        if (mappedFormId != null && headerFormId != null && mappedFormId != headerFormId)
        {
            return RouteFormMismatch;
        }

        return mappedFormId ?? headerFormId;
    }

    /// <summary>
    /// Memoized URL→form resolution: numeric path segments are collapsed so <c>/5</c> and
    /// <c>/9</c> share one cache key.
    /// </summary>
    private static async Task<string?> CachedFormIdForPathAsync(string requestPath, string withoutApiPrefix,
                                                                IApplicationDbContext db,
                                                                CancellationToken cancellationToken)
    {
        var key = NumericSegment.Replace(requestPath, "/{id}");

        if (PathFormCache.TryGetValue(key, out var cached))
        {
            return cached == NoForm ? null : cached;
        }

        var resolved = await FindBestFormIdForRequestPathAsync(requestPath, withoutApiPrefix, db, cancellationToken);

        // Crude bound; the route table is tiny and static.
        if (PathFormCache.Count >= PathCacheMax) PathFormCache.Clear();
        PathFormCache[key] = resolved ?? NoForm;

        return resolved;
    }

    /// <summary>
    /// Finds the <c>form_id</c> that best matches an incoming request URI by walking the menu's
    /// <c>route_path</c>. Ported verbatim from <c>MenuRepository.findBestFormIdForRequestPath</c>.
    /// </summary>
    private static async Task<string?> FindBestFormIdForRequestPathAsync(string requestPath, string withoutApiPrefix,
                                                                         IApplicationDbContext db,
                                                                         CancellationToken cancellationToken)
    {
        var rows = await db.Database.SqlQueryRaw<string?>(
            """
            SELECT m.form_id AS "Value"
            FROM   sys_menu m
            JOIN   sys_submodule s ON s.submodule_no = m.submodule_no
                                  AND s.is_active   = 1
                                  AND s.is_deleted  = 0
            JOIN   sys_module    mo ON mo.module_no = s.module_no
                                   AND mo.is_active = 1
                                   AND mo.is_deleted= 0
            WHERE  m.form_id IS NOT NULL
              AND  m.route_path IS NOT NULL
              AND  m.is_active = 1
              AND  m.is_deleted = 0
              AND  ( m.route_path = {0}
                  OR m.route_path = {1}
                  OR {0} LIKE CONCAT(m.route_path, '/%')
                  OR {1} LIKE CONCAT(m.route_path, '/%') )
            ORDER BY LENGTH(m.route_path) DESC
            LIMIT 1
            """, requestPath, withoutApiPrefix).ToListAsync(cancellationToken);

        return rows.FirstOrDefault();
    }

    /// <summary>Invalidate the URL→form cache — call after menu route paths change (rare).</summary>
    public static void EvictPathFormCache() => PathFormCache.Clear();

    private static string NormalizePath(string? uri)
    {
        if (string.IsNullOrWhiteSpace(uri)) return "/";
        var queryIndex = uri.IndexOf('?');
        return queryIndex >= 0 ? uri[..queryIndex] : uri;
    }

    private static string StripApiPrefix(string path)
    {
        if (path.StartsWith("/api/v1/", StringComparison.Ordinal)) return "/" + path["/api/v1/".Length..];
        if (path.StartsWith("/api/", StringComparison.Ordinal)) return "/" + path["/api/".Length..];
        return path;
    }

    /// <summary>
    /// Resolves the RBAC action from the request. Workflow sub-paths (e.g. <c>/approve</c>,
    /// <c>/post</c>, <c>/cancel</c>) map to the privileged action so that, say, a user with only
    /// <c>can_insert</c> cannot hit an <c>/approve</c> endpoint. Falls back to the HTTP method.
    /// </summary>
    private static string ResolveAction(HttpRequest request)
    {
        var segment = LastPathSegment(NormalizePath(request.Path.Value));

        if (segment != null)
        {
            switch (segment.ToLowerInvariant())
            {
                case "approve":
                case "reject":
                case "dispatch":
                case "receive":
                case "close":
                    return "approve";
                case "post":
                    return "post";
                case "cancel":
                case "void":
                    return "cancel";
                case "submit":
                    return "update";
                case "export":
                    return "export";
            }
        }

        return ActionForMethod(request.Method);
    }

    private static string? LastPathSegment(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        var p = path.EndsWith('/') ? path[..^1] : path;
        var idx = p.LastIndexOf('/');
        return idx >= 0 && idx < p.Length - 1 ? p[(idx + 1)..] : p;
    }

    private static string ActionForMethod(string method) => method.ToUpperInvariant() switch
    {
        "POST" => "insert",
        "PUT" or "PATCH" => "update",
        "DELETE" => "delete",
        _ => "view"
    };

    private static bool IsAuthOrPublicRequest(HttpRequest request)
    {
        var uri = request.Path.Value ?? string.Empty;
        return uri.StartsWith("/api/v1/auth/", StringComparison.Ordinal)
               || uri.StartsWith("/actuator/", StringComparison.Ordinal)
               || uri.StartsWith("/v3/api-docs", StringComparison.Ordinal)
               || uri.StartsWith("/swagger-ui", StringComparison.Ordinal)
               || uri.StartsWith("/uploads/", StringComparison.Ordinal);
    }
}
