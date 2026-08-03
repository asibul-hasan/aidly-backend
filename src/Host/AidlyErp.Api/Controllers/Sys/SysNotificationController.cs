using System.Text.Json.Serialization;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Sys.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace AidlyErp.Api.Controllers.Sys;

/// <summary>Body of a notification action: which advertised action, plus optional remarks.</summary>
public sealed class NotificationActionRequest
{
    [JsonPropertyName("action_key")]
    public string? ActionKey { get; set; }

    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }
}

/// <summary>
/// The signed-in user's notification inbox.
///
/// <para>Every action derives the recipient from the auth context and never from the request, so
/// a user can only ever read or mutate their own notifications.</para>
///
/// <para>These actions deliberately do NOT swallow exceptions. The previous version wrapped each
/// one in <c>catch { return Ok(empty); }</c>, which made a database outage indistinguishable from
/// "you have no notifications" and reported a failed mark-as-read as success. Failures now
/// surface through the global exception handler like every other endpoint.</para>
/// </summary>
[Route("api/v1/sys/notifications")]
public class SysNotificationController : ApiControllerBase
{
    private readonly SysNotificationService _notificationService;
    private readonly ICompanyBranchContext _ctx;

    public SysNotificationController(SysNotificationService notificationService, ICompanyBranchContext ctx)
    {
        _notificationService = notificationService;
        _ctx = ctx;
    }

    /// <summary>One page of the caller's notifications, newest first, plus totals for the pager.</summary>
    [HttpGet("user")]
    public async Task<IActionResult> GetUserNotifications([FromQuery] bool unreadOnly = false,
                                                          [FromQuery] int page = 0,
                                                          [FromQuery] int pageSize = 25,
                                                          [FromQuery] string? search = null,
                                                          CancellationToken ct = default)
    {
        var result = await _notificationService.GetUserNotificationsAsync(
            _ctx.CurrentUserNo(), unreadOnly, page, pageSize, search, ct);

        return OkResponse(result);
    }

    /// <summary>
    /// Performs one of the actions the notification advertised (its <c>payload.actions</c>).
    ///
    /// <para>Deliberately generic: the body carries an action KEY, never an endpoint or a document
    /// type supplied by the client. The server resolves the notification, confirms it belongs to
    /// the caller, and routes the key to the shared approval engine or the owning module. A new
    /// actionable document type needs a handler, not a new endpoint.</para>
    /// </summary>
    [HttpPost("{id:long}/action")]
    public async Task<IActionResult> ExecuteAction(long id, [FromBody] NotificationActionRequest request,
                                                   CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request?.ActionKey))
            return ErrorResponse(StatusCodes.Status400BadRequest, "action_key is required");

        var found = await _notificationService.ExecuteActionAsync(
            id, _ctx.CurrentUserNo(), request.ActionKey.Trim(), request.Remarks, ct);

        return found
            ? OkResponse(new { success = true }, "Action completed")
            : ErrorResponse(StatusCodes.Status404NotFound, "Notification not found");
    }

    /// <summary>Unread count only — what the bell badge polls, without fetching any rows.</summary>
    [HttpGet("user/unread-count")]
    public async Task<IActionResult> GetUnreadCount(CancellationToken ct = default)
    {
        var count = await _notificationService.GetUnreadCountAsync(_ctx.CurrentUserNo(), ct);
        return OkResponse(new { unread_count = count });
    }

    [HttpPost("{id:long}/read")]
    public async Task<IActionResult> MarkAsRead(long id, CancellationToken ct = default)
    {
        var found = await _notificationService.MarkAsReadAsync(id, _ctx.CurrentUserNo(), ct);

        // 404 rather than 403 — a notification belonging to someone else must not be
        // distinguishable from one that does not exist.
        return found
            ? OkResponse(new { success = true }, "Notification marked as read")
            : ErrorResponse(StatusCodes.Status404NotFound, "Notification not found");
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken ct = default)
    {
        var updated = await _notificationService.MarkAllAsReadAsync(_ctx.CurrentUserNo(), ct);
        return OkResponse(new { updated }, $"{updated} notification(s) marked as read");
    }
}
