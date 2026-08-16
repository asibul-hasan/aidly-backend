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

public interface ISal1103Service
{
    Task<List<Sal1103ReturnDto>> GetListAsync(CancellationToken ct = default);
    Task<Sal1103ReturnDto> GetDetailAsync(long returnNo, CancellationToken ct = default);
    Task<Sal1103ReturnDto> SaveAsync(Sal1103ReturnDto dto, CancellationToken ct = default);
    Task<Sal1103ReturnDto> PostAsync(long returnNo, CancellationToken ct = default);
    Task<Sal1103ReturnDto> CancelAsync(long returnNo, string? reason, CancellationToken ct = default);
    Task DeleteAsync(long returnNo, CancellationToken ct = default);
}

public class Sal1103Service : ISal1103Service
{
    private readonly ISalDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    private readonly ISalArLedgerService _arLedger;

    private const short StDraft = 1, StPosted = 2, StCancelled = 3;

    /// <summary>refund_method 2 = settled against the customer's outstanding due (Java REFUND_AGAINST_DUE).</summary>
    // sal_return.refund_method: 1=Cash 2=Card 3=Mobile 4=Bank 5=StoreCredit 6=AgainstDue.
    // This was 2 (Card), so card refunds credited the customer's receivable as though the debt had
    // been reduced, while genuine against-due refunds never touched it — wrong in both directions.
    private const short RefundAgainstDue = 6;

    /// <summary>sal_return_dtl.restock_flag — 1 puts the goods back into sellable stock.</summary>
    private const short RestockYes = 1;

    /// <summary>AR ledger ref_doc_type for a return adjustment (Java AR_REF_ADJ).</summary>
    private const short ArRefAdjustment = 5;

    /// <summary>AR ledger ref_doc_type for the return itself (Java AR_REF_RETURN).</summary>
    private const short ArRefReturn = 4;

    /// <summary><c>inv_stock_ledger.ref_doc_type</c> for a sales return, and its movement type.</summary>
    private const short RefSalRet = 5, MvSaleReturn = 5;

    private readonly IInvCatalog _catalog;
    private readonly IInvStockPostingService _stockPostingService;
    private readonly IFinCalendar _calendar;

    private readonly IDocSequenceGenerator _docSeq;
    private const string DocSeqReturn = "SAL_RETURN";

    public Sal1103Service(ISalDbContext db, ICompanyBranchContext ctx, ISalArLedgerService arLedger,
                          IInvCatalog catalog, IInvStockPostingService stockPostingService,
                          IFinCalendar calendar, IDocSequenceGenerator docSeq)
    {
        _docSeq = docSeq;
        _catalog = catalog;
        _stockPostingService = stockPostingService;
        _calendar = calendar;
        _db = db;
        _ctx = ctx;
        _arLedger = arLedger;
    }

    public async Task<List<Sal1103ReturnDto>> GetListAsync(CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? 0;
        var custs = await _db.SalCustomers.AsNoTracking().Where(c => c.IsDeleted == 0).ToDictionaryAsync(c => c.CustomerNo, c => c.CustomerName, ct);

        var list = await _db.SalReturns
            .AsNoTracking()
            .Where(r => r.CompanyNo == companyNo && r.IsDeleted == 0)
            .OrderByDescending(r => r.ReturnNo)
            .ToListAsync(ct);

        return list.Select(r => ToDto(r, custs.ContainsKey(r.CustomerNo) ? custs[r.CustomerNo] : null, null)).ToList();
    }

