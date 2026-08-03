using AidlyErp.Sys.Contracts;
using System.Text.Json.Serialization;

namespace AidlyErp.Sys.Contracts;

/// <summary>
/// Result of raising an approval. <c>AutoApproved</c> means no workflow was configured, so the
/// document should proceed immediately; otherwise <c>ApprovalRequestNo</c> identifies the request
/// now sitting in the inbox.
/// </summary>
public record ApprovalOutcome(bool AutoApproved, long? ApprovalRequestNo)
{
    public static ApprovalOutcome Auto() => new(true, null);

    public static ApprovalOutcome Pending(long approvalRequestNo) => new(false, approvalRequestNo);
}

/// <summary>Where the current approver stands on a document.</summary>
public enum ApproverView
{
    /// <summary>At the current step and not yet acted — the approver can act now.</summary>
    Actionable = 0,

    /// <summary>Assigned to a later step — waiting on earlier approvers.</summary>
    Waiting = 1,

    /// <summary>Assigned to an earlier step that has already passed.</summary>
    Passed = 2
}

/// <summary>A request awaiting the current approver's action.</summary>
public class PendingApprovalDto
{
    [JsonPropertyName("approval_request_no")] public long ApprovalRequestNo { get; set; }
    [JsonPropertyName("document_type")] public string? DocumentType { get; set; }
    [JsonPropertyName("document_no")] public string? DocumentNo { get; set; }
    [JsonPropertyName("document_pk")] public long? DocumentPk { get; set; }
    [JsonPropertyName("amount")] public decimal? Amount { get; set; }
    [JsonPropertyName("current_step")] public short? CurrentStep { get; set; }
    [JsonPropertyName("approver_role_no")] public long? ApproverRoleNo { get; set; }
    [JsonPropertyName("requested_by")] public long? RequestedBy { get; set; }
    [JsonPropertyName("requested_at")] public DateTime? RequestedAt { get; set; }
}

/// <summary>
/// Notified when an approval request reaches a terminal state. The .NET counterpart of the Spring
/// <c>ApplicationEventPublisher</c> → <c>@EventListener</c> pair: the engine resolves every
/// registered listener and calls it once the request is completed.
/// </summary>
public interface IApprovalCompletedListener
{
    Task OnApprovalCompletedAsync(string documentType, long documentPk, bool approved,
                                  CancellationToken cancellationToken = default);
}

public interface IApprovalService
{
    // ─── Raise ───────────────────────────────────────────────────────────────

    /// <summary>Raises against the form the caller is currently on, falling back to the document type.</summary>
    Task<ApprovalOutcome> RaiseAsync(string documentType, long documentPk, string? documentNo, decimal? amount,
                                     CancellationToken cancellationToken = default);

    Task<ApprovalOutcome> RaiseAsync(string documentType, long documentPk, string? documentNo, decimal? amount,
                                     long? departmentNo, CancellationToken cancellationToken = default);

    /// <summary>Raises against an explicitly named form.</summary>
    Task<ApprovalOutcome> RaiseOnFormAsync(string? menuFormId, string documentType, long documentPk,
                                           string? documentNo, decimal? amount, long? departmentNo,
                                           CancellationToken cancellationToken = default);

    /// <summary>
    /// Tries each form in order and raises against the first with an active scope; auto-approves
    /// when none has one.
    /// </summary>
    Task<ApprovalOutcome> RaiseOnFirstConfiguredFormAsync(IReadOnlyList<string>? menuFormIds, string documentType,
                                                          long documentPk, string? documentNo, decimal? amount,
                                                          long? departmentNo,
                                                          CancellationToken cancellationToken = default);

    // ─── Act ─────────────────────────────────────────────────────────────────

    Task ActAsync(long approvalRequestNo, bool approve, string? remarks,
                  CancellationToken cancellationToken = default);

    Task CancelRequestAsync(long? approvalRequestNo, CancellationToken cancellationToken = default);

    // ─── Query ───────────────────────────────────────────────────────────────

    Task<List<PendingApprovalDto>> GetPendingAsync(CancellationToken cancellationToken = default);

    /// <summary>Document PKs the caller can act on right now.</summary>
    Task<List<long>> GetPendingDocumentPksForCurrentApproverAsync(string documentType,
                                                                  CancellationToken cancellationToken = default);

    /// <summary>Document PKs the caller is an approver on, for requests in the given status.</summary>
    Task<List<long>> GetInvolvedDocumentPksAsync(string documentType, short requestStatus,
                                                 CancellationToken cancellationToken = default);

    /// <summary>Per-document view of where the caller stands; the most actionable view wins.</summary>
    Task<Dictionary<long, ApproverView>> GetApproverViewsAsync(string documentType,
                                                               CancellationToken cancellationToken = default);

    /// <summary>True when the request is absent, or pending with no step yet acted on.</summary>
    Task<bool> IsUntouchedAsync(long? approvalRequestNo, CancellationToken cancellationToken = default);
}
