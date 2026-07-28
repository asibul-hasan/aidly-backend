namespace AidlyErp.Shared.Contracts;

/// <summary>
/// Async request-log writer — port of Java <c>core.audit.service.SysLogService</c>.
/// Writes HTTP request traffic to <c>sys_log</c> without blocking the response pipeline.
/// </summary>
public interface ISysLogService
{
    /// <summary>
    /// Mirrors Java <c>writeLog</c> exactly. User agent, request body and error message are
    /// deliberately absent — <c>sys_log</c> has no such columns.
    /// </summary>
    Task LogRequestAsync(
        string httpMethod,
        string requestUri,
        int responseStatus,
        long durationMs,
        string? ipAddress,
        long? companyNo,
        long? branchNo,
        long? userNo,
        long? sessionNo,
        CancellationToken ct = default);
}
