using AidlyErp.Shared.Contracts;
using AidlyErp.Shared.Core.Exceptions;
using Microsoft.Extensions.DependencyInjection;

namespace AidlyErp.Hrm.Application.Services;

/// <summary>
/// Carries out HRM-specific notification actions.
///
/// <para>Only actions the shared approval engine does NOT own belong here. Approve/Reject on an
/// approval-backed notification already routes through the engine for every module, so this
/// handler exists for the document-specific cases — currently a reliever confirming cover, which
/// writes <c>reliever_status</c> and has nothing to do with approving the leave.</para>
///
/// <para><b>The leave service is resolved lazily, never injected.</b> Every handler is constructed
/// whenever the inbox is read, and taking <c>IHrm1301Service</c> in the constructor closed a
/// dependency cycle:</para>
/// <code>
/// SysNotificationService → INotificationActionHandler → IHrm1301Service
///                        → IApprovalService → INotificationDispatcher → SysNotificationService
/// </code>
/// <para>The dispatcher is registered through a factory lambda, so <c>ValidateOnBuild</c> cannot
/// see through it — the cycle did not fail at startup, it recursed on the first inbox request and
/// hung it. Resolving on demand makes construction free and keeps the graph acyclic; it also stops
/// every read from building the entire HRM service graph just to list two button labels.</para>
/// </summary>
public class HrmNotificationActionHandler : INotificationActionHandler
{
    private const short RelieverAccepted = 2;
    private const short RelieverDeclined = 1;

    private readonly IServiceProvider _services;

    public HrmNotificationActionHandler(IServiceProvider services) => _services = services;

    public bool CanHandle(string documentType) => documentType == Hrm1301Service.DocType;

    /// <summary>
    /// A leave notification offers the reliever Accept / Decline. Approve / Reject are NOT here —
    /// those belong to the shared approval engine, and the inbox adds them itself whenever the
    /// notification is backed by a pending approval request.
    /// </summary>
    public IReadOnlyList<NotificationAction> GetAvailableActions(string documentType) =>
        documentType == Hrm1301Service.DocType
            ? [NotificationAction.Accept(), NotificationAction.Decline()]
            : [];

    public async Task HandleAsync(string documentType, long documentPk, string actionKey, string? remarks,
                                  CancellationToken cancellationToken = default)
    {
        var status = actionKey switch
        {
            "accept" => RelieverAccepted,
            "decline" => RelieverDeclined,
            _ => throw new ValidationException($"'{actionKey}' is not a valid action for a leave notification.")
        };

        // Resolved here, not in the constructor — see the class remarks.
        var leave = _services.GetRequiredService<IHrm1301Service>();

        await leave.UpdateRelieverStatusAsync(documentPk, status, remarks, cancellationToken);
    }
}
