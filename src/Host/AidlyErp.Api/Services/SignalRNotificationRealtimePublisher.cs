using AidlyErp.Api.Hubs;
using AidlyErp.Shared.Contracts;
using Microsoft.AspNetCore.SignalR;

namespace AidlyErp.Api.Services;

public class SignalRNotificationRealtimePublisher : INotificationRealtimePublisher
{
    private readonly IHubContext<NotificationHub> _hubContext;

    public SignalRNotificationRealtimePublisher(IHubContext<NotificationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    /// <summary>
    /// Sends to every live connection the user has open, across tabs and devices.
    /// <c>Clients.User</c> resolves through <c>UserNoUserIdProvider</c>, so the identifier here is
    /// the same <c>userNo</c> the connection was registered under. A user with no open connection
    /// is a no-op — the notification is already stored and appears on their next poll.
    /// </summary>
    public async Task PublishToUserAsync(long targetUserNo, object message, CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients.User(targetUserNo.ToString())
            .SendAsync("ReceiveNotification", message, cancellationToken);
    }
}
