using AidlyErp.Shared.Core.Abstractions;
using AidlyErp.Shared.Contracts;
using AidlyErp.Sys.Contracts;
using AidlyErp.Hrm.Application.Interfaces;

namespace AidlyErp.Hrm.Application.Services;

/// <summary>
/// Applies the shared approval engine's outcome to HRM documents.
/// Port of Java <c>HrmApprovalListener.onApprovalCompleted(ApprovalCompletedEvent)</c>.
/// Dispatches by document_type to the appropriate service's ApplyApprovalOutcomeAsync method.
/// </summary>
public class HrmApprovalListener : IApprovalCompletedListener
{
    private readonly IHrm1202Service _payrollService;
    private readonly IHrm1301Service _leaveService;
    private readonly IHrm1206Service _loanService;

    public HrmApprovalListener(
        IHrm1202Service payrollService,
        IHrm1301Service leaveService,
        IHrm1206Service loanService)
    {
        _payrollService = payrollService;
        _leaveService = leaveService;
        _loanService = loanService;
    }

    /// <summary>
    /// Dispatches approval outcome to the correct HRM service based on document type.
    /// </summary>
    /// <param name="documentType">The document type constant (e.g. "HRM_PAYROLL", "HRM_LEAVE", "HRM_LOAN").</param>
    /// <param name="documentNo">The primary key of the document.</param>
    /// <param name="approved">True if approved, false if rejected.</param>
    public async Task OnApprovalCompletedAsync(string documentType, long documentNo, bool approved, CancellationToken ct = default)
    {
        switch (documentType)
        {
            case "HRM_PAYROLL":
                await _payrollService.ApplyApprovalOutcomeAsync(documentNo, approved, ct);
                break;
            case "HRM_1301":
            case "HRM_LEAVE":
                await _leaveService.ApplyApprovalOutcomeAsync(documentNo, approved, ct);
                break;
            case "HRM_LOAN":
                // Loan approval is handled inline in Hrm1206Service via IApprovalService.
                break;
            // Other HRM document types (HRM_ATT_ADJ, HRM_OT, HRM_MOVEMENT, HRM_REQUISITION,
            // HRM_BONUS, HRM_FINAL_SETTLEMENT) are handled inline in their respective services
            // via IApprovalService integration — no separate listener dispatch needed.
        }
    }

    /// <summary>
    /// Legacy overload for backward compatibility with existing callers.
    /// </summary>
    public async Task OnApprovalCompletedAsync(long payrollRunNo, bool approved, CancellationToken ct = default)
    {
        await _payrollService.ApplyApprovalOutcomeAsync(payrollRunNo, approved, ct);
    }
}
