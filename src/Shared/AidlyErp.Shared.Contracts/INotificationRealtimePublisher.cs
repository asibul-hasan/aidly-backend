namespace AidlyErp.Shared.Contracts;

public interface INotificationRealtimePublisher
{
    Task PublishToUserAsync(long targetUserNo, object message, CancellationToken cancellationToken = default);
}
