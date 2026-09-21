using AidlyErp.Shared.Core;
using AidlyErp.Fin.Application.Interfaces;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
// FinPostingService — GL Event Auto-Posting Engine
// ═══════════════════════════════════════════════════════════════════════════

public interface IFinPostingService
{
    Task<int> DrainOnceAsync(CancellationToken ct = default);
    Task RedriveAsync(long eventNo, CancellationToken ct = default);
}

public class FinPostingService : IFinPostingService
{
    private readonly IFinDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly long _systemUserNo;
    private readonly ILogger<FinPostingService> _log;

    private const short StatusPending = 1;
    private const short StatusPublished = 2;
    private const short StatusFailed = 3;
    private const int BatchSize = 50;
    private const int MaxRetries = 5;

    public FinPostingService(IFinDbContext db, ICompanyBranchContext ctx,
                             IServiceScopeFactory scopeFactory, IConfiguration config,
                             ILogger<FinPostingService> log)
    {
        _log = log;
        _db = db;
        _ctx = ctx;
        _scopeFactory = scopeFactory;
        _systemUserNo = long.TryParse(config["Fin:SystemUserNo"], out var sno) ? sno : 0;
    }

    public async Task<int> DrainOnceAsync(CancellationToken ct = default)
    {
        // Claim the batch with FOR UPDATE SKIP LOCKED.
        //
        // The plain query let EVERY drainer pointed at this database read the same pending rows.
        // With a second instance running against the same Aiven database, both picked up the same
        // event: one won, the other failed and burned a retry, so events landed in Failed with no
        // trace in this process's log and vouchers had to be re-driven by hand. SKIP LOCKED makes
        // each drainer take a disjoint set — the loser simply moves on to the next row instead of
        // fighting over one, so correctness no longer depends on only one instance being deployed.
        //
        // Non-relational providers (the in-memory test context) cannot take row locks; there is no
        // concurrency there either, so the plain query is correct for them.
        List<EventOutbox> dueEvents;
        if (_db.Database.IsRelational())
        {
            dueEvents = await _db.EventOutboxes
                .FromSqlRaw(
                    """
                    SELECT * FROM sys_event_outbox
                    WHERE status = {0} AND available_at <= now()
                    ORDER BY event_no
                    LIMIT {1}
                    FOR UPDATE SKIP LOCKED
                    """, StatusPending, BatchSize)
                .ToListAsync(ct);
        }
        else
        {
            dueEvents = await _db.EventOutboxes
                .Where(e => e.Status == StatusPending && e.AvailableAt <= DateTime.UtcNow)
                .OrderBy(e => e.EventNo)
                .Take(BatchSize)
                .ToListAsync(ct);
        }


        int handled = 0;
        foreach (var ev in dueEvents)
        {
            // Each event gets its own scope + transaction — REQUIRES_NEW semantics
            using var scope = _scopeFactory.CreateScope();
            var scopedVoucherService = scope.ServiceProvider.GetRequiredService<IFin1101Service>();
            var scopedCtx = scope.ServiceProvider.GetRequiredService<ICompanyBranchContext>();
            var scopedUow = scope.ServiceProvider.GetRequiredService<IUnitOfWork<IFinDbContext>>();

            try
            {
                await ProcessAndPublishAsync(ev, scopedVoucherService, scopedCtx, scopedUow, ct);
                handled++;
            }
            catch (Exception ex)
            {
                // MarkFailed must use a SEPARATE clean context — never the one that just threw
                await MarkFailedAsync(ev, ex.Message, ct);
            }
        }


        return handled;
    }

    public async Task RedriveAsync(long eventNo, CancellationToken ct = default)
    {
        var ev = await _db.EventOutboxes.FirstOrDefaultAsync(e => e.EventNo == eventNo, ct)
            ?? throw new NotFoundException($"Outbox event not found: {eventNo}");

        using var scope = _scopeFactory.CreateScope();
        var scopedVoucherService = scope.ServiceProvider.GetRequiredService<IFin1101Service>();
        var scopedCtx = scope.ServiceProvider.GetRequiredService<ICompanyBranchContext>();
        var scopedUow = scope.ServiceProvider.GetRequiredService<IUnitOfWork<IFinDbContext>>();

        try
        {
            await ProcessAndPublishAsync(ev, scopedVoucherService, scopedCtx, scopedUow, ct);
        }
        catch (Exception ex)
        {
            await MarkFailedAsync(ev, ex.Message, ct);
            throw new ValidationException($"Re-drive failed: {ex.Message}");
        }
    }

