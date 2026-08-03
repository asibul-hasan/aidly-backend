using AidlyErp.Shared.Contracts;

namespace AidlyErp.Api.Middleware;

/// <summary>
/// Delivers queued real-time notifications once the request — and therefore its transaction —
/// has finished successfully.
///
/// <para>This is the "after commit" half of the dispatch contract. The dispatcher stages the
/// database row inside the caller's unit of work and queues the push here; only when the pipeline
/// returns without throwing, and with a success status, is anything sent. A rolled-back request
/// never announces the notification it failed to write.</para>
///
/// <para>Delivery failures are logged and swallowed <b>deliberately</b>: the notification is
/// already durable and the client will pick it up on its next poll, so a transient hub problem
/// must not turn a successful business operation into a 500.</para>
/// </summary>
public class NotificationRealtimeFlushMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<NotificationRealtimeFlushMiddleware> _logger;

    public NotificationRealtimeFlushMiddleware(RequestDelegate next,
                                               ILogger<NotificationRealtimeFlushMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context,
                                  INotificationRealtimeQueue queue,
                                  INotificationRealtimePublisher publisher)
    {
        await _next(context);

        // 4xx/5xx means the unit of work rolled back — the rows were never committed.
        if (context.Response.StatusCode >= 400) return;

        var pending = queue.Drain();
        if (pending.Count == 0) return;

        foreach (var push in pending)
        {
            try
            {
                await publisher.PublishToUserAsync(push.TargetUserNo, push.Message, context.RequestAborted);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Real-time notification push failed for userNo={UserNo}; the notification is stored and will appear on the next poll.",
                    push.TargetUserNo);
            }
        }
    }
}
