using System.Text.Json;
using AidlyErp.Fin.Contracts;
using AidlyErp.Pur.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using AidlyErp.Shared.Core;
using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Abstractions;
using AidlyErp.Shared.Contracts;
using AidlyErp.Sys.Contracts;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Pur.Application.Dto;
using AidlyErp.Pur.Domain;

namespace AidlyErp.Pur.Application.Services;

public interface IPur1106Service
{
    Task<List<Pur1106LandedCostDto>> GetListAsync(CancellationToken ct = default);
    Task<Pur1106LandedCostDto> GetDetailAsync(long landedCostNo, CancellationToken ct = default);
    Task<List<Pur1106LandedCostDto>> GetPostedInvoicesAsync(CancellationToken ct = default);
    Task<List<Pur1106AllocDto>> PreviewAsync(long invoiceNo, decimal amount, short basis, CancellationToken ct = default);
    Task<Pur1106LandedCostDto> SaveAsync(Pur1106LandedCostDto dto, CancellationToken ct = default);
    Task<Pur1106LandedCostDto> ApplyAsync(long landedCostNo, CancellationToken ct = default);
    Task DeleteAsync(long landedCostNo, CancellationToken ct = default);
}

/// <summary>
/// PUR_1106 Landed Cost — full port of Java Pur1106Service.
/// Allocates additional costs (freight, customs) over posted purchase invoices.
/// Apply updates invoice final_unit_cost and emits LandedCostApplied GL event.
/// </summary>
public class Pur1106Service : IPur1106Service
{
    private readonly IPurDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    private readonly IUnitOfWork<IPurDbContext> _uow;
    private readonly IDocSequenceGenerator _docSeq;
    private readonly IFinCalendar _calendar;

    private const short StDraft = 1, StApplied = 2;
    private const string DocSeqType = "PUR_LC";

    public Pur1106Service(IPurDbContext db, ICompanyBranchContext ctx, IUnitOfWork<IPurDbContext> uow,
                          IDocSequenceGenerator docSeq, IFinCalendar calendar)
    {
        _db = db;
        _ctx = ctx;
        _uow = uow;
        _docSeq = docSeq;
        _calendar = calendar;
    }

    // ── reads ──────────────────────────────────────────────────────────────────

    public async Task<List<Pur1106LandedCostDto>> GetListAsync(CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? 0;
        long branchNo = _ctx.CurrentBranchNo() ?? 0;

        var costs = await _db.PurLandedCosts.AsNoTracking()
            .Where(c => c.CompanyNo == companyNo && c.BranchNo == branchNo && c.IsDeleted == 0)
            .OrderByDescending(c => c.LandedCostNo)
            .ToListAsync(ct);

        var invoiceNos = costs.Select(c => c.InvoiceNo).Distinct().ToList();
        var invoices = await _db.PurInvoices.AsNoTracking()
            .Where(i => invoiceNos.Contains(i.InvoiceNo) && i.IsDeleted == 0)
            .ToDictionaryAsync(i => i.InvoiceNo, ct);

        var supplierNos = invoices.Values.Select(i => i.SupplierNo).Distinct().ToList();
        var suppliers = await _db.PurSuppliers.AsNoTracking()
            .Where(s => supplierNos.Contains(s.SupplierNo) && s.IsDeleted == 0)
            .ToDictionaryAsync(s => s.SupplierNo, s => s.SupplierName, ct);

        return costs.Select(c =>
        {
            invoices.TryGetValue(c.InvoiceNo, out var inv);
            return new Pur1106LandedCostDto
            {
                LandedCostNo = c.LandedCostNo,
                CostDate = c.CostDate,
                InvoiceNo = c.InvoiceNo,
                InvoiceId = inv?.InvoiceId,
                SupplierName = inv != null ? suppliers.GetValueOrDefault(inv.SupplierNo) : null,
                CostType = c.CostType,
                Amount = c.Amount,
                Status = c.Status,
                InvoiceLandedCostTotal = inv?.LandedCostTotal ?? 0
            };
        }).ToList();
    }

