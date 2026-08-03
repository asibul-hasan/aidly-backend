namespace AidlyErp.Shared.Contracts;

/// <summary>Lifecycle state of a notification row (<c>sys_notification.status</c>).</summary>
public static class NotificationStatus
{
    public const short Unread = 0;
    public const short Read = 1;
    public const short ActionTaken = 2;
}

/// <summary>
/// Well-known trigger events, so a dispatch site and a <c>sys_notification_template</c> row
/// agree on one vocabulary instead of loose strings.
/// </summary>
public static class NotificationEvents
{
    public const string ApprovalRequested = "APPROVAL_REQUESTED";
    public const string ApprovalStepAdvanced = "APPROVAL_STEP_ADVANCED";
    public const string ApprovalApproved = "APPROVAL_APPROVED";
    public const string ApprovalRejected = "APPROVAL_REJECTED";
}

public record NotificationDispatchPayload(
    long CompanyNo,
    long? BranchNo,
    long TargetUserNo,
    long? TargetEmployeeNo,
    long? SenderUserNo,
    long? MenuNo,
    string? FormId,
    string? DocumentType,
    long? DocumentPk,
    long? ApprovalRequestNo,
    string Title,
    string Message,
    object? PayloadData
);

public interface INotificationDispatcher
{
    /// <summary>
    /// Stages one notification.
    ///
    /// <para><b>Enlists in the caller's transaction.</b> The row is added to the change tracker
    /// and committed by the surrounding unit of work — it is not saved independently. A
    /// notification therefore cannot survive a rolled-back business operation, which is the whole
    /// point: "your leave was approved" must not outlive an approval that failed to commit.</para>
    ///
    /// <para>The real-time push is queued, not sent, and flushes only after the request's
    /// transaction commits (see <see cref="INotificationRealtimeQueue"/>). Pushing inside the
    /// transaction would announce a row that is not visible yet — or never commits at all.</para>
    /// </summary>
    Task DispatchAsync(NotificationDispatchPayload payload, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stages notifications for several recipients in one call, resolving shared lookups once.
    /// Duplicate target users are collapsed.
    /// </summary>
    Task DispatchManyAsync(IEnumerable<NotificationDispatchPayload> payloads,
                           CancellationToken cancellationToken = default);

    /// <summary>
    /// Stages AND commits, for callers that are not running inside a SYS unit of work.
    ///
    /// <para>Notifications live in the SYS context. A module that owns a different context — HRM
    /// saving a leave, for example — has no SYS transaction to enlist in, so
    /// <see cref="DispatchAsync"/> would leave the row sitting in the change tracker with nothing
    /// to save it. This method commits it explicitly.</para>
    ///
    /// <para><b>Not atomic with the caller's own write</b>, and it cannot be: the two contexts are
    /// separate connections. Use it only where the notification failing independently is
    /// acceptable — announcing something, never recording it. Inside SYS, prefer
    /// <see cref="DispatchAsync"/> so the notification commits with the change that caused it.</para>
    /// </summary>
    Task DispatchAndSaveAsync(NotificationDispatchPayload payload, CancellationToken cancellationToken = default);
}
