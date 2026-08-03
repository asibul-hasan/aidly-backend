using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace AidlyErp.Api.Hubs;

/// <summary>
/// Push channel for the notification bell. Clients connect to <c>/hubs/notification</c> and
/// handle the <c>ReceiveNotification</c> message.
///
/// <para>There is no manual group bookkeeping: <see cref="UserNoUserIdProvider"/> maps each
/// connection to its <c>userNo</c>, so SignalR maintains the per-user connection set itself and
/// the publisher addresses recipients with <c>Clients.User(userNo)</c>. The hand-rolled
/// <c>User_{id}</c> groups this replaces were keyed on the user NAME (from <c>sub</c>) while the
/// publisher sent to the numeric user id — which is why nothing was ever delivered.</para>
///
/// <para>The hub exposes no callable methods; it is push-only. Clients read and mutate
/// notifications over the REST endpoints, which carry the same authorization as every other API.</para>
/// </summary>
[Authorize]
public class NotificationHub : Hub
{
}
