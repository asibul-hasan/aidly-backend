using System.Text.Json;
using AidlyErp.Fin.Contracts;
using AidlyErp.Inv.Contracts;
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

public interface IPur1102Service
{
    Task<List<Pur1102InvoiceDto>> GetListAsync(CancellationToken ct = default);
    Task<Pur1102InvoiceDto> GetDetailAsync(long invoiceNo, CancellationToken ct = default);
    Task<Pur1102InvoiceDto> SaveAsync(Pur1102InvoiceDto dto, CancellationToken ct = default);
    Task<Pur1102InvoiceDto> SubmitAsync(long invoiceNo, CancellationToken ct = default);
    Task<Pur1102InvoiceDto> RejectAsync(long invoiceNo, CancellationToken ct = default);
    Task ApplyApprovalOutcomeAsync(long invoiceNo, bool approved, CancellationToken ct = default);
    Task<Pur1102InvoiceDto> CancelAsync(long invoiceNo, string? reason, CancellationToken ct = default);
    Task DeleteAsync(long invoiceNo, CancellationToken ct = default);
}

/// <summary>
/// PUR_1102 Purchase Invoice — full port of Java Pur1102Service.
/// Wires all four platform seams on post: stock IN, GL via outbox, AP via sub-ledger,
/// approval via shared ApprovalService. Cancel reverses stock + AP + GL.
/// </summary>
public class Pur1102Service : IPur1102Service
{
    private readonly IPurDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    private readonly IPurApLedgerService _apLedger;
    private readonly IInvStockPostingService _stockPostingService;
    private readonly IVatTaxLookup _vatTaxLookup;
    private readonly IUnitOfWork<IPurDbContext> _uow;
    private readonly IDocSequenceGenerator _docSeq;
    private readonly IFinCalendar _calendar;
    private readonly IApprovalService _approvalService;
    private readonly IInvCatalog _catalog;

    public const string DocType = "PUR_INVOICE";
    private const short StDraft = 1, StPosted = 2, StReturned = 4, StCancelled = 5;
    private const short MvPurchase = 2;
    private const short RefPurInv = 2;
    private const short ApRefInvoice = 2, ApRefAdj = 5;
    private const string DocSeq = "PUR_INV";

    public Pur1102Service(IPurDbContext db, ICompanyBranchContext ctx, IPurApLedgerService apLedger,
                          IInvStockPostingService stockPostingService, IVatTaxLookup vatTaxLookup,
                          IUnitOfWork<IPurDbContext> uow, IDocSequenceGenerator docSeq,
                          IFinCalendar calendar, IApprovalService approvalService, IInvCatalog catalog)
    {
        _db = db;
        _ctx = ctx;
        _apLedger = apLedger;
        _stockPostingService = stockPostingService;
        _vatTaxLookup = vatTaxLookup;
        _uow = uow;
        _docSeq = docSeq;
        _calendar = calendar;
        _approvalService = approvalService;
        _catalog = catalog;
    }

    // ── reads ──────────────────────────────────────────────────────────────────

    public async Task<List<Pur1102InvoiceDto>> GetListAsync(CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? 0;
        long branchNo = _ctx.CurrentBranchNo() ?? 0;

        var sups = await _db.PurSuppliers.AsNoTracking()
            .Where(s => s.CompanyNo == companyNo && s.IsDeleted == 0)
            .ToDictionaryAsync(s => s.SupplierNo, s => s.SupplierName, ct);

        var list = await _db.PurInvoices
            .AsNoTracking()
            .Where(i => i.CompanyNo == companyNo && i.BranchNo == branchNo && i.IsDeleted == 0)
            .OrderByDescending(i => i.InvoiceNo)
            .ToListAsync(ct);

        return list.Select(i => ToDto(i, sups.GetValueOrDefault(i.SupplierNo), null)).ToList();
    }

    public async Task<Pur1102InvoiceDto> GetDetailAsync(long invoiceNo, CancellationToken ct = default)
    {
        var inv = await RequireAsync(invoiceNo, ct);
        var supName = await _db.PurSuppliers.AsNoTracking()
            .Where(s => s.SupplierNo == inv.SupplierNo && s.IsDeleted == 0)
            .Select(s => s.SupplierName).FirstOrDefaultAsync(ct);
        var lines = await GetLinesAsync(invoiceNo, ct);
        return ToDto(inv, supName, lines);
    }

