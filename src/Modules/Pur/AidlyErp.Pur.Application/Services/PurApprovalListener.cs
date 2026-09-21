using Microsoft.Extensions.DependencyInjection;
using AidlyErp.Sys.Contracts;

namespace AidlyErp.Pur.Application.Services;

// ═══════════════════════════════════════════════════════════════════════════
// PurApprovalListener — applies a completed approval to the PUR document
//
// PUR raises two approvable documents (PUR_PO from Pur1101Service, PUR_INVOICE
// from Pur1102Service) but registered NO listener, while FIN and HRM both did.
//
// That was invisible for as long as no workflow existed. With no
// sys_approval_workflow rows for a document type, RaiseAsync returns
// auto-approved and the service calls its own ApplyApprovalOutcomeAsync
// directly — the listener is never needed. The moment anyone configures a
// workflow in SYS_1108, submit takes the other branch: the document parks at
// its in-review status, the approver acts from the inbox, ApprovalService
// publishes the completion... and nothing was listening for PUR. The order or
// invoice would sit approved-in-the-engine but unposted forever — no stock, no
// payable, no GL — with no error to show for it.
// ═══════════════════════════════════════════════════════════════════════════

public class PurApprovalListener : IApprovalCompletedListener
{
    // Resolved at callback time, NOT injected — the same rule FinApprovalListener documents.
    // ApprovalService takes IEnumerable<IApprovalCompletedListener>, and both PUR services take
    // IApprovalService, so constructor-injecting them here would close the loop
    //     ApprovalService -> PurApprovalListener -> Pur1101Service -> ApprovalService
    // which the container cannot build. It compiles and the app still starts; the cycle only
    // surfaces when the graph is first walked, taking down every request downstream of the
    // approval engine. The provider is the request scope, so the resolved service shares the
    // engine's DbContext and transaction exactly as constructor injection would have.
    private readonly IServiceProvider _services;

    public PurApprovalListener(IServiceProvider services)
    {
        _services = services;
    }

    public async Task OnApprovalCompletedAsync(
        string documentType, long documentPk, bool approved, CancellationToken ct = default)
    {
        switch (documentType)
        {
            case Pur1101Service.DocType:      // PUR_PO
                await _services.GetRequiredService<IPur1101Service>()
                    .ApplyApprovalOutcomeAsync(documentPk, approved, ct);
                break;

            case Pur1102Service.DocType:      // PUR_INVOICE
                await _services.GetRequiredService<IPur1102Service>()
                    .ApplyApprovalOutcomeAsync(documentPk, approved, ct);
                break;
        }
    }
}