    public async Task<Pur1106LandedCostDto> GetDetailAsync(long landedCostNo, CancellationToken ct = default)
    {
        var cost = await RequireAsync(landedCostNo, ct);
        var inv = await _db.PurInvoices.AsNoTracking()
            .FirstOrDefaultAsync(i => i.InvoiceNo == cost.InvoiceNo && i.IsDeleted == 0, ct);

        var supName = inv != null
            ? await _db.PurSuppliers.AsNoTracking()
                .Where(s => s.SupplierNo == inv.SupplierNo && s.IsDeleted == 0)
                .Select(s => s.SupplierName).FirstOrDefaultAsync(ct)
            : null;

        var dto = new Pur1106LandedCostDto
        {
            LandedCostNo = cost.LandedCostNo,
            CostDate = cost.CostDate,
            InvoiceNo = cost.InvoiceNo,
            InvoiceId = inv?.InvoiceId,
            SupplierName = supName,
            CostType = cost.CostType,
            Amount = cost.Amount,
            Status = cost.Status,
            Remarks = cost.Remarks,
            InvoiceLandedCostTotal = inv?.LandedCostTotal ?? 0
        };

        // Build allocations
        if (cost.Status == StDraft && inv != null)
        {
            // Preview mode — compute allocation
            dto.Allocations = Allocate(inv.InvoiceNo, cost.Amount, 1);
        }
        else
        {
            // Applied — load saved allocations
            var allocs = await _db.PurLandedCostAllocs.AsNoTracking()
                .Where(a => a.LandedCostNo == landedCostNo && a.IsDeleted == 0)
                .OrderBy(a => a.LandedCostAllocNo)
                .ToListAsync(ct);

            var preview = inv != null ? Allocate(inv.InvoiceNo, cost.Amount, 1) : new();
            var allocMap = allocs.ToDictionary(a => a.InvoiceDtlNo);

            foreach (var row in preview)
            {
                if (allocMap.TryGetValue(row.InvoiceDtlNo, out var saved))
                {
                    row.LandedCostAllocNo = saved.LandedCostAllocNo;
                    row.AllocatedAmount = saved.AllocatedAmount;
                }
            }
            dto.Allocations = preview;
        }

        return dto;
    }

    public async Task<List<Pur1106LandedCostDto>> GetPostedInvoicesAsync(CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? 0;
        long branchNo = _ctx.CurrentBranchNo() ?? 0;

        var suppliers = await _db.PurSuppliers.AsNoTracking()
            .Where(s => s.CompanyNo == companyNo && s.IsDeleted == 0)
            .ToDictionaryAsync(s => s.SupplierNo, s => s.SupplierName, ct);

        return await _db.PurInvoices.AsNoTracking()
            .Where(i => i.CompanyNo == companyNo && i.BranchNo == branchNo && i.Status == 2 && i.IsDeleted == 0)
            .OrderByDescending(i => i.InvoiceNo)
            .Select(i => new Pur1106LandedCostDto
            {
                InvoiceNo = i.InvoiceNo,
                InvoiceId = i.InvoiceId,
                SupplierName = "",
                CostDate = i.InvoiceDate,
                Amount = 0,
                InvoiceLandedCostTotal = i.LandedCostTotal
            })
            .ToListAsync(ct);
    }

    public Task<List<Pur1106AllocDto>> PreviewAsync(long invoiceNo, decimal amount, short basis, CancellationToken ct = default)
    {
        return Task.FromResult(Allocate(invoiceNo, amount, basis));
    }

    // ── write ──────────────────────────────────────────────────────────────────

