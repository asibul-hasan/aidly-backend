using AidlyErp.Shared.Contracts;

namespace AidlyErp.Api.Services;

/// <summary>
/// Request-scoped, in-memory queue of pending real-time pushes. Registered scoped, so each
/// request gets its own buffer and a rolled-back request simply drops its own.
/// </summary>
public class NotificationRealtimeQueue : INotificationRealtimeQueue
{
    private readonly List<PendingRealtimePush> _pending = [];
    private readonly Lock _gate = new();

    public void Enqueue(long targetUserNo, object message)
    {
        lock (_gate)
        {
            _pending.Add(new PendingRealtimePush(targetUserNo, message));
        }
    }

    public IReadOnlyList<PendingRealtimePush> Drain()
    {
        lock (_gate)
        {
            if (_pending.Count == 0) return [];

            var drained = _pending.ToArray();
            _pending.Clear();
            return drained;
        }
    }
}
