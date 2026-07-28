using System.Security.Claims;
using System.Text.Json;
using AidlyErp.Sys.Application.Auth.Services;
using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Security;

namespace AidlyErp.Api.Middleware;

/// <summary>
/// Populates the request-scoped tenant context from the validated JWT. The .NET counterpart of the
/// claim-reading half of <c>core.security.JwtAuthenticationFilter</c> (signature validation itself
/// is handled by the JWT bearer authentication handler registered in <c>Program</c>).
/// </summary>
public class TenantContextMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantContextMiddleware> _logger;

    public TenantContextMiddleware(RequestDelegate next, ILogger<TenantContextMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context,
                                  ICompanyBranchContext tenantContext,
                                  IAuthRuntimeValidationService runtimeValidation)
    {
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            var user = context.User;

            var userNoClaim = user.FindFirst("userNo")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var companyNoClaim = user.FindFirst("companyNo")?.Value;
            var branchNoClaim = user.FindFirst("branchNo")?.Value;
            var userIdClaim = user.FindFirst("userId")?.Value;
            var userNameClaim = user.FindFirst("userName")?.Value ?? user.FindFirst(ClaimTypes.Name)?.Value;
            var scopeClaim = user.FindFirst("accessScope")?.Value;
            var sessionNoClaim = user.FindFirst("sessionNo")?.Value;
            var tokenVersionClaim = user.FindFirst("tokenVersion")?.Value;

            long.TryParse(userNoClaim, out var userNo);
            long.TryParse(companyNoClaim, out var companyNo);
            long.TryParse(branchNoClaim, out var branchNo);
            int.TryParse(scopeClaim, out var accessScope);

            tenantContext.SetContext(
                companyNo > 0 ? companyNo : null,
                branchNo > 0 ? branchNo : null,
                userNo > 0 ? userNo : null,
                userIdClaim,
                userNameClaim,
                accessScope > 0 ? accessScope : 1);

            if (long.TryParse(sessionNoClaim, out var sessionNo) && sessionNo > 0)
            {
                tenantContext.SetSessionNo(sessionNo);
            }

            // Per-form permissions ride in the token → per-request RBAC is an in-memory bitmask
            // lookup with zero DB queries. Absent (legacy token) → null → DB fallback.
            tenantContext.SetPermBits(ReadPermBits(user));

            // Runtime guard: user still active, token version current, branch membership alive.
            long? tokenVersion = long.TryParse(tokenVersionClaim, out var tv) ? tv : null;
            try
            {
                await runtimeValidation.ValidateJwtContextAsync(
                    userNo > 0 ? userNo : null,
                    companyNo > 0 ? companyNo : null,
                    branchNo > 0 ? branchNo : null,
                    tokenVersion,
                    context.RequestAborted);
            }
            catch (ValidationException ex)
            {
                // Matches the Java filter: the request continues unauthenticated rather than
                // erroring, and downstream authorization denies it.
                _logger.LogWarning("JWT context rejected for {Method} {Path}: {Reason}",
                    context.Request.Method, context.Request.Path, ex.Message);

                tenantContext.Clear();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }
        }

        await _next(context);
    }

    /// <summary>
    /// Reads the <c>perms</c> claim (formId → bitmask). The JWT handler surfaces object claims as
    /// a JSON string, so it is parsed back here.
    /// </summary>
    private static IReadOnlyDictionary<string, int>? ReadPermBits(ClaimsPrincipal user)
    {
        var raw = user.FindFirst("perms")?.Value;
        if (string.IsNullOrWhiteSpace(raw)) return null;

        try
        {
            using var document = JsonDocument.Parse(raw);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return null;

            var perms = new Dictionary<string, int>();
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetInt32(out var value))
                {
                    perms[property.Name] = value;
                }
            }

            return perms;
        }
        catch (JsonException)
        {
            return null; // malformed claim → fall back to the DB resolver
        }
    }
}
