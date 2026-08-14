using System.Text.Json;
using AidlyErp.Shared.Contracts;
using AidlyErp.Shared.Core.Abstractions;
using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Sys.Contracts;
using AidlyErp.Sys.Application.Interfaces;
using AidlyErp.Sys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AidlyErp.Sys.Application.Services;

/// <summary>
/// The notification store: stages rows inside the caller's transaction, and reads a user's
/// inbox scoped to the company they are signed into.
/// </summary>
public class SysNotificationService : INotificationDispatcher
{
    /// <summary>Inbox page cap. The bell is a recent-activity list, not an archive.</summary>
    public const int MaxPageSize = 100;

    /// <summary><c>sys_approval_request.status</c> = 1. Only a pending request can still be acted on.</summary>
    private const short ApprovalPending = 1;

    private readonly ISysDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    private readonly INotificationRealtimeQueue _realtimeQueue;
    private readonly IEnumerable<INotificationActionHandler> _actionHandlers;
    private readonly IServiceProvider? _services;
    private readonly ILogger<SysNotificationService> _logger;

    /// <summary>
    /// <c>IApprovalService</c> is resolved on demand rather than injected, because injecting it
    /// closes a dependency cycle: <c>ApprovalService</c> takes <c>INotificationDispatcher</c>,
    /// which IS this service. The dispatcher is registered through a factory lambda, so
    /// <c>ValidateOnBuild</c> cannot see the cycle — it did not fail at startup, it recursed on
    /// the first request and hung it. Only <see cref="ExecuteActionAsync"/> needs the engine, and
    /// only when the action is an approval decision.
    /// </summary>
    public SysNotificationService(ISysDbContext db,
                                  ICompanyBranchContext ctx,
                                  INotificationRealtimeQueue realtimeQueue,
                                  IEnumerable<INotificationActionHandler> actionHandlers,
                                  ILogger<SysNotificationService> logger,
                                  IServiceProvider? services = null)
    {
        _db = db;
        _ctx = ctx;
        _realtimeQueue = realtimeQueue;
        _actionHandlers = actionHandlers;
        _services = services;
        _logger = logger;
    }

    // ── Dispatch ──────────────────────────────────────────────────────────────

    public Task DispatchAsync(NotificationDispatchPayload payload, CancellationToken cancellationToken = default) =>
        DispatchManyAsync([payload], cancellationToken);

    /// <summary>
    /// Stages one row per distinct target user and queues each push for after-commit delivery.
    ///
    /// <para>No <c>SaveChanges</c> here: the rows ride the caller's unit of work. Exceptions are
    /// NOT swallowed — a notification that silently fails to save is a support ticket nobody can
    /// diagnose, and the caller's transaction should fail with it.</para>
    /// </summary>
    public async Task DispatchManyAsync(IEnumerable<NotificationDispatchPayload> payloads,
                                        CancellationToken cancellationToken = default)
    {
        // Collapse duplicates: the same approver can appear on several steps of one request.
        var distinct = payloads
            .GroupBy(p => p.TargetUserNo)
            .Select(g => g.First())
            .ToList();

        if (distinct.Count == 0) return;

        var entities = distinct.Select(p => new SysNotification
        {
            CompanyNo = p.CompanyNo,
            BranchNo = p.BranchNo,
            TargetUserNo = p.TargetUserNo,
            TargetEmployeeNo = p.TargetEmployeeNo,
            SenderUserNo = p.SenderUserNo,
            MenuNo = p.MenuNo,
            FormId = p.FormId,
            DocumentType = p.DocumentType,
            DocumentPk = p.DocumentPk,
            ApprovalRequestNo = p.ApprovalRequestNo,
            Title = p.Title,
            Message = p.Message,
            PayloadJson = p.PayloadData != null ? JsonSerializer.Serialize(p.PayloadData) : null,
            Status = NotificationStatus.Unread
        }).ToList();

        _db.Notifications.AddRange(entities);

        // Resolve display names once for the whole batch, not once per row.
        var formNames = await ResolveFormNamesAsync(entities, cancellationToken);

        foreach (var entity in entities)
        {
            _realtimeQueue.Enqueue(entity.TargetUserNo, ToDto(entity, formNames));
        }

        _logger.LogInformation(
            "Staged {Count} notification(s) for documentType={DocumentType}, pk={Pk}",
            entities.Count, distinct[0].DocumentType, distinct[0].DocumentPk);
    }

