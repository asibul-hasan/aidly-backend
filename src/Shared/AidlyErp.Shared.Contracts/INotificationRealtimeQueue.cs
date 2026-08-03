namespace AidlyErp.Shared.Contracts;

/// <summary>One queued real-time push, held until the request's transaction has committed.</summary>
public sealed record PendingRealtimePush(long TargetUserNo, object Message);

/// <summary>
/// Request-scoped buffer of real-time pushes.
///
/// <para>Exists so a notification is announced <b>after</b> it is durable. The dispatcher stages
/// the database row inside the caller's transaction and enqueues the push here; the queue is
/// drained once, at the end of the request, by <c>NotificationRealtimeFlushMiddleware</c>. If the
/// transaction rolls back, the queue is discarded with the request and nothing was ever
/// announced.</para>
/// </summary>
public interface INotificationRealtimeQueue
{
    void Enqueue(long targetUserNo, object message);

    /// <summary>Removes and returns everything queued so far. Draining twice yields nothing the second time.</summary>
    IReadOnlyList<PendingRealtimePush> Drain();
}