    public async Task<Pur1106LandedCostDto> SaveAsync(Pur1106LandedCostDto dto, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context required");
        long branchNo = _ctx.CurrentBranchNo() ?? throw new ValidationException("Branch context required");

        if (dto.InvoiceNo <= 0) throw new ValidationException("Invoice is required");
        if (dto.Amount <= 0) throw new ValidationException("Amount must be greater than zero");

        var inv = await _db.PurInvoices.FirstOrDefaultAsync(i => i.InvoiceNo == dto.InvoiceNo && i.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Invoice not found: {dto.InvoiceNo}");
        if (inv.CompanyNo != companyNo) throw new ValidationException("Invoice belongs to another branch/company");
        if (inv.Status != 2) throw new ValidationException("Landed cost can be added only to a posted invoice");

        // Period guard
        var costDate = dto.CostDate != default ? dto.CostDate : DateTime.UtcNow.Date;
        var finYear = await _calendar.FindYearForDateAsync(companyNo, DateOnly.FromDateTime(costDate), ct)
            ?? throw new ValidationException($"No financial year for date {costDate:yyyy-MM-dd}");

        PurLandedCost cost;
        if (dto.LandedCostNo.HasValue && dto.LandedCostNo.Value > 0)
        {
            cost = await _db.PurLandedCosts.FirstOrDefaultAsync(c => c.LandedCostNo == dto.LandedCostNo.Value && c.IsDeleted == 0, ct)
                ?? throw new NotFoundException($"Landed cost not found: {dto.LandedCostNo}");
            if (cost.Status != StDraft) throw new ValidationException("Only a Draft landed cost can be edited");
        }
        else
        {
            cost = new PurLandedCost
            {
                CompanyNo = companyNo,
                BranchNo = branchNo,
                Status = StDraft,
                IsActive = 1, IsDeleted = 0,
                CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow
            };
            _db.PurLandedCosts.Add(cost);
        }

        cost.CostDate = costDate;
        cost.InvoiceNo = inv.InvoiceNo;
        cost.CostType = dto.CostType ?? "FREIGHT";
        cost.Amount = dto.Amount;
        cost.Remarks = dto.Remarks;
        cost.UpdatedBy = _ctx.CurrentUserNo(); cost.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return await GetDetailAsync(cost.LandedCostNo, ct);
    }

    public async Task<Pur1106LandedCostDto> ApplyAsync(long landedCostNo, CancellationToken ct = default)
    {
        return await _uow.ExecuteAsync(async token => await ApplyInternalAsync(landedCostNo, token), ct);
    }

    private async Task<Pur1106LandedCostDto> ApplyInternalAsync(long landedCostNo, CancellationToken ct)
    {
        var cost = await _db.PurLandedCosts.FirstOrDefaultAsync(c => c.LandedCostNo == landedCostNo && c.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Landed cost not found: {landedCostNo}");
        if (cost.Status == StApplied) return await GetDetailAsync(landedCostNo, ct);
        if (cost.Status != StDraft) throw new ValidationException("Only a Draft landed cost can be applied");

        var inv = await _db.PurInvoices.FirstOrDefaultAsync(i => i.InvoiceNo == cost.InvoiceNo && i.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Invoice not found: {cost.InvoiceNo}");

        var rows = Allocate(inv.InvoiceNo, cost.Amount, 1);
        if (rows.Count == 0) throw new ValidationException("Invoice has no allocatable lines");

        // Save allocations and update invoice line costs
        var existing = await _db.PurLandedCostAllocs.Where(a => a.LandedCostNo == landedCostNo && a.IsDeleted == 0).ToListAsync(ct);
        foreach (var e in existing) { e.IsDeleted = 1; e.DeletedBy = _ctx.CurrentUserNo(); e.DeletedAt = DateTime.UtcNow; }

        foreach (var row in rows)
        {
            var line = await _db.PurInvoiceDtls.FirstOrDefaultAsync(l => l.InvoiceDtlNo == row.InvoiceDtlNo && l.IsDeleted == 0, ct);
            if (line == null) continue;

            line.LandedCostAlloc += row.AllocatedAmount;
            if (line.QtyBase > 0)
                line.FinalUnitCost += row.AllocatedAmount / line.QtyBase;

            var alloc = new PurLandedCostAlloc
            {
                LandedCostNo = landedCostNo,
                InvoiceDtlNo = line.InvoiceDtlNo,
                AllocatedAmount = row.AllocatedAmount,
                IsActive = 1, IsDeleted = 0,
                CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow
            };
            _db.PurLandedCostAllocs.Add(alloc);
        }

        inv.LandedCostTotal += cost.Amount;
        inv.GrandTotal += cost.Amount;
        inv.DueAmount += cost.Amount;

        // GL emit
        await EmitGlAsync(cost, inv, ct);

        cost.Status = StApplied;
        cost.AppliedBy = _ctx.CurrentUserNo();
        cost.AppliedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return await GetDetailAsync(landedCostNo, ct);
    }

    public async Task DeleteAsync(long landedCostNo, CancellationToken ct = default)
    {
        var cost = await _db.PurLandedCosts.FirstOrDefaultAsync(c => c.LandedCostNo == landedCostNo && c.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Landed cost not found: {landedCostNo}");
        if (cost.Status != StDraft) throw new ValidationException("Only a Draft landed cost can be deleted");

        var allocs = await _db.PurLandedCostAllocs.Where(a => a.LandedCostNo == landedCostNo && a.IsDeleted == 0).ToListAsync(ct);
        foreach (var a in allocs) { a.IsDeleted = 1; a.DeletedBy = _ctx.CurrentUserNo(); a.DeletedAt = DateTime.UtcNow; }

        cost.IsDeleted = 1; cost.IsActive = 0;
        cost.DeletedBy = _ctx.CurrentUserNo(); cost.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    // ── helpers ────────────────────────────────────────────────────────────────

    private List<Pur1106AllocDto> Allocate(long invoiceNo, decimal amount, short basis)
    {
        if (amount <= 0) return new();

        var lines = _db.PurInvoiceDtls.AsNoTracking()
            .Where(l => l.InvoiceNo == invoiceNo && l.IsDeleted == 0)
            .OrderBy(l => l.LineNo)
            .ToList();

        decimal totalBasis = lines.Sum(l => basis == 2 ? l.QtyBase : l.TaxableAmount);
        if (totalBasis <= 0) throw new ValidationException("Allocation basis total is zero");

        var rows = new List<Pur1106AllocDto>();
        decimal allocated = 0;
        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            decimal basisAmount = basis == 2 ? line.QtyBase : line.TaxableAmount;
            decimal share = i == lines.Count - 1
                ? amount - allocated
                : Math.Round(amount * basisAmount / totalBasis, 4);
            allocated += share;

            rows.Add(new Pur1106AllocDto
            {
                InvoiceDtlNo = line.InvoiceDtlNo,
                LineNo = line.LineNo,
                ProductNo = line.ProductNo,
                QtyBase = line.QtyBase,
                TaxableAmount = line.TaxableAmount,
                PreviousAlloc = line.LandedCostAlloc,
                AllocatedAmount = share,
                FinalUnitCost = line.QtyBase > 0 ? line.FinalUnitCost + share / line.QtyBase : line.FinalUnitCost
            });
        }
        return rows;
    }

    private async Task EmitGlAsync(PurLandedCost cost, PurInvoice inv, CancellationToken ct)
    {
        long partyNo = inv.SupplierNo;
        var legs = new List<GlPostingPayload.Leg>
        {
            new() { LegKey = "INVENTORY", Amount = cost.Amount, DrCr = "dr" },
            new() { LegKey = "PAYABLE", Amount = cost.Amount, DrCr = "cr", PartyType = 2, PartyNo = partyNo }
        };

        var payload = new GlPostingPayload
        {
            VoucherDate = cost.CostDate,
            Narration = $"Landed cost for {inv.InvoiceId}",
            BranchNo = cost.BranchNo,
            Legs = legs
        };

        var ev = new EventOutbox
        {
            CompanyNo = cost.CompanyNo,
            BranchNo = cost.BranchNo,
            EventType = "LandedCostApplied",
            AggregateType = "PUR_LANDED_COST",
            AggregateId = cost.LandedCostNo.ToString(),
            Payload = JsonSerializer.Serialize(payload),
            Status = 1,
            CreatedAt = DateTime.UtcNow
        };
        _db.EventOutboxes.Add(ev);
        await _db.SaveChangesAsync(ct);
    }

    private async Task<PurLandedCost> RequireAsync(long landedCostNo, CancellationToken ct)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context required");
        var c = await _db.PurLandedCosts.AsNoTracking()
            .FirstOrDefaultAsync(x => x.LandedCostNo == landedCostNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Landed cost not found: {landedCostNo}");
        if (c.CompanyNo != companyNo) throw new ValidationException("Landed cost belongs to another company");
        return c;
    }
}