    /// <inheritdoc />
    public async Task DispatchAndSaveAsync(NotificationDispatchPayload payload,
                                           CancellationToken cancellationToken = default)
    {
        await DispatchManyAsync([payload], cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    // ── Reads ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// One page of the caller's inbox, newest first.
    ///
    /// <para><b>Company-scoped explicitly.</b> <c>sys_notification</c> is
    /// <c>ICompanyScopedEntity</c>, and the global tenant filter only covers
    /// <c>IMultiTenantEntity</c> — so without this predicate a user who belongs to two companies
    /// would see both companies' notifications in one bell. It is scoped by company and NOT by
    /// branch on purpose: a notification is addressed to a person, and switching branch must not
    /// hide an approval waiting on them.</para>
    /// </summary>
    public async Task<NotificationPageDto> GetUserNotificationsAsync(long userNo,
                                                                     bool unreadOnly = false,
                                                                     int page = 0,
                                                                     int pageSize = 25,
                                                                     string? search = null,
                                                                     CancellationToken cancellationToken = default)
    {
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        page = Math.Max(page, 0);

        var query = BaseInboxQuery(userNo);

        if (unreadOnly) query = query.Where(n => n.Status == NotificationStatus.Unread);

        // Search runs in SQL, not over the loaded page — otherwise "search" would silently mean
        // "search the 10 rows you happen to be looking at", which is worse than no search at all.
        if (!string.IsNullOrWhiteSpace(search))
        {
            // Provider-agnostic case-insensitive contains — this project's Application layer does
            // not reference the Npgsql provider, so EF.Functions.ILike is not available here.
            var term = search.Trim().ToLower();
            query = query.Where(n => n.Title.ToLower().Contains(term)
                                     || n.Message.ToLower().Contains(term));
        }

        // Total for the filtered set, so the pager can say "1 to 10 of 24" honestly.
        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(n => n.NotificationNo)
            .Skip(page * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // The badge counts every unread notification — unfiltered, and independent of this page.
        var unreadCount = await BaseInboxQuery(userNo)
            .CountAsync(n => n.Status == NotificationStatus.Unread, cancellationToken);

        var formNames = await ResolveFormNamesAsync(items, cancellationToken);
        var actions = await ResolveActionsAsync(items, cancellationToken);

        return new NotificationPageDto(
            items.Select(n => ToDto(n, formNames, actions)).ToList(),
            unreadCount,
            total,
            page,
            pageSize,
            (page + 1) * pageSize < total);
    }

    public Task<int> GetUnreadCountAsync(long userNo, CancellationToken cancellationToken = default) =>
        BaseInboxQuery(userNo).CountAsync(n => n.Status == NotificationStatus.Unread, cancellationToken);

    // ── State changes ─────────────────────────────────────────────────────────

    /// <summary>Marks one notification read. Returns false when it does not belong to the caller.</summary>
    public Task<bool> MarkAsReadAsync(long notificationNo, long userNo, CancellationToken cancellationToken = default) =>
        SetStatusAsync(notificationNo, userNo, NotificationStatus.Read, cancellationToken);

    /// <summary>Marks one notification as acted upon (the user approved/rejected from the bell).</summary>
    public Task<bool> MarkActionTakenAsync(long notificationNo, long userNo, CancellationToken cancellationToken = default) =>
        SetStatusAsync(notificationNo, userNo, NotificationStatus.ActionTaken, cancellationToken);

    /// <summary>Marks every unread notification read. Returns how many rows changed.</summary>
    public async Task<int> MarkAllAsReadAsync(long userNo, CancellationToken cancellationToken = default)
    {
        var unread = await TrackedInboxQuery(userNo)
            .Where(n => n.Status == NotificationStatus.Unread)
            .ToListAsync(cancellationToken);

        if (unread.Count == 0) return 0;

        var now = DateTime.UtcNow;
        foreach (var n in unread)
        {
            n.Status = NotificationStatus.Read;
            n.ReadAt ??= now;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return unread.Count;
    }

    /// <summary>
    /// Carries out an action the notification offered, then marks it acted on.
    ///
    /// <para>Two routes, no document-specific knowledge in between:</para>
    /// <list type="number">
    ///   <item><b>approve / reject on an approval-backed notification</b> → the shared approval
    ///   engine. This works for every approvable document in the ERP because the engine, not this
    ///   service, knows the workflow.</item>
    ///   <item><b>anything else</b> → the module that claims the document type, via
    ///   <see cref="INotificationActionHandler"/>.</item>
    /// </list>
    ///
    /// <para>The notification is only marked acted on once the action succeeds — a rejected
    /// action leaves it actionable rather than silently consuming it.</para>
    /// </summary>
    public async Task<bool> ExecuteActionAsync(long notificationNo, long userNo, string actionKey,
                                                string? remarks, CancellationToken cancellationToken = default)
    {
        var notification = await BaseInboxQuery(userNo)
            .FirstOrDefaultAsync(n => n.NotificationNo == notificationNo, cancellationToken);

        if (notification == null) return false;

        var isApprovalDecision = notification.ApprovalRequestNo is > 0
                                 && actionKey is "approve" or "reject";

        if (isApprovalDecision)
        {
            // Resolved here, not injected — see the constructor remarks about the cycle.
            var approvals = _services?.GetService(typeof(IApprovalService)) as IApprovalService
                ?? throw new InvalidOperationException("Approval engine is not available.");

            await approvals.ActAsync(notification.ApprovalRequestNo!.Value,
                                     actionKey == "approve", remarks, cancellationToken);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(notification.DocumentType) || notification.DocumentPk == null)
                throw new ValidationException("This notification has no document to act on.");

            var handler = _actionHandlers.FirstOrDefault(h => h.CanHandle(notification.DocumentType));

            if (handler == null)
                throw new ValidationException(
                    $"No module handles '{actionKey}' for document type {notification.DocumentType}.");

            await handler.HandleAsync(notification.DocumentType, notification.DocumentPk.Value,
                                      actionKey, remarks, cancellationToken);
        }

        await SetStatusAsync(notificationNo, userNo, NotificationStatus.ActionTaken, cancellationToken);
        return true;
    }

    private async Task<bool> SetStatusAsync(long notificationNo, long userNo, short status,
                                            CancellationToken cancellationToken)
    {
        // The user predicate is the authorization check — never fetch by id alone, or any signed-in
        // user could mark somebody else's notification read by guessing a number.
        var notification = await TrackedInboxQuery(userNo)
            .FirstOrDefaultAsync(n => n.NotificationNo == notificationNo, cancellationToken);

        if (notification == null) return false;

        // Read never overwrites ActionTaken — acting is the stronger state.
        if (status == NotificationStatus.Read && notification.Status == NotificationStatus.ActionTaken) return true;

        if (notification.Status != status)
        {
            notification.Status = status;
            notification.ReadAt ??= DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// The caller's inbox, for READS only — <c>AsNoTracking</c>.
    ///
    /// <para>Never load an entity through this and then mutate it: an untracked entity is invisible
    /// to the change tracker, so <c>SaveChanges</c> writes nothing and the update is silently lost.
    /// Use <see cref="TrackedInboxQuery"/> for anything that changes a row.</para>
    /// </summary>
    private IQueryable<SysNotification> BaseInboxQuery(long userNo) =>
        InboxPredicate(_db.Notifications.AsNoTracking(), userNo);

    /// <summary>The same inbox, tracked, for paths that update a notification.</summary>
    private IQueryable<SysNotification> TrackedInboxQuery(long userNo) =>
        InboxPredicate(_db.Notifications, userNo);

    /// <summary>
    /// Company-scoped explicitly: <c>sys_notification</c> is <c>ICompanyScopedEntity</c> and the
    /// global tenant filter only covers <c>IMultiTenantEntity</c>, so without this a user who
    /// belongs to two companies would see both companies' notifications in one bell.
    /// </summary>
    private IQueryable<SysNotification> InboxPredicate(IQueryable<SysNotification> source, long userNo)
    {
        var companyNo = _ctx.CompanyNo;

        return source.Where(n => n.TargetUserNo == userNo
                                 && n.IsDeleted == 0
                                 && (companyNo == null || n.CompanyNo == companyNo));
    }

    /// <summary>
    /// Menu number / form id → form name for a whole batch in ONE query.
    ///
    /// <para>This used to run per row inside a foreach — 50 notifications meant 50 extra round
    /// trips to a remote database every time the bell opened.</para>
    /// </summary>
    private async Task<Dictionary<string, string>> ResolveFormNamesAsync(IReadOnlyCollection<SysNotification> items,
                                                                          CancellationToken cancellationToken)
    {
        var menuNos = items.Where(n => n.MenuNo.HasValue).Select(n => n.MenuNo!.Value).Distinct().ToList();
        var formIds = items.Where(n => !n.MenuNo.HasValue && !string.IsNullOrEmpty(n.FormId))
            .Select(n => n.FormId!).Distinct().ToList();

        var names = new Dictionary<string, string>();
        if (menuNos.Count == 0 && formIds.Count == 0) return names;

        var menus = await _db.Menus
            .AsNoTracking()
            .Where(m => menuNos.Contains(m.MenuNo) || (m.FormId != null && formIds.Contains(m.FormId)))
            .Select(m => new { m.MenuNo, m.FormId, m.FormName })
            .ToListAsync(cancellationToken);

        foreach (var m in menus)
        {
            if (m.FormName == null) continue;
            names[MenuKey(m.MenuNo)] = m.FormName;
            if (!string.IsNullOrEmpty(m.FormId)) names.TryAdd(FormKey(m.FormId), m.FormName);
        }

        return names;
    }

    /// <summary>
    /// Works out which buttons each notification on the page should offer, RIGHT NOW.
    ///
    /// <para>Two sources, neither stored on the row:</para>
    /// <list type="bullet">
    ///   <item>an approval-backed notification whose request is still <b>pending</b> gets
    ///   Approve / Reject — which is why this works for every approvable document in the ERP
    ///   without the inbox knowing what any of them are;</item>
    ///   <item>anything else asks the module that owns the document type.</item>
    /// </list>
    ///
    /// <para>A notification already acted on offers nothing. The pending-approval check is one
    /// batched query for the whole page, not one per row.</para>
    /// </summary>
    private async Task<Dictionary<long, List<NotificationAction>>> ResolveActionsAsync(
        IReadOnlyCollection<SysNotification> items, CancellationToken cancellationToken)
    {
        var result = new Dictionary<long, List<NotificationAction>>();
        if (items.Count == 0) return result;

        var approvalNos = items
            .Where(n => n.Status != NotificationStatus.ActionTaken && n.ApprovalRequestNo is > 0)
            .Select(n => n.ApprovalRequestNo!.Value)
            .Distinct()
            .ToList();

        // Which of those approvals can still be acted on — one query for the page.
        var pending = approvalNos.Count == 0
            ? []
            : await _db.ApprovalRequests
                .AsNoTracking()
                .Where(r => approvalNos.Contains(r.ApprovalRequestNo) && r.Status == ApprovalPending)
                .Select(r => r.ApprovalRequestNo)
                .ToListAsync(cancellationToken);

        var pendingSet = pending.ToHashSet();

        foreach (var n in items)
        {
            // Acting is terminal: once answered, the buttons are gone.
            if (n.Status == NotificationStatus.ActionTaken)
            {
                result[n.NotificationNo] = [];
                continue;
            }

            if (n.ApprovalRequestNo is > 0 && pendingSet.Contains(n.ApprovalRequestNo.Value))
            {
                result[n.NotificationNo] = [NotificationAction.Approve(), NotificationAction.Reject()];
                continue;
            }

            var handler = string.IsNullOrWhiteSpace(n.DocumentType)
                ? null
                : _actionHandlers.FirstOrDefault(h => h.CanHandle(n.DocumentType));

            result[n.NotificationNo] = handler == null
                ? []
                : handler.GetAvailableActions(n.DocumentType!).ToList();
        }

        return result;
    }

    private static string MenuKey(long menuNo) => "m:" + menuNo;
    private static string FormKey(string formId) => "f:" + formId;

    private static NotificationDto ToDto(SysNotification n,
                                         IReadOnlyDictionary<string, string> formNames,
                                         IReadOnlyDictionary<long, List<NotificationAction>>? actions = null)
    {
        string? formName = null;

        if (n.MenuNo.HasValue) formNames.TryGetValue(MenuKey(n.MenuNo.Value), out formName);
        if (formName == null && !string.IsNullOrEmpty(n.FormId)) formNames.TryGetValue(FormKey(n.FormId), out formName);

        var available = actions != null && actions.TryGetValue(n.NotificationNo, out var a)
            ? (IReadOnlyList<NotificationAction>)a
            : [];

        return new NotificationDto(
            n.NotificationNo,
            n.TargetUserNo,
            n.MenuNo,
            n.FormId,
            formName ?? n.FormId ?? "System",
            n.DocumentType,
            n.DocumentPk,
            n.ApprovalRequestNo,
            n.Title,
            n.Message,
            n.PayloadJson,
            n.Status,
            available,
            n.CreatedAt ?? DateTime.UtcNow
        );
    }
}

public record NotificationDto(
    long NotificationNo,
    long TargetUserNo,
    long? MenuNo,
    string? FormId,
    string FormName,
    string? DocumentType,
    long? DocumentPk,
    long? ApprovalRequestNo,
    string Title,
    string Message,
    string? PayloadJson,
    short Status,
    /// <summary>Buttons available RIGHT NOW — derived per read, never stored on the row.</summary>
    IReadOnlyList<NotificationAction> Actions,
    DateTime CreatedAt
);

/// <summary>
/// One page of the bell. <paramref name="Total"/> counts the filtered set so the pager can say
/// "1 to 10 of 24"; <paramref name="UnreadCount"/> is the unfiltered badge count.
/// </summary>
public record NotificationPageDto(
    List<NotificationDto> Items,
    int UnreadCount,
    int Total,
    int Page,
    int PageSize,
    bool HasMore
);
