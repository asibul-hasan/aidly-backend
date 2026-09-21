using AidlyErp.Shared.Core.Abstractions;
using AidlyErp.Shared.Contracts;
using AidlyErp.Sys.Contracts;
using AidlyErp.Hrm.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace AidlyErp.Hrm.Application.Services;

/// <summary>
/// Applies the shared approval engine's outcome to HRM documents.
/// Port of Java <c>HrmApprovalListener.onApprovalCompleted(ApprovalCompletedEvent)</c>.
/// Dispatches by document_type to the appropriate service's ApplyApprovalOutcomeAsync method.
/// </summary>
public class HrmApprovalListener : IApprovalCompletedListener
{
    // Resolved at callback time, not injected — all three of these services take IApprovalService,
    // and ApprovalService takes IEnumerable<IApprovalCompletedListener>, so constructor injection
    // here is a dependency cycle the container cannot build. See FinApprovalListener for the full
    // account; the same defect was live in both listeners. The provider is the request scope, so
    // the resolved service shares the engine's DbContext and transaction.
    private readonly IServiceProvider _services;

    public HrmApprovalListener(IServiceProvider services)
    {
        _services = services;
    }

    private IHrm1202Service PayrollService => _services.GetRequiredService<IHrm1202Service>();
    private IHrm1301Service LeaveService => _services.GetRequiredService<IHrm1301Service>();

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
                await PayrollService.ApplyApprovalOutcomeAsync(documentNo, approved, ct);
                break;
            case "HRM_1301":
            case "HRM_LEAVE":
                await LeaveService.ApplyApprovalOutcomeAsync(documentNo, approved, ct);
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
        await PayrollService.ApplyApprovalOutcomeAsync(payrollRunNo, approved, ct);
    }
}
