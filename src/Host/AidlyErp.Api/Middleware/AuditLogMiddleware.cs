using System.Diagnostics;
using System.Text;
using AidlyErp.Shared.Core.Abstractions;
using AidlyErp.Shared.Contracts;
using AidlyErp.Sys.Contracts;
using AidlyErp.Sys.Application.Interfaces;
using AidlyErp.Hrm.Application.Interfaces;
using AidlyErp.Shared.Core.Security;

namespace AidlyErp.Api.Middleware;

/// <summary>
/// HTTP request logging middleware — port of Java <c>core.audit.interceptor.AuditLogInterceptor</c>.
/// Logs non-GET requests to <c>sys_log</c> with method, URI, status, latency, and client IP.
/// Runs asynchronously so it never blocks the response pipeline.
/// </summary>
public class AuditLogMiddleware
{
    private readonly RequestDelegate _next;

    public AuditLogMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        // Only log mutating requests (POST, PUT, DELETE, PATCH) — skip GET/HEAD/OPTIONS.
        if (context.Request.Method is "GET" or "HEAD" or "OPTIONS")
        {
            await _next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        string? requestBody = null;

        // Capture request body for POST/PUT/PATCH (not for DELETE).
        if (context.Request.Method is "POST" or "PUT" or "PATCH")
        {
            context.Request.EnableBuffering();
            using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
            requestBody = await reader.ReadToEndAsync(context.RequestAborted);
            context.Request.Body.Position = 0;

            // Truncate large bodies to avoid bloating the log table.
            if (requestBody.Length > 4000)
                requestBody = requestBody[..4000] + "...(truncated)";
        }

        string? errorMessage = null;
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            throw;
        }
        finally
        {
            stopwatch.Stop();

            // Fire-and-forget the log write — use a background task so the response
            // is not delayed by the DB insert.
            _ = Task.Run(async () =>
            {
                try
                {
                    var logService = context.RequestServices.GetRequiredService<ISysLogService>();
                    var tenantCtx = context.RequestServices.GetRequiredService<ICompanyBranchContext>();

                    await logService.LogRequestAsync(
                        httpMethod: context.Request.Method,
                        requestUri: $"{context.Request.Path}{context.Request.QueryString}",
                        responseStatus: context.Response.StatusCode,
                        durationMs: stopwatch.ElapsedMilliseconds,
                        ipAddress: context.Connection.RemoteIpAddress?.ToString(),
                        userAgent: context.Request.Headers.UserAgent.ToString(),
                        requestBody: requestBody,
                        errorMessage: errorMessage,
                        companyNo: tenantCtx.CompanyNo,
                        branchNo: tenantCtx.BranchNo,
                        userNo: tenantCtx.UserNo,
                        sessionNo: tenantCtx.SessionNo,
                        ct: context.RequestAborted);
                }
                catch
                {
                    // Never let logging failures propagate.
                }
            }, context.RequestAborted);
        }
    }
}
