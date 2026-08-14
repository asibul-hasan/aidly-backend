namespace AidlyErp.Sys.Contracts;

/// <summary>
/// Notified when a GL voucher is successfully posted for a source document.
/// Each module implements this to stamp gl_voucher_no on its own document row.
/// Registered via IEnumerable&lt;IGlPostedListener&gt; — same pattern as IApprovalCompletedListener.
/// </summary>
public interface IGlPostedListener
{
    /// <summary>
    /// Called after a successful auto-post. Implementations should switch on sourceDocType
    /// and only handle their own types; unknown types must be a no-op, never throw.
    /// </summary>
    Task OnGlPostedAsync(string sourceDocType, long sourceDocNo, long voucherNo, string voucherId, CancellationToken ct = default);
}