    // ── write (draft) ──────────────────────────────────────────────────────────

    public async Task<Pur1102InvoiceDto> SaveAsync(Pur1102InvoiceDto dto, CancellationToken ct = default)
    {
        return await _uow.ExecuteAsync(async token => await SaveInternalAsync(dto, token), ct);
    }

    private async Task<Pur1102InvoiceDto> SaveInternalAsync(Pur1102InvoiceDto dto, CancellationToken ct)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context required");
        long branchNo = dto.BranchNo ?? _ctx.CurrentBranchNo() ?? throw new ValidationException("Branch context required");

        if (dto.SupplierNo <= 0) throw new ValidationException("Supplier is required");
        if (dto.WarehouseNo <= 0) throw new ValidationException("Warehouse is required");
        if (string.IsNullOrWhiteSpace(dto.SupplierInvoiceNo))
            throw new ValidationException("Supplier bill number is required");
        if (dto.Lines == null || dto.Lines.Count == 0) throw new ValidationException("Add at least one line");

        // Period guard
        var invDate = dto.InvoiceDate != default ? dto.InvoiceDate : DateTime.UtcNow.Date;
        var finYear = await _calendar.FindYearForDateAsync(companyNo, DateOnly.FromDateTime(invDate), ct)
            ?? throw new ValidationException($"No financial year configured for date {invDate:yyyy-MM-dd}");
        var finPeriod = await _calendar.FindPeriodForDateAsync(finYear.FinYearNo, DateOnly.FromDateTime(invDate), ct)
            ?? throw new ValidationException($"No financial period configured for date {invDate:yyyy-MM-dd}");

        await RequireSupplierAsync(dto.SupplierNo, companyNo, ct);

        PurInvoice inv;
        if (dto.InvoiceNo.HasValue && dto.InvoiceNo.Value > 0)
        {
            inv = await _db.PurInvoices.FirstOrDefaultAsync(i => i.InvoiceNo == dto.InvoiceNo.Value && i.IsDeleted == 0, ct)
                ?? throw new NotFoundException($"Invoice not found: {dto.InvoiceNo}");
            if (inv.Status != StDraft) throw new ValidationException("Only a Draft invoice can be edited");
        }
        else
        {
            // The number is drawn BEFORE the insert, not stamped over a "TEMP" placeholder after.
            // The old order left a row literally called "TEMP" if anything failed in between.
            string invoiceId = await _docSeq.NextAsync(companyNo, branchNo, DocSeq, "PINV", 6, ct);

            inv = new PurInvoice
            {
                CompanyNo = companyNo,
                BranchNo = branchNo,
                Status = StDraft,
                ReceiveMode = 1,
                InvoiceId = invoiceId,
                // pur_invoice.fin_year_no is NOT NULL. The period guard above already resolved the
                // year; not carrying it onto the entity meant no purchase invoice could ever be
                // saved. Same defect as the POS sale had.
                FinYearNo = finYear.FinYearNo,
                FinPeriodNo = finPeriod.FinPeriodNo,
                // Also NOT NULL, and it was only assigned further down — after this insert had
                // already been attempted and failed.
                SupplierInvoiceNo = dto.SupplierInvoiceNo?.Trim(),
                SupplierNo = dto.SupplierNo,
                InvoiceDate = invDate,
                IsActive = 1, IsDeleted = 0,
                CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow
            };
            _db.PurInvoices.Add(inv);
            await _db.SaveChangesAsync(ct);
        }

        // Duplicate supplier bill guard
        if (!string.IsNullOrWhiteSpace(dto.SupplierInvoiceNo))
        {
            bool dupExists = await _db.PurInvoices.AnyAsync(i =>
                i.SupplierNo == dto.SupplierNo
                && i.SupplierInvoiceNo == dto.SupplierInvoiceNo.Trim()
                && i.InvoiceNo != inv.InvoiceNo
                && i.IsDeleted == 0, ct);
            if (dupExists) throw new ValidationException("This supplier bill number already exists for the supplier");
        }

