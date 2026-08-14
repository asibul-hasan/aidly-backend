using AidlyErp.Shared.Core;
using AidlyErp.Sal.Application.Interfaces;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Abstractions;
using AidlyErp.Shared.Contracts;
using AidlyErp.Sys.Contracts;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Fin.Contracts;
using AidlyErp.Inv.Contracts;
using AidlyErp.Sal.Application.Dto;
using AidlyErp.Sal.Domain;

namespace AidlyErp.Sal.Application.Services;

public interface ISal1001Service
{
    Task<Sal1001LookupDto> GetLookupsAsync(CancellationToken ct = default);
    Task<List<Sal1001InvoiceDto>> GetListAsync(CancellationToken ct = default);
    Task<Sal1001InvoiceDto> GetDetailAsync(long invoiceNo, CancellationToken ct = default);
    Task<Sal1001InvoiceDto> SaveAsync(Sal1001InvoiceDto dto, CancellationToken ct = default);
    Task<Sal1001InvoiceDto> SubmitAsync(long invoiceNo, CancellationToken ct = default);
    Task<Sal1001InvoiceDto> RejectAsync(long invoiceNo, CancellationToken ct = default);
    Task<Sal1001InvoiceDto> PostAsync(long invoiceNo, CancellationToken ct = default);
    Task<Sal1001InvoiceDto> ConfirmPosSaleAsync(SalPosSaleRequestDto request, CancellationToken ct = default);
    Task<Sal1001InvoiceDto> CancelAsync(long invoiceNo, string? reason, CancellationToken ct = default);
    Task DeleteAsync(long invoiceNo, CancellationToken ct = default);
    Task ApplyApprovalOutcomeAsync(long invoiceNo, bool approved, CancellationToken ct = default);
}

public class Sal1001Service : ISal1001Service
{
    private readonly ISalDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    private readonly ISalArLedgerService _arLedger;
    private readonly IApprovalService _approvalService;
    private readonly IVatTaxLookup _vatTaxLookup;
    private readonly IInvLookup _invLookup;
    private readonly IInvCatalog _catalog;
    private readonly IInvStockPostingService _stockPostingService;
    private readonly IFinCalendar _calendar;

    private readonly ISalPricingService _pricing;
    private readonly ISalPosService _posService;
    private readonly ISalPromotionEngine _promotions;

    public const string DocType = "SAL_INVOICE";
    private const short StDraft = 1, StPosted = 2, StCancelled = 3;

    /// <summary><c>inv_stock_ledger.ref_doc_type</c> for a sale, and its movement type.</summary>
    private const short RefSalInv = 4, MvSale = 4;

    /// <summary><c>sale_type</c> 1 = rung up at a till, as opposed to a back-office credit invoice.</summary>
    private const short SaleTypePos = 1;

    private const short MethodCash = 1;

    public Sal1001Service(ISalDbContext db, ICompanyBranchContext ctx, ISalArLedgerService arLedger,
                          IApprovalService approvalService, IVatTaxLookup vatTaxLookup, IInvLookup invLookup,
                          IInvCatalog catalog, IInvStockPostingService stockPostingService, IFinCalendar calendar,
                          ISalPricingService pricing, ISalPosService posService,
                          ISalPromotionEngine promotions)
    {
        _promotions = promotions;
        _db = db;
        _ctx = ctx;
        _arLedger = arLedger;
        _approvalService = approvalService;
        _vatTaxLookup = vatTaxLookup;
        _invLookup = invLookup;
        _catalog = catalog;
        _stockPostingService = stockPostingService;
        _calendar = calendar;
        _pricing = pricing;
        _posService = posService;
    }

    public async Task<List<Sal1001InvoiceDto>> GetListAsync(CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? 0;
        var custs = await _db.SalCustomers.AsNoTracking().Where(c => c.IsDeleted == 0).ToDictionaryAsync(c => c.CustomerNo, c => c.CustomerName, ct);

        var list = await _db.SalInvoices
            .AsNoTracking()
            .Where(i => i.CompanyNo == companyNo && i.IsDeleted == 0)
            .OrderByDescending(i => i.InvoiceNo)
            .ToListAsync(ct);

        return list.Select(i => ToDto(i, custs.ContainsKey(i.CustomerNo) ? custs[i.CustomerNo] : null, null)).ToList();
    }

