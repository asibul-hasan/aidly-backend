namespace AidlyErp.Application.Common.Interfaces;

/// <summary>
/// Async request-log writer — port of Java <c>core.audit.service.SysLogService</c>.
/// Writes HTTP request traffic to <c>sys_log</c> without blocking the response pipeline.
/// </summary>
public interface ISysLogService
{
    Task LogRequestAsync(
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
        CancellationToken ct = default);
}