        inv.SupplierNo = dto.SupplierNo;
        inv.SupplierInvoiceNo = dto.SupplierInvoiceNo?.Trim();
        inv.InvoiceDate = invDate;
        inv.WarehouseNo = dto.WarehouseNo;
        inv.CurrencyNo = dto.CurrencyNo;
        inv.ExchangeRate = dto.ExchangeRate > 0 ? dto.ExchangeRate : 1m;
        inv.DueDate = dto.DueDate;
        inv.Remarks = dto.Remarks;
        inv.UpdatedBy = _ctx.CurrentUserNo(); inv.UpdatedAt = DateTime.UtcNow;

        await RecomputeLinesAsync(inv, dto.Lines, ct);
        await _db.SaveChangesAsync(ct);

        return await GetDetailAsync(inv.InvoiceNo, ct);
    }

    private async Task RecomputeLinesAsync(PurInvoice inv, List<Pur1102LineDto> rows, CancellationToken ct)
    {
        // Soft-delete existing lines
        var existing = await _db.PurInvoiceDtls.Where(l => l.InvoiceNo == inv.InvoiceNo && l.IsDeleted == 0).ToListAsync(ct);
        foreach (var e in existing) { e.IsDeleted = 1; e.DeletedBy = _ctx.CurrentUserNo(); e.DeletedAt = DateTime.UtcNow; }

        decimal subTotal = 0, discTotal = 0, taxableSum = 0, tax = 0;
        int lineNo = 1;
        var entities = new List<PurInvoiceDtl>();

        var live = rows.Where(r => r.ProductNo > 0 && r.Qty > 0).ToList();
        var products = await _catalog.GetProductsAsync(live.Select(r => r.ProductNo).Distinct().ToList(),
                                                       inv.CompanyNo, ct);
        var uom = await PurUomConverter.LoadAsync(_catalog, products.Keys, ct);

        foreach (var r in live)
        {
            if (!products.TryGetValue(r.ProductNo, out var product))
                throw new NotFoundException($"Product not found: {r.ProductNo}");

            if (product.IsBatchTracked == 1 && string.IsNullOrWhiteSpace(r.BatchCode))
                throw new ValidationException($"Batch code is required for batch-tracked product: {product.ProductId}");

            decimal qty = r.Qty;
            decimal unitPrice = r.UnitPrice > 0 ? r.UnitPrice : 0;
            decimal gross = Math.Round(qty * unitPrice, 4);

            // Discount: use explicit amount if set, otherwise compute from pct
            decimal discPct = r.DiscountPct;
            decimal discAmt = r.DiscountAmount > 0
                ? r.DiscountAmount
                : Math.Round(gross * discPct / 100m, 4);

            decimal lineTaxable = gross - discAmt;
            decimal taxPct = r.TaxRatePct;
            decimal lineTax = Math.Round(lineTaxable * taxPct / 100m, 4);

            var d = new PurInvoiceDtl
            {
                InvoiceNo = inv.InvoiceNo,
                LineNo = lineNo++,
                ProductNo = product.ProductNo,
                VariantNo = r.VariantNo,
                UomNo = r.UomNo ?? product.BaseUomNo,
                Qty = qty,
                QtyBase = uom.ToBaseQty(product, r.UomNo ?? product.BaseUomNo, qty),
                UnitPrice = unitPrice,
                DiscountPct = discPct,
                DiscountAmount = discAmt,
                VatTaxNo = r.VatTaxNo,
                TaxRatePct = taxPct,
                IsTaxInclusive = 0,
                TaxableAmount = lineTaxable,
                TaxAmount = lineTax,
                LineTotal = lineTaxable + lineTax,
                BatchCode = r.BatchCode,
                MfgDate = r.MfgDate,
                ExpiryDate = r.ExpiryDate,
                Remarks = r.Remarks,
                IsActive = 1, IsDeleted = 0,
                CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow
            };
            _db.PurInvoiceDtls.Add(d);
            entities.Add(d);
            subTotal += gross;
            discTotal += discAmt;
            taxableSum += lineTaxable;
            tax += lineTax;
        }

        if (entities.Count == 0) throw new ValidationException("All lines are empty");

        // Allocate (shipping + other + round_off) into final_unit_cost by taxable weight
        decimal allocTotal = inv.ShippingCharge + inv.OtherCharges + inv.RoundOff;
        decimal basisSum = taxableSum > 0 ? taxableSum : 1;
        foreach (var d in entities)
        {
            decimal alloc = Math.Round(allocTotal * d.TaxableAmount / basisSum, 4);
            d.LandedCostAlloc = alloc;
            d.FinalUnitCost = d.QtyBase > 0
                ? Math.Round((d.TaxableAmount + alloc) / d.QtyBase, 6)
                : 0;
        }

        decimal grand = taxableSum + tax + allocTotal;
        inv.SubTotal = subTotal;
        inv.DiscountTotal = discTotal;
        inv.TaxableAmount = taxableSum;
        inv.TaxAmount = tax;
        inv.GrandTotal = grand;
        inv.DueAmount = grand - inv.PaidAmount;
        inv.PaymentStatus = PaymentStatus(inv);
    }

    // ── workflow ───────────────────────────────────────────────────────────────

    public async Task<Pur1102InvoiceDto> SubmitAsync(long invoiceNo, CancellationToken ct = default)
    {
        var inv = await RequireAsync(invoiceNo, ct);
        if (inv.Status != StDraft) throw new ValidationException("Only a Draft invoice can be submitted");

        var lines = await _db.PurInvoiceDtls.Where(l => l.InvoiceNo == invoiceNo && l.IsDeleted == 0).ToListAsync(ct);
        if (lines.Count == 0) throw new ValidationException("Add at least one line before submitting");

        if (inv.ApprovalRequestNo == null)
        {
            var outcome = await _approvalService.RaiseAsync(DocType, inv.InvoiceNo, inv.InvoiceId, inv.GrandTotal, ct);
            if (outcome.AutoApproved)
            {
                await ApplyApprovalOutcomeAsync(inv.InvoiceNo, true, ct);
            }
            else
            {
                inv.ApprovalRequestNo = outcome.ApprovalRequestNo;
                await _db.SaveChangesAsync(ct);
            }
        }
        else
        {
            await _approvalService.ActAsync(inv.ApprovalRequestNo.Value, true, null, ct);
        }

        return await GetDetailAsync(invoiceNo, ct);
    }

    public async Task<Pur1102InvoiceDto> RejectAsync(long invoiceNo, CancellationToken ct = default)
    {
        var inv = await RequireAsync(invoiceNo, ct);
        if (inv.ApprovalRequestNo == null) throw new ValidationException("No approval request — submit first");
        await _approvalService.ActAsync(inv.ApprovalRequestNo.Value, false, null, ct);
        return await GetDetailAsync(invoiceNo, ct);
    }

    public async Task ApplyApprovalOutcomeAsync(long invoiceNo, bool approved, CancellationToken ct = default)
    {
        var inv = await _db.PurInvoices.FirstOrDefaultAsync(i => i.InvoiceNo == invoiceNo && i.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Invoice not found: {invoiceNo}");

        if (approved)
        {
            await PostInternalAsync(inv, ct);
        }
        else
        {
            inv.ApprovalRequestNo = null;
            await _db.SaveChangesAsync(ct);
        }
    }

    /// <summary>One-step post: receive stock + credit AP + emit GL event. Idempotent on status.</summary>
    private async Task PostInternalAsync(PurInvoice inv, CancellationToken ct)
    {
        if (inv.Status == StPosted) return;
        if (inv.Status != StDraft) throw new ValidationException("Only a Draft invoice can be posted");

        long companyNo = inv.CompanyNo;
        long branchNo = inv.BranchNo;

        // Period guard
        var invDay = DateOnly.FromDateTime(inv.InvoiceDate);
        var finYear = await _calendar.FindYearForDateAsync(companyNo, invDay, ct)
            ?? throw new ValidationException($"No financial year for date {inv.InvoiceDate:yyyy-MM-dd}");
        var finPeriod = await _calendar.FindPeriodForDateAsync(finYear.FinYearNo, invDay, ct)
            ?? throw new ValidationException($"No financial period for date {inv.InvoiceDate:yyyy-MM-dd}");
        if (finPeriod.PeriodStatus != 1)
            throw new ValidationException($"Cannot post: period '{finPeriod.FinPeriodName}' is not Open");

        var lines = await _db.PurInvoiceDtls.Where(l => l.InvoiceNo == inv.InvoiceNo && l.IsDeleted == 0)
            .OrderBy(l => l.LineNo).ToListAsync(ct);
        if (lines.Count == 0) throw new ValidationException("Nothing to post");

        // (1) Stock IN
        var legs = lines.Select((l, idx) => StockPostingLeg.In(
            inv.WarehouseNo, l.ProductNo, l.VariantNo, l.BatchNo,
            l.QtyBase, MvPurchase, l.InvoiceDtlNo,
            l.FinalUnitCost > 0 ? l.FinalUnitCost : l.UnitPrice)).ToList();

        var cmd = new StockPostingCommand(companyNo, branchNo, RefPurInv,
            inv.InvoiceId, inv.InvoiceNo, inv.InvoiceDate, finYear.FinYearNo, null, false, legs);
        await _stockPostingService.PostAsync(cmd, ct);

        // (2) AP subsidiary ledger
        await _apLedger.CreditAsync(inv.SupplierNo, inv.GrandTotal, ApRefInvoice, inv.InvoiceId, inv.InvoiceNo,
            $"Purchase invoice {inv.InvoiceId}", finYear.FinYearNo, finPeriod.FinPeriodNo, inv.InvoiceDate, ct);

        // (3) GL via outbox — per-VAT-line segregation
        await EmitGlAsync(inv, "PurchaseInvoicePosted", false, ct);

        inv.Status = StPosted;
        inv.PostedBy = _ctx.CurrentUserNo();
        inv.PostedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    // ── cancel ─────────────────────────────────────────────────────────────────

    public async Task<Pur1102InvoiceDto> CancelAsync(long invoiceNo, string? reason, CancellationToken ct = default)
    {
        return await _uow.ExecuteAsync(async token => await CancelInternalAsync(invoiceNo, reason, token), ct);
    }

    private async Task<Pur1102InvoiceDto> CancelInternalAsync(long invoiceNo, string? reason, CancellationToken ct)
    {
        var inv = await _db.PurInvoices.FirstOrDefaultAsync(i => i.InvoiceNo == invoiceNo && i.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Invoice not found: {invoiceNo}");
        if (inv.Status != StPosted) throw new ValidationException("Only a Posted invoice can be cancelled");
        if (inv.ReturnedAmount > 0) throw new ValidationException("Invoice has returns — reverse those first");

        // Period guard
        var invDay = DateOnly.FromDateTime(inv.InvoiceDate);
        var finYear = await _calendar.FindYearForDateAsync(inv.CompanyNo, invDay, ct)
            ?? throw new ValidationException($"No financial year for date {inv.InvoiceDate:yyyy-MM-dd}");
        var finPeriod = await _calendar.FindPeriodForDateAsync(finYear.FinYearNo, invDay, ct)
            ?? throw new ValidationException($"No financial period for date {inv.InvoiceDate:yyyy-MM-dd}");
        if (finPeriod.PeriodStatus != 1)
            throw new ValidationException($"Cannot cancel: period '{finPeriod.FinPeriodName}' is not Open");

        // (1) Reverse stock — create OUT legs to reverse the original IN
        var lines = await _db.PurInvoiceDtls.Where(l => l.InvoiceNo == invoiceNo && l.IsDeleted == 0)
            .OrderBy(l => l.LineNo).ToListAsync(ct);

        var reversalLegs = lines.Select(l =>
            StockPostingLeg.Out(inv.WarehouseNo, l.ProductNo, l.VariantNo, l.BatchNo,
                l.QtyBase, MvPurchase, l.InvoiceDtlNo)).ToList();

        var cmd = new StockPostingCommand(inv.CompanyNo, inv.BranchNo, RefPurInv,
            inv.InvoiceId, inv.InvoiceNo, inv.InvoiceDate, finYear.FinYearNo, null, true, reversalLegs);
        await _stockPostingService.PostAsync(cmd, ct);

        // (2) Reverse AP
        await _apLedger.DebitAsync(inv.SupplierNo, inv.GrandTotal, ApRefAdj, inv.InvoiceId, inv.InvoiceNo,
            $"Invoice cancelled {inv.InvoiceId}", finYear.FinYearNo, finPeriod.FinPeriodNo, inv.InvoiceDate, ct);

        // (3) Reversing GL
        await EmitGlAsync(inv, "PurchaseInvoiceReversed", true, ct);

        inv.Status = StCancelled;
        inv.CancelledBy = _ctx.CurrentUserNo();
        inv.CancelledAt = DateTime.UtcNow;
        inv.CancelReason = reason;
        inv.DueAmount = 0;
        await _db.SaveChangesAsync(ct);

        return await GetDetailAsync(invoiceNo, ct);
    }

    public async Task DeleteAsync(long invoiceNo, CancellationToken ct = default)
    {
        var inv = await _db.PurInvoices.FirstOrDefaultAsync(i => i.InvoiceNo == invoiceNo && i.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Invoice not found: {invoiceNo}");
        if (inv.Status == StPosted || inv.Status == StReturned)
            throw new ValidationException("A posted invoice cannot be deleted — cancel it instead");

        var lines = await _db.PurInvoiceDtls.Where(l => l.InvoiceNo == invoiceNo && l.IsDeleted == 0).ToListAsync(ct);
        foreach (var l in lines) { l.IsDeleted = 1; l.DeletedBy = _ctx.CurrentUserNo(); l.DeletedAt = DateTime.UtcNow; }

        inv.IsDeleted = 1; inv.IsActive = 0;
        inv.DeletedBy = _ctx.CurrentUserNo(); inv.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    // ── GL emit ────────────────────────────────────────────────────────────────

    private async Task EmitGlAsync(PurInvoice inv, string eventType, bool reverse, CancellationToken ct)
    {
        decimal grand = inv.GrandTotal;
        if (grand <= 0) return;

        // Group VAT by VatTaxNo for per-rate GL segregation
        var lines = await _db.PurInvoiceDtls.AsNoTracking()
            .Where(l => l.InvoiceNo == inv.InvoiceNo && l.IsDeleted == 0)
            .ToListAsync(ct);

        var vatByTax = lines
            .Where(l => l.VatTaxNo.HasValue && l.TaxAmount > 0)
            .GroupBy(l => l.VatTaxNo!.Value)
            .Select(g => new { VatTaxNo = g.Key, Amount = g.Sum(x => x.TaxAmount) })
            .ToList();

        var taxNos = vatByTax.Select(v => v.VatTaxNo).ToList();
        var taxCodes = await _vatTaxLookup.GetTaxCodesAsync(taxNos, ct);

        decimal taxTotal = vatByTax.Sum(v => v.Amount);
        decimal inventory = inv.SubTotal;

        var purLegs = new List<GlPostingPayload.Leg>();

        if (inventory > 0)
            purLegs.Add(new() { LegKey = "INVENTORY", Amount = inventory, DrCr = reverse ? "cr" : "dr" });

        // One VAT_INPUT leg per tax code, with SubKey = tax code string (e.g. VAT-15)
        foreach (var vat in vatByTax)
        {
            var subKey = taxCodes.TryGetValue(vat.VatTaxNo, out var code) ? code : vat.VatTaxNo.ToString();
            purLegs.Add(new() { LegKey = "VAT_INPUT", SubKey = subKey, Amount = vat.Amount, DrCr = reverse ? "cr" : "dr" });
        }
        // Fallback if no per-line VAT
        if (vatByTax.Count == 0 && taxTotal > 0)
            purLegs.Add(new() { LegKey = "VAT_INPUT", Amount = taxTotal, DrCr = reverse ? "cr" : "dr" });

        purLegs.Add(new() { LegKey = "PAYABLE", Amount = grand, DrCr = reverse ? "dr" : "cr", PartyType = 2, PartyNo = inv.SupplierNo });

        var payload = new GlPostingPayload
        {
            VoucherDate = inv.InvoiceDate,
            Narration = (reverse ? "Purchase invoice reversal " : "Purchase invoice ") + inv.InvoiceId,
            BranchNo = inv.BranchNo,
            Legs = purLegs
        };

        var ev = new EventOutbox
        {
            CompanyNo = inv.CompanyNo,
            BranchNo = inv.BranchNo,
            EventType = eventType,
            AggregateType = "PUR_INVOICE",
            AggregateId = inv.InvoiceNo.ToString(),
            Payload = JsonSerializer.Serialize(payload),
            Status = 1,
            CreatedAt = DateTime.UtcNow
        };
        _db.EventOutboxes.Add(ev);
        await _db.SaveChangesAsync(ct);
    }

    // ── helpers ────────────────────────────────────────────────────────────────

    private static short PaymentStatus(PurInvoice inv)
    {
        if (inv.PaidAmount <= 0) return 1;
        return inv.PaidAmount >= inv.GrandTotal ? (short)3 : (short)2;
    }

    /// <summary>
    /// Loads an invoice <b>tracked</b>. SubmitAsync and RejectAsync mutate what this returns, and
    /// with AsNoTracking those mutations went to a detached entity — SaveChanges wrote nothing
    /// while the endpoint still returned 200. Same defect PUR_1101 had on its approval path.
    /// </summary>
    private async Task<PurInvoice> RequireAsync(long invoiceNo, CancellationToken ct)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context required");
        var i = await _db.PurInvoices
            .FirstOrDefaultAsync(x => x.InvoiceNo == invoiceNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Invoice not found: {invoiceNo}");
        if (i.CompanyNo != companyNo) throw new ValidationException("Invoice belongs to another company");
        return i;
    }

    private async Task RequireSupplierAsync(long supplierNo, long companyNo, CancellationToken ct)
    {
        var s = await _db.PurSuppliers.AsNoTracking()
            .FirstOrDefaultAsync(x => x.SupplierNo == supplierNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException("Supplier not found");
        if (s.CompanyNo != companyNo) throw new ValidationException("Supplier not in your company");
    }

    private async Task<List<Pur1102LineDto>> GetLinesAsync(long invoiceNo, CancellationToken ct)
    {
        var lines = await _db.PurInvoiceDtls.AsNoTracking()
            .Where(l => l.InvoiceNo == invoiceNo && l.IsDeleted == 0)
            .OrderBy(l => l.LineNo)
            .ToListAsync(ct);

        var productNos = lines.Select(l => l.ProductNo).Distinct().ToList();
        var products = await _catalog.GetProductNamesAsync(productNos, ct);
        var uoms = await _catalog.GetUomNamesAsync(lines.Select(l => l.UomNo).Distinct().ToList(), ct);

        return lines.Select(l => new Pur1102LineDto
        {
            InvoiceDtlNo = l.InvoiceDtlNo,
            LineNo = l.LineNo,
            ProductNo = l.ProductNo,
            UomNo = l.UomNo,
            UomName = uoms.GetValueOrDefault(l.UomNo),
            Qty = l.Qty,
            QtyBase = l.QtyBase,
            FinalUnitCost = l.FinalUnitCost,
            UnitPrice = l.UnitPrice,
            LineTotal = l.LineTotal,
            DiscountAmount = l.DiscountAmount,
            TaxAmount = l.TaxAmount,
            NetAmount = l.TaxableAmount,
            VatTaxNo = l.VatTaxNo,
            TaxRatePct = l.TaxRatePct,
            Remarks = l.Remarks,
            ProductName = products.GetValueOrDefault(l.ProductNo)
        }).ToList();
    }

    private static Pur1102InvoiceDto ToDto(PurInvoice i, string? supName, List<Pur1102LineDto>? lines) => new()
    {
        InvoiceNo = i.InvoiceNo,
        InvoiceId = i.InvoiceId,
        SupplierInvoiceNo = i.SupplierInvoiceNo,
        InvoiceDate = i.InvoiceDate,
        SupplierNo = i.SupplierNo,
        SupplierName = supName,
        WarehouseNo = i.WarehouseNo,
        OrderNo = i.OrderNo,
        SubTotal = i.SubTotal,
        DiscountTotal = i.DiscountTotal,
        TaxableAmount = i.TaxableAmount,
        TaxAmount = i.TaxAmount,
        ShippingCharge = i.ShippingCharge,
        OtherCharges = i.OtherCharges,
        RoundOff = i.RoundOff,
        GrandTotal = i.GrandTotal,
        PaidAmount = i.PaidAmount,
        DueAmount = i.DueAmount,
        PaymentStatus = i.PaymentStatus,
        Status = i.Status,
        DueDate = i.DueDate,
        Remarks = i.Remarks,
        BranchNo = i.BranchNo,
        IsActive = i.IsActive ?? 0,
        RowVersion = i.RowVersion,
        ApprovalRequestNo = i.ApprovalRequestNo,
        Lines = lines ?? new()
    };
}