    private async Task ProcessAndPublishAsync(EventOutbox ev, IFin1101Service voucherService,
                                              ICompanyBranchContext ctx, IUnitOfWork<IFinDbContext> uow,
                                              CancellationToken ct)
    {
        if (ev.Status == StatusPublished) return;

        // Set tenant context from the event row — not from an ambient request
        ctx.SetContext(ev.CompanyNo, ev.BranchNo, _systemUserNo, "system", "FinPostingService");
        try
        {
            // Handle GlPosted events — dispatch to registered listeners
            if (ev.EventType == "GlPosted")
            {
                await DispatchGlPostedAsync(ev, ct);
                ev.Status = StatusPublished;
                ev.PublishedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);
                return;
            }

            long companyNo = ev.CompanyNo;
            long? sourceDocNo = long.TryParse(ev.AggregateId, out var parsedId) ? parsedId : null;

            if (sourceDocNo == null || !await voucherService.AlreadyPostedAsync(ev.EventType, sourceDocNo.Value, companyNo, ct))
            {
                var payload = ParsePayload(ev.Payload);
                long branchNo = payload.BranchNo ?? ev.BranchNo ?? throw new ValidationException("Event has no branch to post against");

                var lines = await BuildLinesAsync(ev.EventType, companyNo, payload, ct);
                if (lines.Count < 2) throw new ValidationException("Event produced fewer than two GL lines");

                long voucherTypeNo = await voucherService.ResolveSystemJournalTypeAsync(companyNo, ct);
                DateTime date = payload.VoucherDate ?? DateTime.UtcNow;

                // ONE transaction for insert + post.
                //
                // PostSystemVoucherAsync calls InsertInternalAsync (which commits the Draft header
                // and lines) and then PostInternalAsync (period re-check, balance re-check,
                // fin_ledger write) as two separate units. InsertInternalAsync only skips opening
                // its own transaction when one is already ambient — and this engine, the highest
                // volume unattended posting path in the system, never opened one. Its comment
                // claimed "the posting engine opens one"; it did not.
                //
                // So anything failing between the two — a period closed by another user a moment
                // earlier, a process restart, a database blip — left a Draft voucher committed and
                // unposted while the outbox event went back to Pending. AlreadyPostedAsync only
                // looks for Status == Posted, so the retry would not see that Draft and would build
                // a second voucher for the same source document. The unique index on
                // (company, branch, source_doc_type, source_doc_no) then rejected the retry
                // outright, so the event could never post at all until someone deleted the orphan
                // by hand.
                long voucherNo = await uow.ExecuteAsync(async token => await voucherService.PostSystemVoucherAsync(
                    companyNo,
                    branchNo,
                    voucherTypeNo,
                    date,
                    !string.IsNullOrWhiteSpace(payload.Narration) ? payload.Narration : ev.EventType,
                    SourceModuleFor(ev.EventType),
                    ev.EventType,
                    sourceDocNo ?? 0,
                    lines,
                    token), ct);

                // Emit GlPosted so the source document can stamp its gl_voucher_no
                var postedVoucher = await _db.FinVouchers.AsNoTracking()
                    .FirstOrDefaultAsync(v => v.VoucherNo == voucherNo, ct);
                if (postedVoucher != null && sourceDocNo.HasValue)
                {
                    var glPosted = new EventOutbox
                    {
                        CompanyNo = companyNo,
                        BranchNo = branchNo,
                        EventType = "GlPosted",
                        AggregateType = ev.EventType, // original event type (e.g., SalesInvoicePosted)
                        AggregateId = sourceDocNo.Value.ToString(),
                        Payload = JsonSerializer.Serialize(new
                        {
                            source_doc_type = ev.EventType,
                            source_doc_no = sourceDocNo.Value,
                            voucher_no = voucherNo,
                            voucher_id = postedVoucher.VoucherId
                        }),
                        Status = 1,
                        CreatedAt = DateTime.UtcNow
                    };
                    _db.EventOutboxes.Add(glPosted);
                }
            }

            ev.Status = StatusPublished;
            ev.PublishedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

        }
        finally
        {
            ctx.Clear();
        }
    }

    private async Task DispatchGlPostedAsync(EventOutbox ev, CancellationToken ct)
    {
        // Parse the GlPosted payload
        var glPayload = JsonSerializer.Deserialize<GlPostedPayload>(ev.Payload);
        if (glPayload == null) return;

        // Fan out to all registered IGlPostedListener implementations
        using var scope = _scopeFactory.CreateScope();
        var listeners = scope.ServiceProvider.GetServices<IGlPostedListener>();
        foreach (var listener in listeners)
        {
            try
            {
                await listener.OnGlPostedAsync(glPayload.source_doc_type, glPayload.source_doc_no,
                    glPayload.voucher_no, glPayload.voucher_id ?? "", ct);
            }
            catch (Exception ex)
            {
                // A listener must never break the drain — log and continue
                System.Diagnostics.Debug.WriteLine($"GlPosted listener failed: {ex.Message}");
            }
        }
    }

    private record GlPostedPayload(string source_doc_type, long source_doc_no, long voucher_no, string? voucher_id);

    private async Task<List<Fin1101VoucherLineDto>> BuildLinesAsync(string eventType, long companyNo, GlPostingPayload payload, CancellationToken ct)
    {
        if (payload.Legs == null || payload.Legs.Count == 0) throw new ValidationException("Event payload has no legs");

        // Batch-load ALL mappings for (companyNo, eventType) once — avoid N+1
        var allMaps = await _db.FinGlMaps.AsNoTracking()
            .Where(m => m.CompanyNo == companyNo && m.EventType == eventType && m.IsActive == 1 && m.IsDeleted == 0)
            .ToListAsync(ct);

        // Build lookup: exact match first, then generic fallback
        var mapLookup = allMaps
            .GroupBy(m => (m.LegKey, m.SubKey ?? ""))
            .ToDictionary(g => g.Key, g => g.First());

        var genericFallback = allMaps
            .Where(m => string.IsNullOrEmpty(m.SubKey))
            .ToDictionary(m => m.LegKey, m => m);

        var lines = new List<Fin1101VoucherLineDto>();

        foreach (var leg in payload.Legs)
        {
            if (leg.Amount == 0) continue;
            string legKey = leg.LegKey?.Trim().ToUpperInvariant() ?? throw new ValidationException("Leg without legKey");
            string subKey = leg.SubKey?.Trim() ?? string.Empty;

            // Exact match first, then generic fallback
            FinGlMap? map = null;
            if (!string.IsNullOrEmpty(subKey))
            {
                mapLookup.TryGetValue((legKey, subKey), out map);
            }
            map ??= genericFallback.GetValueOrDefault(legKey);

            if (map == null)
                throw new ValidationException($"No active GL mapping for {eventType} / {legKey} (sub-key '{subKey}') — configure it in GL Mapping (FIN_1006)");

            string side = leg.DrCr?.Trim().ToLowerInvariant() ?? "dr";
            if (side is "1" or "dr" or "debit") side = "dr";
            else if (side is "2" or "cr" or "credit") side = "cr";

            var line = new Fin1101VoucherLineDto
            {
                AccountNo = map.AccountNo,
                Debit = side == "dr" ? leg.Amount : 0m,
                Credit = side == "cr" ? leg.Amount : 0m,
                CostCenterNo = leg.CostCenterNo,
                PartyType = leg.PartyType,
                PartyNo = leg.PartyNo
            };

            lines.Add(line);
        }

        return lines;
    }

    private async Task MarkFailedAsync(EventOutbox ev, string message, CancellationToken ct)
    {
        // The reason used to be discarded: this method took `message` and never used it, so a Failed
        // event reached FIN_1201 with nothing to diagnose it by. There is no error_message column on
        // sys_event_outbox, so the log is the record until there is one.
        _log.LogError("GL posting failed for event {EventNo} ({EventType}, aggregate {AggregateId}): {Reason}",
                      ev.EventNo, ev.EventType, ev.AggregateId, message);

        try
        {
            // A SEPARATE context, as the original comment always intended but the code did not do:
            // it saved on _db, the very context whose failure put us here. Once EF has thrown, that
            // context can hold half-applied tracked state, so the retry bookkeeping could fail to
            // persist — and the failure was swallowed into Debug.WriteLine, which goes nowhere in a
            // console host. The event then stayed exactly as it was, and the operator saw a stuck
            // row with no explanation. Re-reading the row on a clean context makes the bookkeeping
            // independent of whatever went wrong.
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IFinDbContext>();

            var row = await db.EventOutboxes.FirstOrDefaultAsync(e => e.EventNo == ev.EventNo, ct);
            if (row is null) return;

            row.RetryCount++;
            if (row.RetryCount >= MaxRetries)
            {
                row.Status = StatusFailed;
                // Leave as Failed — FIN_1201 re-drive is the manual escape hatch
            }
            else
            {
                row.Status = StatusPending;
                // Exponential backoff: 2^RetryCount minutes, capped at 1 hour
                var backoffMinutes = Math.Min(Math.Pow(2, row.RetryCount), 60);
                row.AvailableAt = DateTime.UtcNow.AddMinutes(backoffMinutes);
            }
            await db.SaveChangesAsync(ct);

            // Keep the caller's instance in step so the drain loop sees the same state.
            ev.RetryCount = row.RetryCount;
            ev.Status = row.Status;
            ev.AvailableAt = row.AvailableAt;
        }
        catch (Exception ex)
        {
            // Never silent: if even the bookkeeping fails, that is the operator's only warning that
            // an event is stuck and will not be retried on its own.
            _log.LogError(ex, "Could not record failure for outbox event {EventNo} — it may be stuck",
                          ev.EventNo);
        }
    }

    private static GlPostingPayload ParsePayload(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<GlPostingPayload>(json) ?? throw new ValidationException("Empty payload");
        }
        catch (Exception ex)
        {
            throw new ValidationException($"Unreadable GL payload: {ex.Message}");
        }
    }

    private static short SourceModuleFor(string eventType)
    {
        if (string.IsNullOrEmpty(eventType)) return 5;
        if (eventType.StartsWith("Payroll") || eventType.StartsWith("Hrm") || eventType.Contains("Settlement")) return 1;
        if (eventType.StartsWith("Stock") || eventType.StartsWith("Inv")) return 2;
        if (eventType.StartsWith("Purchase") || eventType.StartsWith("Supplier")) return 3;
        if (eventType.StartsWith("Sales") || eventType.StartsWith("Customer")) return 4;
        return 5; // FIN
    }
}
