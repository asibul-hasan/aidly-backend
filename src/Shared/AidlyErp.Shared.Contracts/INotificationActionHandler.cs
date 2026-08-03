namespace AidlyErp.Shared.Contracts;

/// <summary>
/// One button a notification offers its recipient.
///
/// <para>Actions travel WITH the notification instead of being inferred by the UI. The inbox is a
/// single ERP-wide surface — leave, purchase orders, stock adjustments, expense reports — so a
/// component that decided which buttons to show by testing <c>document_type == "HRM_1301"</c>
/// would need editing for every new document type ever added. The module that raises the
/// notification is the only thing that knows what can be done about it, so it says so.</para>
/// </summary>
/// <param name="Key">Stable identifier the module's handler switches on, e.g. <c>approve</c>.</param>
/// <param name="Label">What the button reads.</param>
/// <param name="Style">Visual intent: <c>primary</c>, <c>success</c>, <c>danger</c>, <c>neutral</c>.</param>
public sealed record NotificationAction(string Key, string Label, string Style)
{
    public static NotificationAction Approve(string label = "Approve") => new("approve", label, "primary");
    public static NotificationAction Reject(string label = "Reject") => new("reject", label, "danger");
    public static NotificationAction Accept(string label = "Accept") => new("accept", label, "success");
    public static NotificationAction Decline(string label = "Decline") => new("decline", label, "danger");
}

/// <summary>
/// A module's hook for carrying out an action a notification offered.
///
/// <para>The notification controller owns no business logic: it authenticates, resolves the
/// notification, and hands the action to whichever module claims the document type. Adding a new
/// actionable document means registering a handler — the inbox, its endpoint and its UI stay
/// untouched.</para>
/// </summary>
public interface INotificationActionHandler
{
    /// <summary>True when this handler owns <paramref name="documentType"/>.</summary>
    bool CanHandle(string documentType);

    /// <summary>
    /// The actions this document type offers its recipient.
    ///
    /// <para>Asked when the inbox is READ, never stored on the notification. Actions written into
    /// the payload at dispatch time become a snapshot: a notification raised before an action
    /// existed would show no buttons forever, and one raised before an action was removed would
    /// keep offering it. Deriving them per read means the buttons always match today's code.</para>
    ///
    /// <para>Deliberately synchronous and stateless — it runs for every row on the page, so it must
    /// not query. State-dependent hiding (already answered, document cancelled) is handled by the
    /// caller from the notification's own status.</para>
    /// </summary>
    IReadOnlyList<NotificationAction> GetAvailableActions(string documentType);

    /// <summary>
    /// Performs <paramref name="actionKey"/> against the document. Throw a validation/not-found
    /// exception to reject it — the caller surfaces that to the user and leaves the notification
    /// unacted.
    /// </summary>
    Task HandleAsync(string documentType, long documentPk, string actionKey, string? remarks,
                     CancellationToken cancellationToken = default);
}