    public async Task<Sal1103ReturnDto> GetDetailAsync(long returnNo, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? 0;
        var r = await _db.SalReturns.AsNoTracking().FirstOrDefaultAsync(x => x.ReturnNo == returnNo && x.CompanyNo == companyNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Sales return not found: {returnNo}");

        var custName = await _db.SalCustomers.Where(c => c.CustomerNo == r.CustomerNo).Select(c => c.CustomerName).FirstOrDefaultAsync(ct);
        var lines = await GetLinesAsync(returnNo, ct);

        return ToDto(r, custName, lines);
    }

    public async Task<Sal1103ReturnDto> SaveAsync(Sal1103ReturnDto dto, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
        long branchNo = _ctx.CurrentBranchNo() ?? throw new ValidationException("Branch context is required");

        if (dto.CustomerNo <= 0) throw new ValidationException("Customer is required");
        // Restocking needs a real warehouse; defaulting to 1 would return goods to the wrong store.
        if (dto.WarehouseNo <= 0) throw new ValidationException("Warehouse is required");
        if (dto.Lines == null || dto.Lines.Count == 0) throw new ValidationException("Return lines are required");

        decimal totalAmount = dto.Lines.Sum(l => l.Qty * l.UnitPrice);
        decimal grandTotal = totalAmount + dto.TaxAmount;

        SalReturn ret;
        if (dto.ReturnNo.HasValue && dto.ReturnNo.Value > 0)
        {
            ret = await _db.SalReturns.FirstOrDefaultAsync(x => x.ReturnNo == dto.ReturnNo.Value && x.CompanyNo == companyNo && x.IsDeleted == 0, ct)
                ?? throw new NotFoundException($"Return not found: {dto.ReturnNo}");
            if (ret.Status != StDraft) throw new ValidationException("Only a Draft return can be edited");
            ret.SubTotal = totalAmount;
            ret.TaxAmount = dto.TaxAmount;
            ret.GrandTotal = grandTotal;
            ret.ReturnReason = dto.ReturnReason;
            ret.RefundMethod = dto.RefundMethod;
            ret.Remarks = dto.Remarks;
            ret.UpdatedBy = _ctx.CurrentUserNo(); ret.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            // Number from the ID generator (SYS_1301), not a timestamp and not the surrogate key.
            // The old code wrote a timestamp, saved, then overwrote with RET-{ReturnNo:D6} — so the
            // number was really the table PK, shared across every company and branch.
            string returnId = await _docSeq.NextAsync(companyNo, branchNo, DocSeqReturn, "RET", 6, ct);

            ret = new SalReturn
            {
                CompanyNo = companyNo,
                BranchNo = branchNo,
                ReturnId = returnId,
                ReturnDate = dto.ReturnDate != default ? dto.ReturnDate : DateTime.UtcNow.Date,
                OriginalInvoiceNo = dto.InvoiceNo,
                CustomerNo = dto.CustomerNo,
                WarehouseNo = dto.WarehouseNo,
                SubTotal = totalAmount,
                TaxAmount = dto.TaxAmount,
                GrandTotal = grandTotal,
                Status = StDraft,
                ReturnReason = dto.ReturnReason,
                RefundMethod = dto.RefundMethod,
                Remarks = dto.Remarks,
                IsActive = 1, IsDeleted = 0,
                CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow
            };
            _db.SalReturns.Add(ret);
            await _db.SaveChangesAsync(ct);
        }

        await ReplaceLinesAsync(ret.ReturnNo, companyNo, dto.Lines, ct);
        await _db.SaveChangesAsync(ct);

        return await GetDetailAsync(ret.ReturnNo, ct);
    }

    public async Task<Sal1103ReturnDto> PostAsync(long returnNo, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");

        var ret = await _db.SalReturns.FirstOrDefaultAsync(x => x.ReturnNo == returnNo && x.CompanyNo == companyNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Return not found: {returnNo}");

        if (ret.Status == StPosted) return await GetDetailAsync(returnNo, ct);
        if (ret.Status != StDraft) throw new ValidationException("Only a Draft return can be posted");

        var lines = await _db.SalReturnDtls
            .Where(l => l.ReturnNo == returnNo && l.IsDeleted == 0)
            .OrderBy(l => l.LineNo)
            .ToListAsync(ct);
        if (lines.Count == 0) throw new ValidationException("Nothing to return");

        // (1) Stock IN — returned goods go back to the same batch at the cost they left at.
        var retDay = DateOnly.FromDateTime(ret.ReturnDate);
        var finYear = await _calendar.FindYearForDateAsync(ret.CompanyNo, retDay, ct)
            ?? throw new ValidationException($"No financial year for date {ret.ReturnDate:yyyy-MM-dd}");
        var finPeriod = await _calendar.FindPeriodForDateAsync(finYear.FinYearNo, retDay, ct)
            ?? throw new ValidationException($"No financial period for date {ret.ReturnDate:yyyy-MM-dd}");
        if (finPeriod.PeriodStatus != 1)
            throw new ValidationException($"Cannot post: period '{finPeriod.FinPeriodName}' is not Open");

        // Only lines the user marked restockable rejoin sellable stock. A defective or expired
        // return is credited to the customer but written off, not put back on the shelf.
        var restocked = lines.Where(l => l.RestockFlag == RestockYes).ToList();

        var stockResult = restocked.Count == 0
            ? new StockPostingResult(0m, new Dictionary<long, decimal>())
            : await _stockPostingService.PostAsync(new StockPostingCommand(
                ret.CompanyNo, ret.BranchNo, RefSalRet, ret.ReturnId, ret.ReturnNo,
                ret.ReturnDate, finYear.FinYearNo, null, false,
                restocked.Select(l => StockPostingLeg.In(
                    ret.WarehouseNo, l.ProductNo, l.VariantNo, l.BatchNo, l.QtyBase, MvSaleReturn,
                    l.ReturnDtlNo, l.UnitCost)).ToList()), ct);

        foreach (var l in lines)
        {
            // A written-off line has no inventory value to bring back, so it carries no cost.
            l.LineCost = l.RestockFlag != RestockYes
                ? 0m
                : stockResult.LineCosts.TryGetValue(l.ReturnDtlNo, out var c) ? c : l.QtyBase * l.UnitCost;
        }
        ret.TotalCost = stockResult.TotalCost > 0 ? stockResult.TotalCost : lines.Sum(l => l.LineCost);

        // Java only touches the AR sub-ledger when the refund is settled against the
        // customer's due; a cash refund never moves receivables.
        if (ret.RefundMethod == RefundAgainstDue && ret.GrandTotal > 0)
        {
            await _arLedger.CreditAsync(ret.CustomerNo, ret.GrandTotal, ArRefReturn,
                ret.ReturnId, ret.ReturnNo, $"Sales Return {ret.ReturnId}", ct);
        }

        EmitGl(ret, "SalesReturnPosted", reverse: false);

        ret.Status = StPosted;
        ret.PostedAt = DateTime.UtcNow;
        ret.UpdatedBy = _ctx.CurrentUserNo(); ret.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return await GetDetailAsync(returnNo, ct);
    }

    /// <summary>
    /// Port of Java Sal1103Service.cancel. Reverses the AR movement (when the refund was
    /// taken against the customer's due) and emits a REVERSING GL event.
    ///
    /// The reversal uses a distinct event_type — SalesReturnReversed — so the posting
    /// engine's idempotency guard on (source_doc_type, source_doc_no) does not dedupe it
    /// against the original SalesReturnPosted. That mirrors the INV module's
    /// StockAdjustmentPosted / StockAdjustmentReversed pair.
    /// </summary>
    public async Task<Sal1103ReturnDto> CancelAsync(long returnNo, string? reason, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");

        var ret = await _db.SalReturns.FirstOrDefaultAsync(x => x.ReturnNo == returnNo && x.CompanyNo == companyNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Return not found: {returnNo}");

        if (ret.Status == StCancelled) throw new ValidationException("Return is already cancelled");
        if (ret.Status != StPosted) throw new ValidationException("Only a Posted return can be cancelled");

        // Take the restocked goods back out again, mirroring the IN legs the post wrote.
        var retDay = DateOnly.FromDateTime(ret.ReturnDate);
        var finYear = await _calendar.FindYearForDateAsync(ret.CompanyNo, retDay, ct)
            ?? throw new ValidationException($"No financial year for date {ret.ReturnDate:yyyy-MM-dd}");
        var finPeriod = await _calendar.FindPeriodForDateAsync(finYear.FinYearNo, retDay, ct)
            ?? throw new ValidationException($"No financial period for date {ret.ReturnDate:yyyy-MM-dd}");
        if (finPeriod.PeriodStatus != 1)
            throw new ValidationException($"Cannot cancel: period '{finPeriod.FinPeriodName}' is not Open");

        var lines = await _db.SalReturnDtls.AsNoTracking()
            .Where(l => l.ReturnNo == returnNo && l.IsDeleted == 0)
            .OrderBy(l => l.LineNo)
            .ToListAsync(ct);

        // Reverse exactly the lines the post put in. Written-off lines never entered stock, so
        // taking them out here would invent inventory that was never there.
        var restocked = lines.Where(l => l.RestockFlag == RestockYes).ToList();
        if (restocked.Count > 0)
        {
            var reversalLegs = restocked.Select(l => StockPostingLeg.Out(
                ret.WarehouseNo, l.ProductNo, l.VariantNo, l.BatchNo, l.QtyBase, MvSaleReturn, l.ReturnDtlNo)).ToList();

            await _stockPostingService.PostAsync(new StockPostingCommand(
                ret.CompanyNo, ret.BranchNo, RefSalRet, ret.ReturnId, ret.ReturnNo,
                ret.ReturnDate, finYear.FinYearNo, null, true, reversalLegs), ct);
        }

        // The post credits AR when the refund was settled against the customer's due,
        // so cancelling must debit the same amount back.
        if (ret.RefundMethod == RefundAgainstDue && ret.GrandTotal > 0)
        {
            await _arLedger.DebitAsync(ret.CustomerNo, ret.GrandTotal, ArRefAdjustment,
                ret.ReturnId, ret.ReturnNo, $"Return cancelled {ret.ReturnId}", ct);
        }

        EmitGl(ret, "SalesReturnReversed", reverse: true);

        ret.Status = StCancelled;
        ret.CancelledAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(reason))
            ret.Remarks = string.IsNullOrWhiteSpace(ret.Remarks) ? $"Cancelled: {reason}" : $"{ret.Remarks} | Cancelled: {reason}";
        ret.UpdatedBy = _ctx.CurrentUserNo(); ret.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return await GetDetailAsync(returnNo, ct);
    }

    /// <summary>
    /// Builds the sales-return GL payload. <paramref name="reverse"/> swaps every leg's
    /// direction so the cancellation nets the original to zero.
    /// Leg keys are the standardised names the FIN GL map is seeded with
    /// (SALES_RETURN / VAT_OUTPUT / RECEIVABLE / CASH / INVENTORY / COGS) — NOT the older
    /// Java names (OUTPUT_VAT / AR / CASH_BANK), which no longer resolve.
    /// </summary>
    private void EmitGl(SalReturn ret, string eventType, bool reverse)
    {
        if (ret.GrandTotal <= 0) return;

        string dr = reverse ? "cr" : "dr";
        string cr = reverse ? "dr" : "cr";
        bool againstDue = ret.RefundMethod == RefundAgainstDue;

        var legs = new List<GlPostingPayload.Leg>();
        if (ret.SubTotal > 0) legs.Add(new() { LegKey = "SALES_RETURN", Amount = ret.SubTotal, DrCr = dr });
        if (ret.TaxAmount > 0) legs.Add(new() { LegKey = "VAT_OUTPUT", Amount = ret.TaxAmount, DrCr = dr });

        if (againstDue)
            legs.Add(new() { LegKey = "RECEIVABLE", Amount = ret.GrandTotal, DrCr = cr, PartyType = 1, PartyNo = ret.CustomerNo });
        else
            legs.Add(new() { LegKey = "CASH", Amount = ret.GrandTotal, DrCr = cr });

        // Returned goods go back into stock at cost, so COGS is relieved.
        if (ret.TotalCost > 0)
        {
            legs.Add(new() { LegKey = "INVENTORY", Amount = ret.TotalCost, DrCr = dr });
            legs.Add(new() { LegKey = "COGS", Amount = ret.TotalCost, DrCr = cr });
        }

        var payload = new GlPostingPayload
        {
            VoucherDate = ret.ReturnDate,
            Narration = (reverse ? "Sales return reversal " : "Sales return ") + ret.ReturnId,
            BranchNo = ret.BranchNo,
            Legs = legs
        };

        _db.EventOutboxes.Add(new EventOutbox
        {
            CompanyNo = ret.CompanyNo,
            BranchNo = ret.BranchNo,
            EventType = eventType,
            AggregateType = "SAL_RETURN",
            AggregateId = ret.ReturnNo.ToString(),
            Payload = JsonSerializer.Serialize(payload),
            Status = 1,
            CreatedAt = DateTime.UtcNow
        });
    }

    public async Task DeleteAsync(long returnNo, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");

        var ret = await _db.SalReturns.FirstOrDefaultAsync(x => x.ReturnNo == returnNo && x.CompanyNo == companyNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Return not found: {returnNo}");

        if (ret.Status != StDraft) throw new ValidationException("Only a Draft return can be deleted");

        ret.IsDeleted = 1; ret.IsActive = 0;
        ret.DeletedBy = _ctx.CurrentUserNo(); ret.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private async Task ReplaceLinesAsync(long returnNo, long companyNo, List<Sal1103LineDto> lines,
                                         CancellationToken ct)
    {
        var existing = await _db.SalReturnDtls.Where(l => l.ReturnNo == returnNo && l.IsDeleted == 0).ToListAsync(ct);
        foreach (var e in existing) { e.IsDeleted = 1; e.DeletedBy = _ctx.CurrentUserNo(); e.DeletedAt = DateTime.UtcNow; }

        var live = lines.Where(l => l.ProductNo > 0 && l.Qty > 0).ToList();
        var products = await _catalog.GetProductsAsync(live.Select(l => l.ProductNo).Distinct().ToList(), companyNo, ct);
        var factors = await _catalog.GetUomFactorsAsync(products.Keys.ToList(), ct);

        int lineNo = 1;
        foreach (var dto in live)
        {
            if (!products.TryGetValue(dto.ProductNo, out var product))
                throw new NotFoundException($"Product not found: {dto.ProductNo}");

            long uomNo = dto.UomNo ?? product.BaseUomNo;
            decimal netUnitPrice = dto.NetUnitPrice > 0m ? dto.NetUnitPrice : dto.UnitPrice;
            var entity = new SalReturnDtl
            {
                ReturnNo = returnNo,
                LineNo = lineNo++,
                ProductNo = dto.ProductNo,
                UomNo = uomNo,
                Qty = dto.Qty,
                // Restocking uses base quantity, exactly as the sale's relief did.
                QtyBase = ToBaseQty(product, uomNo, dto.Qty, factors),
                UnitPrice = dto.UnitPrice,
                // What is actually credited back. Falls back to the selling price so a caller that
                // sends no net price refunds the full amount rather than nothing.
                NetUnitPrice = netUnitPrice,
                UnitCost = dto.UnitCost,
                TaxRatePct = dto.TaxRatePct,
                TaxAmount = Math.Round(dto.Qty * netUnitPrice * dto.TaxRatePct / 100m, 4),
                LineTotal = dto.Qty * netUnitPrice,
                // Whether the goods rejoin sellable stock is the user's call. Defaulting it here
                // would put damaged and expired returns back on the shelf.
                RestockFlag = dto.RestockFlag,
                Remarks = dto.Remarks,
                IsActive = 1, IsDeleted = 0,
                CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow
            };
            _db.SalReturnDtls.Add(entity);
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

    private async Task<List<Sal1103LineDto>> GetLinesAsync(long returnNo, CancellationToken ct)
    {
        var lines = await ProjectLinesAsync(returnNo, ct);
        if (lines.Count == 0) return lines;

        // Two queries for the whole set, not two per line.
        var productNames = await _catalog.GetProductNamesAsync(
            lines.Select(l => l.ProductNo).Distinct().ToList(), ct);
        var uomNames = await _catalog.GetUomNamesAsync(
            lines.Where(l => l.UomNo.HasValue).Select(l => l.UomNo!.Value).Distinct().ToList(), ct);

        foreach (var l in lines)
        {
            l.ProductName = productNames.GetValueOrDefault(l.ProductNo);
            if (l.UomNo.HasValue) l.UomName = uomNames.GetValueOrDefault(l.UomNo.Value);
        }
        return lines;
    }

    private async Task<List<Sal1103LineDto>> ProjectLinesAsync(long returnNo, CancellationToken ct)
    {
        return await _db.SalReturnDtls
            .AsNoTracking()
            .Where(l => l.ReturnNo == returnNo && l.IsDeleted == 0)
            .Select(l => new Sal1103LineDto
            {
                ReturnDtlNo = l.ReturnDtlNo,
                LineNo = l.LineNo,
                ProductNo = l.ProductNo,
                UomNo = l.UomNo,
                Qty = l.Qty,
                QtyBase = l.QtyBase,
                UnitPrice = l.UnitPrice,
                NetUnitPrice = l.NetUnitPrice,
                UnitCost = l.UnitCost,
                TaxRatePct = l.TaxRatePct,
                TaxAmount = l.TaxAmount,
                RestockFlag = l.RestockFlag,
                LineTotal = l.LineTotal,
                Remarks = l.Remarks
            })
            .ToListAsync(ct);
    }

    private static Sal1103ReturnDto ToDto(SalReturn r, string? custName, List<Sal1103LineDto>? lines) => new()
    {
        ReturnNo = r.ReturnNo,
        ReturnId = r.ReturnId,
        ReturnDate = r.ReturnDate,
        InvoiceNo = r.InvoiceNo,
        CustomerNo = r.CustomerNo,
        CustomerName = custName,
        WarehouseNo = r.WarehouseNo,
        SubTotal = r.SubTotal,
        TotalAmount = r.SubTotal,
        TaxAmount = r.TaxAmount,
        GrandTotal = r.GrandTotal,
        TotalCost = r.TotalCost,
        Status = r.Status,
        ReturnReason = r.ReturnReason,
        RefundMethod = r.RefundMethod,
        Reason = r.ReturnReason?.ToString(),
        Remarks = r.Remarks,
        IsActive = r.IsActive ?? 0,
        RowVersion = r.RowVersion,
        Lines = lines ?? new()
    };
}
