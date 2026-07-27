using AidlyErp.Application.Common.Interfaces;
using AidlyErp.Domain.Audit;

namespace AidlyErp.Application.Common.Services;

/// <summary>
/// Async request-log writer — port of Java <c>core.audit.service.SysLogService</c>.
/// Runs in its own scope so the log row survives rollback of the failed request.
/// Never throws — logging failures must not propagate to the caller.
/// </summary>
public class SysLogService : ISysLogService
{
    private readonly IApplicationDbContextFactory _dbFactory;

    public SysLogService(IApplicationDbContextFactory dbFactory) => _dbFactory = dbFactory;

    public async Task LogRequestAsync(
        string httpMethod,
        string requestUri,
        int responseStatus,
        long durationMs,
        string? ipAddress,
        string? userAgent,
        string? requestBody,
        string? errorMessage,
        long? companyNo,
        long? branchNo,
        long? userNo,
        long? sessionNo,
        CancellationToken ct = default)
    {
        try
        {
            await using var scope = _dbFactory.CreateScope();
            var db = scope.Context;
            var log = new SysLog
            {
                CompanyNo = companyNo,
                BranchNo = branchNo,
                UserNo = userNo,
                SessionNo = sessionNo,
                HttpMethod = httpMethod,
                RequestUri = requestUri,
                ResponseStatus = responseStatus,
                DurationMs = durationMs,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                RequestBody = requestBody,
                ErrorMessage = errorMessage,
                RequestAt = DateTime.UtcNow
            };
            db.SysLogs.Add(log);
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            // Never let logging failures propagate — swallow silently.
        }
    }
}
