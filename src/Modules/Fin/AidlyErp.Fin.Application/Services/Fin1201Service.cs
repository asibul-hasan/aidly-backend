using Microsoft.Extensions.DependencyInjection;
using AidlyErp.Fin.Application.Interfaces;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Abstractions;
using AidlyErp.Shared.Contracts;
using AidlyErp.Sys.Contracts;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Fin.Contracts;
using AidlyErp.Fin.Application.Dto;
using AidlyErp.Fin.Domain;

namespace AidlyErp.Fin.Application.Services;

// ═══════════════════════════════════════════════════════════════════════════
// Fin1201Service — Event Outbox Monitor & Re-drive
// ═══════════════════════════════════════════════════════════════════════════

public interface IFin1201Service
{
    Task<List<Fin1201EventDto>> GetOutboxEventsAsync(short? status, CancellationToken ct = default);
    Task RedriveAsync(long eventNo, CancellationToken ct = default);
}

public class Fin1201Service : IFin1201Service
{
    private readonly IFinDbContext _db;
    private readonly IFinPostingService _postingEngine;
    private readonly ICompanyBranchContext _ctx;

    public Fin1201Service(IFinDbContext db, IFinPostingService postingEngine, ICompanyBranchContext ctx)
    {
        _db = db;
        _postingEngine = postingEngine;
        _ctx = ctx;
    }

    public async Task<List<Fin1201EventDto>> GetOutboxEventsAsync(short? status, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? 0;
        var query = _db.EventOutboxes.AsNoTracking().Where(e => e.CompanyNo == companyNo);
        if (status.HasValue) query = query.Where(e => e.Status == status.Value);

        return await query
            .OrderByDescending(e => e.EventNo)
            .Take(100)
            .Select(e => new Fin1201EventDto
            {
                EventNo = e.EventNo,
                EventType = e.EventType,
                AggregateType = e.AggregateType,
                AggregateId = e.AggregateId,
                CreatedAt = e.CreatedAt,
                Status = e.Status,
                Payload = e.Payload
            })
            .ToListAsync(ct);
    }

    public async Task RedriveAsync(long eventNo, CancellationToken ct = default)
    {
        await _postingEngine.RedriveAsync(eventNo, ct);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// FinApprovalListener — Workflow Approval Listener
// Port of Java FinApprovalListener.onApprovalCompleted(ApprovalCompletedEvent).
// Implements IApprovalCompletedListener so the shared approval engine's
// IEnumerable<IApprovalCompletedListener> injection discovers this listener.
// ═══════════════════════════════════════════════════════════════════════════

public class FinApprovalListener : IApprovalCompletedListener
{
    // Resolved at callback time, not injected.
    //
    // ApprovalService takes IEnumerable<IApprovalCompletedListener> in its constructor, and
    // Fin1101Service takes IApprovalService. Taking IFin1101Service here closed that loop:
    //
    //     ApprovalService -> FinApprovalListener -> Fin1101Service -> ApprovalService
    //
    // The container cannot build that, so *every* request whose controller needed anything
    // downstream of IApprovalService — the whole of POS among them — failed with a circular
    // dependency at controller construction. It compiles and the app still starts, because the
    // cycle is only discovered when the graph is first walked at runtime.
    //
    // This is the same rule the approval docs state for ApprovalService itself ("never inject a
    // document service into it — use the event"); it applies just as much to a listener the engine
    // constructs. The provider here is the request scope, so the resolved service shares the
    // engine's DbContext and transaction exactly as constructor injection would have.
    private readonly IServiceProvider _services;

    public FinApprovalListener(IServiceProvider services)
    {
        _services = services;
    }

    public async Task OnApprovalCompletedAsync(string documentType, long documentPk, bool approved, CancellationToken ct = default)
    {
        switch (documentType)
        {
            case "FIN_VOUCHER":
                await _services.GetRequiredService<IFin1101Service>()
                    .ApplyApprovalOutcomeAsync(documentPk, approved, ct);
                break;
        }
    }
}
