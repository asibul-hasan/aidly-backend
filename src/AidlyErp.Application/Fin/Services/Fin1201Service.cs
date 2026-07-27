using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using AidlyErp.Application.Common.Exceptions;
using AidlyErp.Application.Common.Interfaces;
using AidlyErp.Application.Common.Security;
using AidlyErp.Application.Fin.Contract;
using AidlyErp.Application.Fin.Dto;
using AidlyErp.Domain.Fin;
using AidlyErp.Domain.Sys;

namespace AidlyErp.Application.Fin.Services;

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
    private readonly IApplicationDbContext _db;
    private readonly IFinPostingService _postingEngine;
    private readonly ICompanyBranchContext _ctx;

    public Fin1201Service(IApplicationDbContext db, IFinPostingService postingEngine, ICompanyBranchContext ctx)
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
// ═══════════════════════════════════════════════════════════════════════════

public interface IFinApprovalListener
{
    Task OnApprovalOutcomeAsync(long voucherNo, bool approved, CancellationToken ct = default);
}

public class FinApprovalListener : IFinApprovalListener
{
    private readonly IFin1101Service _voucherService;

    public FinApprovalListener(IFin1101Service voucherService)
    {
        _voucherService = voucherService;
    }

    public async Task OnApprovalOutcomeAsync(long voucherNo, bool approved, CancellationToken ct = default)
    {
        await _voucherService.ApplyApprovalOutcomeAsync(voucherNo, approved, ct);
    }
}
