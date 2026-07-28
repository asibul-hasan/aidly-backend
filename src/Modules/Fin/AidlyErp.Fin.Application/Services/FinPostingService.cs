using AidlyErp.Fin.Application.Interfaces;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Abstractions;
using AidlyErp.Shared.Contracts;
using AidlyErp.Sys.Contracts;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Fin.Application.Contract;
using AidlyErp.Fin.Application.Dto;
using AidlyErp.Fin.Domain;
using AidlyErp.Sys.Domain;

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
    private readonly IFin1101Service _voucherService;

    private const short StatusPending = 1;
    private const short StatusPublished = 2;
    private const short StatusFailed = 3;
    private const int BatchSize = 50;

    public FinPostingService(IFinDbContext db, IFin1101Service voucherService)
    {
        _db = db;
        _voucherService = voucherService;
    }

    public async Task<int> DrainOnceAsync(CancellationToken ct = default)
    {
        var dueEvents = await _db.EventOutboxes
            .Where(e => e.Status == StatusPending && e.AvailableAt <= DateTime.UtcNow)
            .OrderBy(e => e.EventNo)
            .Take(BatchSize)
            .ToListAsync(ct);

        int handled = 0;
        foreach (var ev in dueEvents)
        {
            try
            {
                await ProcessAndPublishAsync(ev, ct);
                handled++;
            }
            catch (Exception ex)
            {
                await MarkFailedAsync(ev, ex.Message, ct);
            }
        }
        return handled;
    }

    public async Task RedriveAsync(long eventNo, CancellationToken ct = default)
    {
        var ev = await _db.EventOutboxes.FirstOrDefaultAsync(e => e.EventNo == eventNo, ct)
            ?? throw new NotFoundException($"Outbox event not found: {eventNo}");

        try
        {
            await ProcessAndPublishAsync(ev, ct);
        }
        catch (Exception ex)
        {
            await MarkFailedAsync(ev, ex.Message, ct);
            throw new ValidationException($"Re-drive failed: {ex.Message}");
        }
    }

    private async Task ProcessAndPublishAsync(EventOutbox ev, CancellationToken ct)
    {
        if (ev.Status == StatusPublished) return;

        long companyNo = ev.CompanyNo;
        long? sourceDocNo = long.TryParse(ev.AggregateId, out var parsedId) ? parsedId : null;

        if (sourceDocNo == null || !await _voucherService.AlreadyPostedAsync(ev.EventType, sourceDocNo.Value, companyNo, ct))
        {
            var payload = ParsePayload(ev.Payload);
            long branchNo = payload.BranchNo ?? ev.BranchNo ?? throw new ValidationException("Event has no branch to post against");

            var lines = await BuildLinesAsync(ev.EventType, companyNo, payload, ct);
            if (lines.Count < 2) throw new ValidationException("Event produced fewer than two GL lines");

            long voucherTypeNo = await _voucherService.ResolveSystemJournalTypeAsync(companyNo, ct);
            DateTime date = payload.VoucherDate ?? DateTime.UtcNow;

            await _voucherService.PostSystemVoucherAsync(
                companyNo,
                branchNo,
                voucherTypeNo,
                date,
                !string.IsNullOrWhiteSpace(payload.Narration) ? payload.Narration : ev.EventType,
                SourceModuleFor(ev.EventType),
                ev.EventType,
                sourceDocNo ?? 0,
                lines,
                ct);
        }

        ev.Status = StatusPublished;
        ev.PublishedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private async Task<List<Fin1101VoucherLineDto>> BuildLinesAsync(string eventType, long companyNo, GlPostingPayload payload, CancellationToken ct)
    {
        if (payload.Legs == null || payload.Legs.Count == 0) throw new ValidationException("Event payload has no legs");

        var lines = new List<Fin1101VoucherLineDto>();

        foreach (var leg in payload.Legs)
        {
            if (leg.Amount == 0) continue;
            string legKey = leg.LegKey?.Trim().ToUpperInvariant() ?? throw new ValidationException("Leg without legKey");
            string subKey = leg.SubKey?.Trim() ?? string.Empty;

            var map = await _db.FinGlMaps
                .FirstOrDefaultAsync(m => m.CompanyNo == companyNo && m.EventType == eventType && m.LegKey == legKey && (m.SubKey == subKey || string.IsNullOrEmpty(m.SubKey)) && m.IsActive == 1 && m.IsDeleted == 0, ct)
                ?? throw new ValidationException($"No active GL mapping for {eventType} / {legKey} — configure in GL Mapping (FIN_1006)");

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
        try
        {
            ev.Status = StatusFailed;
            ev.RetryCount++;
            await _db.SaveChangesAsync(ct);
        }
        catch { }
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
