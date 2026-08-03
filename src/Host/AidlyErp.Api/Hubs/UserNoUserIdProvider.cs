using Microsoft.AspNetCore.SignalR;

namespace AidlyErp.Api.Hubs;

/// <summary>
/// Identifies a SignalR connection by <c>userNo</c>.
///
/// <para><b>Why this exists.</b> SignalR's default provider reads
/// <c>ClaimTypes.NameIdentifier</c>, which for these tokens maps from <c>sub</c> — and
/// <c>JwtTokenService</c> sets <c>sub</c> to the user NAME. The hub therefore joined group
/// <c>User_asibul</c> while the publisher sent to <c>User_42</c>, so no push could ever be
/// delivered. Both sides now agree on the numeric user id.</para>
/// </summary>
public class UserNoUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        var userNo = connection.User?.FindFirst("userNo")?.Value;
        return string.IsNullOrWhiteSpace(userNo) ? null : userNo;
    }
}