    public async Task<Sal1001InvoiceDto> GetDetailAsync(long invoiceNo, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? 0;
        var inv = await _db.SalInvoices.AsNoTracking().FirstOrDefaultAsync(i => i.InvoiceNo == invoiceNo && i.CompanyNo == companyNo && i.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Invoice not found: {invoiceNo}");

        var custName = await _db.SalCustomers.Where(c => c.CustomerNo == inv.CustomerNo).Select(c => c.CustomerName).FirstOrDefaultAsync(ct);
        var lines = await GetLinesAsync(invoiceNo, ct);

        return ToDto(inv, custName, lines);
    }

    public async Task<Sal1001InvoiceDto> SaveAsync(Sal1001InvoiceDto dto, CancellationToken ct = default)
    {
        return dto.InvoiceNo.HasValue && dto.InvoiceNo.Value > 0
            ? await UpdateAsync(dto.InvoiceNo.Value, dto, ct)
            : await InsertAsync(dto, ct);
    }

    private async Task<Sal1001InvoiceDto> InsertAsync(Sal1001InvoiceDto dto, CancellationToken ct)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
        long branchNo = dto.BranchNo ?? _ctx.CurrentBranchNo() ?? throw new ValidationException("Branch context is required");

        if (dto.CustomerNo <= 0) throw new ValidationException("Customer is required");
        // Defaulting to warehouse 1 would silently relieve stock from the wrong store.
        if (dto.WarehouseNo <= 0) throw new ValidationException("Warehouse is required");
        if (dto.Lines == null || dto.Lines.Count == 0) throw new ValidationException("Invoice lines are required");

        decimal subTotal = dto.Lines.Sum(l => l.Qty * l.UnitPrice);
        decimal taxAmount = dto.Lines.Sum(l => l.TaxAmount);
        decimal grandTotal = subTotal + taxAmount + dto.ShippingCharge + dto.RoundOff - dto.LineDiscountTotal;
        decimal dueAmount = grandTotal - dto.PaidAmount;

        var inv = new SalInvoice
        {
            CompanyNo = companyNo,
            BranchNo = branchNo,
            InvoiceId = $"INV-{DateTime.UtcNow:yyyyMMddHHmmss}",
            InvoiceDate = dto.InvoiceDate != default ? dto.InvoiceDate : DateTime.UtcNow.Date,
            SaleType = dto.SaleType,
            CustomerNo = dto.CustomerNo,
            WarehouseNo = dto.WarehouseNo,
            Status = StDraft,
            SubTotal = subTotal,
            LineDiscountTotal = dto.LineDiscountTotal,
            TaxableAmount = subTotal - dto.LineDiscountTotal,
            TaxAmount = taxAmount,
            ShippingCharge = dto.ShippingCharge,
            RoundOff = dto.RoundOff,
            GrandTotal = grandTotal,
            PaidAmount = dto.PaidAmount,
            DueAmount = dueAmount,
            PaymentStatus = dueAmount <= 0 ? (short)3 : (dto.PaidAmount > 0 ? (short)2 : (short)1),
            DueDate = dto.DueDate ?? DateTime.UtcNow.AddDays(30),
            Remarks = dto.Remarks,
            IsActive = 1, IsDeleted = 0,
            CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow
        };

        _db.SalInvoices.Add(inv);
        await _db.SaveChangesAsync(ct);

        inv.InvoiceId = $"INV-{inv.InvoiceNo:D6}";
        await ReplaceLinesAsync(inv.InvoiceNo, companyNo, dto.Lines, ct);
        await _db.SaveChangesAsync(ct);

        return await GetDetailAsync(inv.InvoiceNo, ct);
    }

    private async Task<Sal1001InvoiceDto> UpdateAsync(long invoiceNo, Sal1001InvoiceDto dto, CancellationToken ct)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
        var inv = await _db.SalInvoices.FirstOrDefaultAsync(i => i.InvoiceNo == invoiceNo && i.CompanyNo == companyNo && i.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Invoice not found: {invoiceNo}");

        if (inv.Status != StDraft) throw new ValidationException("Only a Draft invoice can be edited");

        if (dto.Lines != null && dto.Lines.Count > 0)
        {
            decimal subTotal = dto.Lines.Sum(l => l.Qty * l.UnitPrice);
            decimal taxAmount = dto.Lines.Sum(l => l.TaxAmount);
            decimal grandTotal = subTotal + taxAmount + dto.ShippingCharge + dto.RoundOff - dto.LineDiscountTotal;

            inv.SubTotal = subTotal;
            inv.LineDiscountTotal = dto.LineDiscountTotal;
            inv.TaxableAmount = subTotal - dto.LineDiscountTotal;
            inv.TaxAmount = taxAmount;
            inv.ShippingCharge = dto.ShippingCharge;
            inv.RoundOff = dto.RoundOff;
            inv.GrandTotal = grandTotal;
            inv.DueAmount = grandTotal - inv.PaidAmount;
            inv.PaymentStatus = inv.DueAmount <= 0 ? (short)3 : (inv.PaidAmount > 0 ? (short)2 : (short)1);

            await ReplaceLinesAsync(invoiceNo, companyNo, dto.Lines, ct);
        }

        if (dto.Remarks != null) inv.Remarks = dto.Remarks;
        inv.UpdatedBy = _ctx.CurrentUserNo(); inv.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return await GetDetailAsync(invoiceNo, ct);
    }

    /// <summary>
    /// Port of Java Sal1001Service.getLookups. Customers come from SAL's own context;
    /// warehouses, products and UOMs come through IInvLookup, because ISalDbContext
    /// carries no INV entity sets and referencing AidlyErp.Inv.Domain from here would
    /// break the module boundary tests.
    /// </summary>
    public async Task<Sal1001LookupDto> GetLookupsAsync(CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
        long? branchNo = _ctx.CurrentBranchNo();

        var customers = await _db.SalCustomers.AsNoTracking()
            .Where(c => c.CompanyNo == companyNo && c.IsDeleted == 0 && (c.IsActive == null || c.IsActive == 1))
            .OrderBy(c => c.CustomerId)
            .Select(c => new SalOptionDto { No = c.CustomerNo, Name = c.CustomerId + " — " + c.CustomerName })
            .ToListAsync(ct);

        var warehouses = await _invLookup.GetWarehousesAsync(companyNo, branchNo, ct);
        var products = await _invLookup.GetProductsAsync(companyNo, ct);
        var uoms = await _invLookup.GetUomsAsync(companyNo, ct);

        return new Sal1001LookupDto
        {
            Customers = customers,
            Warehouses = warehouses.Select(o => new SalOptionDto { No = o.No, Name = o.Name }).ToList(),
            Products = products.Select(o => new SalOptionDto { No = o.No, Name = o.Name }).ToList(),
            Uoms = uoms.Select(o => new SalOptionDto { No = o.No, Name = o.Name }).ToList()
        };
    }

    /// <summary>
    /// Port of Java Sal1001Service.reject. Acts on the live approval request rather than
    /// touching the invoice directly — the engine owns the state machine and fires the
    /// completion event that routes back through ApplyApprovalOutcomeAsync.
    /// </summary>
    public async Task<Sal1001InvoiceDto> RejectAsync(long invoiceNo, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
        var inv = await _db.SalInvoices.FirstOrDefaultAsync(i => i.InvoiceNo == invoiceNo && i.CompanyNo == companyNo && i.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Invoice not found: {invoiceNo}");

        if (!inv.ApprovalRequestNo.HasValue)
            throw new ValidationException("No approval request — submit the invoice first");

        await _approvalService.ActAsync(inv.ApprovalRequestNo.Value, false, "Rejected by user", ct);
        inv.ApprovalRequestNo = null;
        inv.UpdatedBy = _ctx.CurrentUserNo(); inv.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return await GetDetailAsync(invoiceNo, ct);
    }

    public async Task<Sal1001InvoiceDto> SubmitAsync(long invoiceNo, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
        var inv = await _db.SalInvoices.FirstOrDefaultAsync(i => i.InvoiceNo == invoiceNo && i.CompanyNo == companyNo && i.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Invoice not found: {invoiceNo}");

        if (inv.Status != StDraft) throw new ValidationException("Only a Draft invoice can be submitted");

        var outcome = await _approvalService.RaiseAsync(DocType, inv.InvoiceNo, inv.InvoiceId, inv.GrandTotal, ct);
        if (outcome.AutoApproved)
        {
            await PostInternalAsync(inv, ct);
        }
        else
        {
            inv.ApprovalRequestNo = outcome.ApprovalRequestNo;
            await _db.SaveChangesAsync(ct);
        }

        return await GetDetailAsync(invoiceNo, ct);
    }

    public async Task<Sal1001InvoiceDto> PostAsync(long invoiceNo, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
        var inv = await _db.SalInvoices.FirstOrDefaultAsync(i => i.InvoiceNo == invoiceNo && i.CompanyNo == companyNo && i.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Invoice not found: {invoiceNo}");

        await PostInternalAsync(inv, ct);
        return await GetDetailAsync(invoiceNo, ct);
    }

    /// <summary>
    /// Rings up a POS sale in one call: price → create → tender → post. Everything monetary is
    /// derived here from the catalogue; the till's job is to say what was scanned and how it was paid.
    /// </summary>
    public async Task<Sal1001InvoiceDto> ConfirmPosSaleAsync(SalPosSaleRequestDto request,
                                                             CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
        long branchNo = _ctx.CurrentBranchNo() ?? throw new ValidationException("Branch context is required");

        if (request.ClientUuid == Guid.Empty) throw new ValidationException("client_uuid is required");
        if (request.CustomerNo <= 0) throw new ValidationException("Customer is required");
        if (request.Tenders is null || request.Tenders.Count == 0) throw new ValidationException("Add at least one tender");

        // Replay of a sale we already rang up: hand back the original rather than selling twice.
        var existing = await _db.SalInvoices.AsNoTracking()
            .FirstOrDefaultAsync(i => i.ClientUuid == request.ClientUuid, ct);
        if (existing is not null) return await GetDetailAsync(existing.InvoiceNo, ct);

        // Blocks rather than auto-opening: the cashier is told to open a drawer first.
        var session = await _posService.RequireOpenSessionAsync(request.TerminalNo, ct);

        var terminal = await _db.SalPosTerminals.AsNoTracking()
            .FirstOrDefaultAsync(t => t.TerminalNo == request.TerminalNo && t.IsDeleted == 0, ct)
            ?? throw new NotFoundException("Terminal not found");

        var priced = await _pricing.QuoteAsync(new SalQuotePriceRequestDto
        {
            CustomerNo = request.CustomerNo,
            BillDiscountType = request.BillDiscountType,
            BillDiscountValue = request.BillDiscountValue,
            ShippingCharge = request.ShippingCharge,
            AllowBelowMinPrice = request.AllowBelowMinPrice,
            Lines = request.Lines
        }, ct);

        decimal paid = request.Tenders.Sum(t => t.Amount);
        if (paid < 0) throw new ValidationException("Tendered amount cannot be negative");
        if (paid > priced.GrandTotal)
            throw new ValidationException("Tendered amount exceeds the sale total — record the excess as change, not tender");

        decimal due = priced.GrandTotal - paid;

        // Change is only meaningful against cash; a card is never over-tendered.
        decimal cashTendered = request.Tenders.Where(t => t.PaymentMethod == MethodCash)
                                              .Sum(t => t.TenderedAmount ?? t.Amount);
        decimal cashApplied = request.Tenders.Where(t => t.PaymentMethod == MethodCash).Sum(t => t.Amount);
        decimal change = Math.Max(0m, cashTendered - cashApplied);

        var now = DateTime.UtcNow;

        // Resolved before the insert, not during posting: sal_invoice.fin_year_no is NOT NULL, so
        // without this the row can never be written. Doing it here also means a till outside an
        // open financial year is told so plainly, instead of failing on a constraint.
        var finYear = await _calendar.FindYearForDateAsync(companyNo, DateOnly.FromDateTime(now.Date), ct)
            ?? throw new ValidationException($"No financial year covers {now.Date:yyyy-MM-dd} — the sale cannot be recorded");

        var inv = new SalInvoice
        {
            CompanyNo = companyNo,
            BranchNo = branchNo,
            ClientUuid = request.ClientUuid,
            FinYearNo = finYear.FinYearNo,
            InvoiceDate = now.Date,
            InvoiceTime = now,
            SaleType = SaleTypePos,
            CustomerNo = request.CustomerNo,
            WarehouseNo = terminal.WarehouseNo,          // the till's warehouse, never the client's
            TerminalNo = terminal.TerminalNo,
            PosSessionNo = session.SessionNo,
            SalespersonEmployeeNo = request.SalespersonEmployeeNo,
            Status = StDraft,
            SubTotal = priced.SubTotal,
            LineDiscountTotal = priced.LineDiscountTotal,
            BillDiscountType = request.BillDiscountType,
            BillDiscountValue = request.BillDiscountValue,
            BillDiscountAmount = priced.BillDiscountAmount,
            PromotionDiscount = priced.PromotionDiscount,
            TaxableAmount = priced.TaxableAmount,
            TaxAmount = priced.TaxAmount,
            ShippingCharge = priced.ShippingCharge,
            RoundOff = priced.RoundOff,
            GrandTotal = priced.GrandTotal,
            PaidAmount = paid,
            ChangeAmount = change,
            DueAmount = due,
            PaymentStatus = due <= 0 ? (short)3 : (paid > 0 ? (short)2 : (short)1),
            Remarks = request.Remarks,
            IsActive = 1,
            IsDeleted = 0,
            CreatedBy = _ctx.CurrentUserNo(),
            CreatedAt = now
        };

        _db.SalInvoices.Add(inv);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Two submits raced past the read above; uq_sal_inv_uuid settled it. Return the winner
            // rather than surfacing a constraint error for what is a duplicate request.
            _db.SalInvoices.Remove(inv);

            var winner = await _db.SalInvoices.AsNoTracking()
                .FirstOrDefaultAsync(i => i.ClientUuid == request.ClientUuid, ct);
            if (winner is null) throw;

            return await GetDetailAsync(winner.InvoiceNo, ct);
        }

        inv.InvoiceId = $"{terminal.ReceiptPrefix ?? "INV-"}{inv.InvoiceNo:D6}";

        await WritePosLinesAsync(inv.InvoiceNo, priced, ct);
        await WriteTendersAsync(inv.InvoiceNo, request.Tenders, ct);
        await _db.SaveChangesAsync(ct);

        await PostInternalAsync(inv, ct);

        // Running session totals, so the Z-report does not have to re-aggregate every sale.
        session.TotalSales += inv.GrandTotal;
        session.InvoiceCount += 1;
        await _db.SaveChangesAsync(ct);

        // Only now, once the sale is actually posted — a quote must never consume a usage limit.
        if (priced.AppliedPromotions.Count > 0)
            await _promotions.RecordUsageAsync(priced.AppliedPromotions, ct);

        return await GetDetailAsync(inv.InvoiceNo, ct);
    }

    private async Task WritePosLinesAsync(long invoiceNo, SalQuotePriceResultDto priced, CancellationToken ct)
    {
        int lineNo = 1;
        foreach (var p in priced.Lines)
        {
            _db.SalInvoiceDtls.Add(new SalInvoiceDtl
            {
                InvoiceNo = invoiceNo,
                LineNo = lineNo++,
                ProductNo = p.ProductNo,
                VariantNo = p.VariantNo,
                UomNo = p.UomNo,
                Qty = p.Qty,
                QtyBase = p.QtyBase,
                UnitPrice = p.UnitPrice,
                Mrp = p.Mrp,
                LineDiscountAmount = p.LineDiscountAmount + p.BillDiscountShare,
                PromotionNo = p.PromotionNo,
                PromotionDiscount = p.PromotionDiscount,
                IsFreeItem = p.IsFreeItem,
                VatTaxNo = p.VatTaxNo,
                TaxRatePct = p.TaxRatePct,
                IsTaxInclusive = p.IsTaxInclusive,
                TaxableAmount = p.TaxableAmount,
                TaxAmount = p.TaxAmount,
                LineTotal = p.LineTotal,
                LineCost = 0m,                            // filled by the stock engine at post
                Remarks = p.Remarks,
                IsActive = 1,
                IsDeleted = 0,
                CreatedBy = _ctx.CurrentUserNo(),
                CreatedAt = DateTime.UtcNow
            });
        }
    }

    private async Task WriteTendersAsync(long invoiceNo, List<SalTenderDto> tenders, CancellationToken ct)
    {
        int lineNo = 1;
        foreach (var t in tenders)
        {
            if (t.Amount == 0) continue;
            if (t.PaymentMethod is < 1 or > 9) throw new ValidationException("Invalid payment method");

            _db.SalInvoicePayments.Add(new SalInvoicePayment
            {
                InvoiceNo = invoiceNo,
                LineNo = lineNo++,
                PaymentMethod = t.PaymentMethod,
                Amount = t.Amount,
                TenderedAmount = t.TenderedAmount,
                CardLast4 = t.CardLast4,
                CardType = t.CardType,
                ApprovalCode = t.ApprovalCode,
                MobileProvider = t.MobileProvider,
                TxnRef = t.TxnRef,
                BankNo = t.BankNo,
                ChequeNo = t.ChequeNo,
                ChequeDate = t.ChequeDate,
                Remarks = t.Remarks
            });
        }

        await Task.CompletedTask;
    }

    private async Task PostInternalAsync(SalInvoice inv, CancellationToken ct)
    {
        if (inv.Status == StPosted) return;
        if (inv.Status != StDraft) throw new ValidationException("Only a Draft invoice can be posted");

        // Tracked, not AsNoTracking: the engine's per-line cost is written back onto these rows.
        var lines = await _db.SalInvoiceDtls
            .Where(l => l.InvoiceNo == inv.InvoiceNo && l.IsDeleted == 0)
            .OrderBy(l => l.LineNo)
            .ToListAsync(ct);
        if (lines.Count == 0) throw new ValidationException("Nothing to post");

        // (1) Stock OUT — the engine relieves inventory and returns the costed value (COGS).
        var invDay = DateOnly.FromDateTime(inv.InvoiceDate);
        var finYear = await _calendar.FindYearForDateAsync(inv.CompanyNo, invDay, ct)
            ?? throw new ValidationException($"No financial year for date {inv.InvoiceDate:yyyy-MM-dd}");
        var finPeriod = await _calendar.FindPeriodForDateAsync(finYear.FinYearNo, invDay, ct)
            ?? throw new ValidationException($"No financial period for date {inv.InvoiceDate:yyyy-MM-dd}");
        if (finPeriod.PeriodStatus != 1)
            throw new ValidationException($"Cannot post: period '{finPeriod.FinPeriodName}' is not Open");

        var stockLegs = lines.Select(l => StockPostingLeg.Out(
            inv.WarehouseNo, l.ProductNo, l.VariantNo, l.BatchNo, l.QtyBase, MvSale, l.InvoiceDtlNo)).ToList();

        var stockResult = await _stockPostingService.PostAsync(new StockPostingCommand(
            inv.CompanyNo, inv.BranchNo, RefSalInv, inv.InvoiceId, inv.InvoiceNo,
            inv.InvoiceDate, finYear.FinYearNo, null, false, stockLegs), ct);

        foreach (var l in lines)
        {
            decimal lineCost = stockResult.LineCosts.TryGetValue(l.InvoiceDtlNo, out var c) ? c : 0m;
            l.LineCost = lineCost;
            l.UnitCost = l.QtyBase == 0 ? 0m : Math.Round(lineCost / l.QtyBase, 6);
        }
        inv.TotalCost = stockResult.TotalCost;

        // (2) AR for the credit (due) portion.
        if (inv.DueAmount > 0)
        {
            await _arLedger.DebitAsync(inv.CustomerNo, inv.DueAmount, 2, inv.InvoiceNo.ToString(), inv.InvoiceNo, $"Sales Invoice {inv.InvoiceId}", ct);
        }

        // Group VAT by tax code (VatTaxNo) for per-rate GL segregation
        var vatByTax = lines
            .Where(l => l.VatTaxNo.HasValue && l.TaxAmount > 0)
            .GroupBy(l => l.VatTaxNo!.Value)
            .Select(g => new { VatTaxNo = g.Key, Amount = g.Sum(x => x.TaxAmount) })
            .ToList();

        // Batch-load tax codes for SubKey resolution
        var taxNos = vatByTax.Select(v => v.VatTaxNo).ToList();
        var taxCodes = await _vatTaxLookup.GetTaxCodesAsync(taxNos, ct);

        // COGS as costed by the stock engine — not the selling value of the lines.
        decimal totalCogs = stockResult.TotalCost;

        var legs = new List<GlPostingPayload.Leg>
        {
            new() { LegKey = "RECEIVABLE", Amount = inv.DueAmount, DrCr = "dr", PartyType = 1, PartyNo = inv.CustomerNo },
            new() { LegKey = "CASH", Amount = inv.PaidAmount, DrCr = "dr" },
            new() { LegKey = "REVENUE", Amount = inv.SubTotal - inv.LineDiscountTotal, DrCr = "cr" }
        };

        // One VAT_OUTPUT leg per tax code, with SubKey = tax code string
        foreach (var vat in vatByTax)
        {
            var subKey = taxCodes.TryGetValue(vat.VatTaxNo, out var code) ? code : vat.VatTaxNo.ToString();
            legs.Add(new() { LegKey = "VAT_OUTPUT", SubKey = subKey, Amount = vat.Amount, DrCr = "cr" });
        }

        // If no per-line VAT, fall back to aggregated TaxAmount
        if (vatByTax.Count == 0 && inv.TaxAmount > 0)
        {
            legs.Add(new() { LegKey = "VAT_OUTPUT", Amount = inv.TaxAmount, DrCr = "cr" });
        }

        // COGS legs (Dr COGS / Cr INVENTORY)
        if (totalCogs > 0)
        {
            legs.Add(new() { LegKey = "COGS", Amount = totalCogs, DrCr = "dr" });
            legs.Add(new() { LegKey = "INVENTORY", Amount = totalCogs, DrCr = "cr" });
        }

        var payload = new GlPostingPayload
        {
            VoucherDate = inv.InvoiceDate,
            Narration = $"Sales Invoice {inv.InvoiceId}",
            BranchNo = inv.BranchNo,
            Legs = legs
        };

        var ev = new EventOutbox
        {
            CompanyNo = inv.CompanyNo,
            BranchNo = inv.BranchNo,
            EventType = "SalesInvoicePosted",
            AggregateType = "SalInvoice",
            AggregateId = inv.InvoiceNo.ToString(),
            Payload = JsonSerializer.Serialize(payload),
            Status = 1,
            CreatedAt = DateTime.UtcNow
        };
        _db.EventOutboxes.Add(ev);

        inv.Status = StPosted;
        inv.PostedBy = _ctx.CurrentUserNo();
        inv.PostedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
    }

    public async Task ApplyApprovalOutcomeAsync(long invoiceNo, bool approved, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
        var inv = await _db.SalInvoices.FirstOrDefaultAsync(i => i.InvoiceNo == invoiceNo && i.CompanyNo == companyNo && i.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Invoice not found: {invoiceNo}");

        if (approved) await PostInternalAsync(inv, ct);
        else { inv.ApprovalRequestNo = null; await _db.SaveChangesAsync(ct); }
    }

    public async Task<Sal1001InvoiceDto> CancelAsync(long invoiceNo, string? reason, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
        var inv = await _db.SalInvoices.FirstOrDefaultAsync(i => i.InvoiceNo == invoiceNo && i.CompanyNo == companyNo && i.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Invoice not found: {invoiceNo}");

        if (inv.Status == StCancelled) throw new ValidationException("Invoice is already cancelled");
        if (inv.ReturnedAmount > 0) throw new ValidationException("Sale has returns — reverse those first");

        if (inv.Status == StPosted)
        {
            // Put the stock back: an IN leg per line at the cost the sale relieved it at.
            var invDay = DateOnly.FromDateTime(inv.InvoiceDate);
            var finYear = await _calendar.FindYearForDateAsync(inv.CompanyNo, invDay, ct)
                ?? throw new ValidationException($"No financial year for date {inv.InvoiceDate:yyyy-MM-dd}");
            var finPeriod = await _calendar.FindPeriodForDateAsync(finYear.FinYearNo, invDay, ct)
                ?? throw new ValidationException($"No financial period for date {inv.InvoiceDate:yyyy-MM-dd}");
            if (finPeriod.PeriodStatus != 1)
                throw new ValidationException($"Cannot cancel: period '{finPeriod.FinPeriodName}' is not Open");

            var lines = await _db.SalInvoiceDtls.AsNoTracking()
                .Where(l => l.InvoiceNo == invoiceNo && l.IsDeleted == 0)
                .OrderBy(l => l.LineNo)
                .ToListAsync(ct);

            var reversalLegs = lines.Select(l => StockPostingLeg.In(
                inv.WarehouseNo, l.ProductNo, l.VariantNo, l.BatchNo, l.QtyBase, MvSale, l.InvoiceDtlNo,
                l.UnitCost)).ToList();

            if (reversalLegs.Count > 0)
            {
                await _stockPostingService.PostAsync(new StockPostingCommand(
                    inv.CompanyNo, inv.BranchNo, RefSalInv, inv.InvoiceId, inv.InvoiceNo,
                    inv.InvoiceDate, finYear.FinYearNo, null, true, reversalLegs), ct);
            }

            if (inv.DueAmount > 0)
            {
                await _arLedger.CreditAsync(inv.CustomerNo, inv.DueAmount, 5, inv.InvoiceNo.ToString(), inv.InvoiceNo, $"Cancellation of {inv.InvoiceId}", ct);
            }

            // Stock and the AR sub-ledger were both put back above; without this the general ledger
            // keeps the revenue, VAT and COGS of a sale that no longer exists. The sub-ledgers and
            // the control accounts would then disagree by the cancelled amount, permanently.
            await EmitCancellationGlAsync(inv, ct);
        }

        inv.Status = StCancelled;
        inv.CancelledBy = _ctx.CurrentUserNo();
        inv.CancelledAt = DateTime.UtcNow;
        inv.CancelReason = reason;

        await _db.SaveChangesAsync(ct);
        return await GetDetailAsync(invoiceNo, ct);
    }

    /// <summary>
    /// Mirror image of the posting entry, every leg flipped, emitted as its own event type.
    ///
    /// <para>A distinct <c>SalesInvoiceReversed</c> type matters: the posting engine dedupes on
    /// (source_doc_type, source_doc_no), so re-emitting <c>SalesInvoicePosted</c> for the same
    /// invoice would be swallowed as a duplicate and the reversal would never reach the ledger.
    /// This follows the SalesReturnPosted / SalesReturnReversed pair already in use.</para>
    ///
    /// <para>Amounts are read back off the stored invoice and its lines, not recomputed from the
    /// current price list — a reversal has to undo what was actually posted, whatever has changed
    /// since.</para>
    /// </summary>
    private async Task EmitCancellationGlAsync(SalInvoice inv, CancellationToken ct)
    {
        var lines = await _db.SalInvoiceDtls.AsNoTracking()
            .Where(l => l.InvoiceNo == inv.InvoiceNo && l.IsDeleted == 0)
            .ToListAsync(ct);

        decimal totalCogs = lines.Sum(l => l.LineCost);
        decimal revenue = inv.SubTotal - inv.LineDiscountTotal;

        var legs = new List<GlPostingPayload.Leg>();
        if (inv.DueAmount > 0)
            legs.Add(new() { LegKey = "RECEIVABLE", Amount = inv.DueAmount, DrCr = "cr", PartyType = 1, PartyNo = inv.CustomerNo });
        if (inv.PaidAmount > 0)
            legs.Add(new() { LegKey = "CASH", Amount = inv.PaidAmount, DrCr = "cr" });
        if (revenue > 0)
            legs.Add(new() { LegKey = "REVENUE", Amount = revenue, DrCr = "dr" });
        if (inv.TaxAmount > 0)
            legs.Add(new() { LegKey = "VAT_OUTPUT", Amount = inv.TaxAmount, DrCr = "dr" });
        if (totalCogs > 0)
        {
            legs.Add(new() { LegKey = "COGS", Amount = totalCogs, DrCr = "cr" });
            legs.Add(new() { LegKey = "INVENTORY", Amount = totalCogs, DrCr = "dr" });
        }

        if (legs.Count == 0) return;

        var payload = new GlPostingPayload
        {
            VoucherDate = inv.InvoiceDate,
            Narration = $"Cancellation of Sales Invoice {inv.InvoiceId}",
            BranchNo = inv.BranchNo,
            Legs = legs
        };

        _db.EventOutboxes.Add(new EventOutbox
        {
            CompanyNo = inv.CompanyNo,
            BranchNo = inv.BranchNo,
            EventType = "SalesInvoiceReversed",
            AggregateType = "SalInvoice",
            AggregateId = inv.InvoiceNo.ToString(),
            Payload = JsonSerializer.Serialize(payload),
            Status = 1,
            CreatedAt = DateTime.UtcNow
        });
    }

    public async Task DeleteAsync(long invoiceNo, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
        var inv = await _db.SalInvoices.FirstOrDefaultAsync(i => i.InvoiceNo == invoiceNo && i.CompanyNo == companyNo && i.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Invoice not found: {invoiceNo}");

        if (inv.Status != StDraft) throw new ValidationException("Only a Draft invoice can be deleted");

        inv.IsDeleted = 1; inv.IsActive = 0;
        inv.DeletedBy = _ctx.CurrentUserNo(); inv.DeletedAt = DateTime.UtcNow;

        var lines = await _db.SalInvoiceDtls.Where(l => l.InvoiceNo == invoiceNo && l.IsDeleted == 0).ToListAsync(ct);
        foreach (var l in lines) { l.IsDeleted = 1; l.IsActive = 0; l.DeletedBy = _ctx.CurrentUserNo(); l.DeletedAt = DateTime.UtcNow; }

        await _db.SaveChangesAsync(ct);
    }

    private async Task ReplaceLinesAsync(long invoiceNo, long companyNo, List<Sal1001LineDto> lines,
                                         CancellationToken ct)
    {
        var existing = await _db.SalInvoiceDtls.Where(l => l.InvoiceNo == invoiceNo && l.IsDeleted == 0).ToListAsync(ct);
        foreach (var e in existing) { e.IsDeleted = 1; e.DeletedBy = _ctx.CurrentUserNo(); e.DeletedAt = DateTime.UtcNow; }

        var live = lines.Where(l => l.ProductNo > 0 && l.Qty > 0).ToList();
        var products = await _catalog.GetProductsAsync(live.Select(l => l.ProductNo).Distinct().ToList(), companyNo, ct);
        var factors = await _catalog.GetUomFactorsAsync(products.Keys.ToList(), ct);

        int lineNo = 1;
        foreach (var dto in live)
        {
            if (!products.TryGetValue(dto.ProductNo, out var product))
                throw new NotFoundException($"Product not found: {dto.ProductNo}");

            var lineTotal = dto.Qty * dto.UnitPrice;
            long uomNo = dto.UomNo ?? product.BaseUomNo;

            // Money is derived here, not taken from the client. The form sends a discount rate and a
            // tax rate; the amounts it shows are a preview. Trusting them would let a crafted request
            // set its own discount, and would silently disagree with the ledger the moment the
            // browser's arithmetic differed from ours by a rounding step.
            decimal discountAmount = dto.LineDiscountAmount > 0m
                ? dto.LineDiscountAmount
                : Math.Round(lineTotal * dto.LineDiscountPct / 100m, 4);
            decimal taxableAmount = lineTotal - discountAmount;
            decimal taxAmount = Math.Round(taxableAmount * dto.TaxRatePct / 100m, 4);

            var line = new SalInvoiceDtl
            {
                InvoiceNo = invoiceNo,
                LineNo = lineNo++,
                ProductNo = dto.ProductNo,
                UomNo = uomNo,
                Qty = dto.Qty,
                // Stock is held in the base UOM; selling a carton must relieve every piece in it.
                QtyBase = ToBaseQty(product, uomNo, dto.Qty, factors),
                UnitPrice = dto.UnitPrice,
                UnitCost = dto.UnitCost,
                LineTotal = lineTotal,
                LineDiscountPct = dto.LineDiscountPct,
                LineDiscountAmount = discountAmount,
                TaxableAmount = taxableAmount,
                TaxRatePct = dto.TaxRatePct,
                TaxAmount = taxAmount,
                // LineCost is COGS and is filled by the stock engine at post time, not from the
                // selling price — leave it at zero until then.
                LineCost = 0m,
                Remarks = dto.Remarks,
                IsActive = 1, IsDeleted = 0,
                CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow
            };
            _db.SalInvoiceDtls.Add(line);
        }
    }

    /// <summary>Line quantity converted to the product's base UOM, mirroring Java's <c>toBaseQty</c>.</summary>
    private static decimal ToBaseQty(ProductInfo product, long uomNo, decimal qty,
                                     IReadOnlyDictionary<(long ProductNo, long UomNo), decimal> factors)
    {
        if (uomNo <= 0) throw new ValidationException("UOM is required");
        if (uomNo == product.BaseUomNo) return qty;

        if (!factors.TryGetValue((product.ProductNo, uomNo), out decimal factor))
            throw new ValidationException($"No UOM conversion defined for product {product.ProductId}");

        return qty * factor;
    }

    private async Task<List<Sal1001LineDto>> GetLinesAsync(long invoiceNo, CancellationToken ct)
    {
        var lines = await ProjectLinesAsync(invoiceNo, ct);
        await FillNamesAsync(lines, ct);
        return lines;
    }

    /// <summary>
    /// Resolves product and UOM names for a set of lines in two queries, not two per line.
    /// Without this the grid renders a document with blank product and unit columns.
    /// </summary>
    private async Task FillNamesAsync(List<Sal1001LineDto> lines, CancellationToken ct)
    {
        if (lines.Count == 0) return;

        var productNames = await _catalog.GetProductNamesAsync(
            lines.Select(l => l.ProductNo).Distinct().ToList(), ct);
        var uomNames = await _catalog.GetUomNamesAsync(
            lines.Where(l => l.UomNo.HasValue).Select(l => l.UomNo!.Value).Distinct().ToList(), ct);

        foreach (var l in lines)
        {
            l.ProductName = productNames.GetValueOrDefault(l.ProductNo);
            if (l.UomNo.HasValue) l.UomName = uomNames.GetValueOrDefault(l.UomNo.Value);
        }
    }

    private async Task<List<Sal1001LineDto>> ProjectLinesAsync(long invoiceNo, CancellationToken ct)
    {
        return await _db.SalInvoiceDtls
            .AsNoTracking()
            .Where(l => l.InvoiceNo == invoiceNo && l.IsDeleted == 0)
            .Select(l => new Sal1001LineDto
            {
                InvoiceDtlNo = l.InvoiceDtlNo,
                LineNo = l.LineNo,
                ProductNo = l.ProductNo,
                UomNo = l.UomNo,
                Qty = l.Qty,
                QtyBase = l.QtyBase,
                UnitPrice = l.UnitPrice,
                UnitCost = l.UnitCost,
                LineTotal = l.LineTotal,
                LineDiscountPct = l.LineDiscountPct,
                LineDiscountAmount = l.LineDiscountAmount,
                TaxableAmount = l.TaxableAmount,
                TaxRatePct = l.TaxRatePct,
                TaxAmount = l.TaxAmount,
                // Net of discount and inclusive of tax — what the line actually adds to the bill.
                NetAmount = l.TaxableAmount + l.TaxAmount,
                Remarks = l.Remarks
            })
            .ToListAsync(ct);
    }

    private static Sal1001InvoiceDto ToDto(SalInvoice i, string? customerName, List<Sal1001LineDto>? lines) => new()
    {
        InvoiceNo = i.InvoiceNo,
        InvoiceId = i.InvoiceId,
        InvoiceDate = i.InvoiceDate,
        SaleType = i.SaleType,
        CustomerNo = i.CustomerNo,
        CustomerName = customerName,
        WarehouseNo = i.WarehouseNo,
        Status = i.Status,
        SubTotal = i.SubTotal,
        LineDiscountTotal = i.LineDiscountTotal,
        TaxableAmount = i.TaxableAmount,
        TaxAmount = i.TaxAmount,
        ShippingCharge = i.ShippingCharge,
        RoundOff = i.RoundOff,
        GrandTotal = i.GrandTotal,
        PaidAmount = i.PaidAmount,
        DueAmount = i.DueAmount,
        TotalCost = i.TotalCost,
        PaymentStatus = i.PaymentStatus,
        DueDate = i.DueDate,
        FinYearNo = i.FinYearNo,
        GlVoucherNo = i.GlVoucherNo,
        ApprovalRequestNo = i.ApprovalRequestNo,
        Remarks = i.Remarks,
        IsActive = i.IsActive ?? 0,
        RowVersion = i.RowVersion,
        Lines = lines ?? new()
    };
}
