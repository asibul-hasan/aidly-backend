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

public interface IPur1105Service
{
    Task<List<Pur1105ReceiptDto>> GetListAsync(CancellationToken ct = default);
    Task<Pur1105ReceiptDto> GetDetailAsync(long receiptNo, CancellationToken ct = default);
    Task<Pur1105ReceiptDto> SaveAsync(Pur1105ReceiptDto dto, CancellationToken ct = default);
    Task<Pur1105ReceiptDto> PostAsync(long receiptNo, CancellationToken ct = default);
    Task<Pur1105ReceiptDto> CancelAsync(long receiptNo, string? reason, CancellationToken ct = default);
    Task DeleteAsync(long receiptNo, CancellationToken ct = default);
}

/// <summary>
/// PUR_1105 Goods Receipt Note — full port of Java Pur1105Service.
/// Receives goods against a supplier (optionally against a PO), posts immediately.
/// Cancel reverses stock + GL. Period guard and gap-free document numbering are wired.
/// </summary>
public class Pur1105Service : IPur1105Service
{
    private readonly IPurDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    private readonly IInvStockPostingService _stockPostingService;
    private readonly IUnitOfWork<IPurDbContext> _uow;
    private readonly IDocSequenceGenerator _docSeq;
    private readonly IFinCalendar _calendar;
    private readonly IInvCatalog _catalog;

    private const short StDraft = 1, StPosted = 2, StCancelled = 3;

    /// <summary>pur_invoice.status for a cancelled invoice — such a bill releases its receipt lines.</summary>
    private const short InvCancelled = 5;
    private const short MvPurchase = 2;
    private const short RefPurGrn = 6;
    private const string DocSeqType = "PUR_GRN";

    public Pur1105Service(IPurDbContext db, ICompanyBranchContext ctx, IInvStockPostingService stockPostingService,
                          IUnitOfWork<IPurDbContext> uow, IDocSequenceGenerator docSeq,
                          IFinCalendar calendar, IInvCatalog catalog)
    {
        _db = db;
        _ctx = ctx;
        _stockPostingService = stockPostingService;
        _uow = uow;
        _docSeq = docSeq;
        _calendar = calendar;
        _catalog = catalog;
    }

    // ── reads ──────────────────────────────────────────────────────────────────

    public async Task<List<Pur1105ReceiptDto>> GetListAsync(CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? 0;
        long branchNo = _ctx.CurrentBranchNo() ?? 0;

        var receipts = await _db.PurReceipts.AsNoTracking()
            .Where(r => r.CompanyNo == companyNo && r.BranchNo == branchNo && r.IsDeleted == 0)
            .OrderByDescending(r => r.ReceiptNo)
            .ToListAsync(ct);

        var supplierNos = receipts.Select(r => r.SupplierNo).Distinct().ToList();
        var suppliers = await _db.PurSuppliers.AsNoTracking()
            .Where(s => supplierNos.Contains(s.SupplierNo) && s.IsDeleted == 0)
            .ToDictionaryAsync(s => s.SupplierNo, s => s.SupplierName, ct);

        var warehouseNos = receipts.Select(r => r.WarehouseNo).Distinct().ToList();
        var warehouses = await _catalog.GetWarehouseNamesAsync(warehouseNos, ct);

        var orderNos = receipts.Where(r => r.OrderNo.HasValue).Select(r => r.OrderNo!.Value).Distinct().ToList();
        var orders = await _db.PurOrders.AsNoTracking()
            .Where(o => orderNos.Contains(o.OrderNo) && o.IsDeleted == 0)
            .ToDictionaryAsync(o => o.OrderNo, o => o.OrderId, ct);

        return receipts.Select(r => new Pur1105ReceiptDto
        {
            ReceiptNo = r.ReceiptNo,
            ReceiptId = r.ReceiptId,
            ReceiptDate = r.ReceiptDate,
            SupplierNo = r.SupplierNo,
            SupplierName = suppliers.GetValueOrDefault(r.SupplierNo),
            WarehouseNo = r.WarehouseNo,
            WarehouseName = warehouses.GetValueOrDefault(r.WarehouseNo),
            OrderNo = r.OrderNo,
            OrderId = r.OrderNo.HasValue ? orders.GetValueOrDefault(r.OrderNo.Value) : null,
            Status = r.Status,
            CancelReason = r.CancelReason,
            SubTotal = r.SubTotal,
            TaxAmount = r.TaxAmount,
            GrandTotal = r.GrandTotal,
            Remarks = r.Remarks
        }).ToList();
    }

    public async Task<Pur1105ReceiptDto> GetDetailAsync(long receiptNo, CancellationToken ct = default)
    {
        var receipt = await RequireAsync(receiptNo, ct);

        var supName = await _db.PurSuppliers.AsNoTracking()
            .Where(s => s.SupplierNo == receipt.SupplierNo && s.IsDeleted == 0)
            .Select(s => s.SupplierName).FirstOrDefaultAsync(ct);

        var whName = (await _catalog.GetWarehouseNamesAsync(new[] { receipt.WarehouseNo }, ct))
            .GetValueOrDefault(receipt.WarehouseNo);

        string? orderId = null;
        if (receipt.OrderNo.HasValue)
            orderId = await _db.PurOrders.AsNoTracking()
                .Where(o => o.OrderNo == receipt.OrderNo.Value && o.IsDeleted == 0)
                .Select(o => o.OrderId).FirstOrDefaultAsync(ct);

        var lines = await _db.PurReceiptDtls.AsNoTracking()
            .Where(l => l.ReceiptNo == receiptNo && l.IsDeleted == 0)
            .OrderBy(l => l.ReceiptDtlNo)
            .ToListAsync(ct);

        var productNos = lines.Select(l => l.ProductNo).Distinct().ToList();
        var products = await _catalog.GetProductNamesAsync(productNos, ct);
        var uoms = await _catalog.GetUomNamesAsync(lines.Select(l => l.UomNo).Distinct().ToList(), ct);

        return new Pur1105ReceiptDto
        {
            ReceiptNo = receipt.ReceiptNo,
            ReceiptId = receipt.ReceiptId,
            ReceiptDate = receipt.ReceiptDate,
            SupplierNo = receipt.SupplierNo,
            SupplierName = supName,
            WarehouseNo = receipt.WarehouseNo,
            WarehouseName = whName,
            OrderNo = receipt.OrderNo,
            OrderId = orderId,
            Status = receipt.Status,
            CancelReason = receipt.CancelReason,
            SubTotal = receipt.SubTotal,
            TaxAmount = receipt.TaxAmount,
            GrandTotal = receipt.GrandTotal,
            Remarks = receipt.Remarks,
            Lines = lines.Select(l => new Pur1105LineDto
            {
                ReceiptDtlNo = l.ReceiptDtlNo,
                LineNo = l.LineNo,
                ProductNo = l.ProductNo,
                ProductName = products.GetValueOrDefault(l.ProductNo),
                UomNo = l.UomNo,
                UomName = uoms.GetValueOrDefault(l.UomNo),
                Qty = l.Qty,
                QtyBase = l.QtyBase,
                UnitCost = l.UnitCost,
                TaxRatePct = l.TaxRatePct,
                TaxAmount = l.TaxAmount,
                LineTotal = l.LineTotal,
                BatchNo = l.BatchNo,
                BatchCode = l.BatchCode,
                Remarks = l.Remarks
            }).ToList()
        };
    }

    // ── write (draft) ──────────────────────────────────────────────────────────

    public async Task<Pur1105ReceiptDto> SaveAsync(Pur1105ReceiptDto dto, CancellationToken ct = default)
    {
        return await _uow.ExecuteAsync(async token => await SaveInternalAsync(dto, token), ct);
    }

    private async Task<Pur1105ReceiptDto> SaveInternalAsync(Pur1105ReceiptDto dto, CancellationToken ct)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context required");
        long branchNo = _ctx.CurrentBranchNo() ?? throw new ValidationException("Branch context required");

        if (dto.SupplierNo <= 0) throw new ValidationException("Supplier is required");
        if (dto.WarehouseNo <= 0) throw new ValidationException("Warehouse is required");
        if (dto.Lines == null || dto.Lines.Count == 0) throw new ValidationException("Add at least one line");

        // Period guard
        var rcptDate = dto.ReceiptDate != default ? dto.ReceiptDate : DateTime.UtcNow.Date;
        var finYear = await _calendar.FindYearForDateAsync(companyNo, DateOnly.FromDateTime(rcptDate), ct)
            ?? throw new ValidationException($"No financial year for date {rcptDate:yyyy-MM-dd}");

        PurReceipt receipt;
        if (dto.ReceiptNo.HasValue && dto.ReceiptNo.Value > 0)
        {
            receipt = await _db.PurReceipts.FirstOrDefaultAsync(r => r.ReceiptNo == dto.ReceiptNo.Value && r.IsDeleted == 0, ct)
                ?? throw new NotFoundException($"Receipt not found: {dto.ReceiptNo}");
            if (receipt.CompanyNo != companyNo) throw new ValidationException("GRN belongs to another company");
            if (receipt.Status != StDraft) throw new ValidationException("Only a Draft GRN can be edited");
        }
        else
        {
            // Number drawn before the insert — never a "TEMP" placeholder that a failure could leave
            // behind as the permanent id.
            string receiptId = await _docSeq.NextAsync(companyNo, branchNo, DocSeqType, "GRN", 6, ct);

            receipt = new PurReceipt
            {
                CompanyNo = companyNo,
                BranchNo = branchNo,
                Status = StDraft,
                ReceiptId = receiptId,
                // pur_receipt.fin_year_no is NOT NULL. The guard above already resolved the year
                // for this receipt date; not carrying it onto the entity meant no goods receipt
                // could be saved at all — the same defect the POS sale and purchase invoice had.
                FinYearNo = finYear.FinYearNo,
                IsActive = 1, IsDeleted = 0,
                CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow
            };
            _db.PurReceipts.Add(receipt);
            await _db.SaveChangesAsync(ct);
        }

        receipt.ReceiptDate = rcptDate;
        receipt.SupplierNo = dto.SupplierNo;
        receipt.WarehouseNo = dto.WarehouseNo;
        receipt.OrderNo = dto.OrderNo;
        receipt.Remarks = dto.Remarks;
        receipt.UpdatedBy = _ctx.CurrentUserNo(); receipt.UpdatedAt = DateTime.UtcNow;

        // Recompute lines
        var existing = await _db.PurReceiptDtls.Where(l => l.ReceiptNo == receipt.ReceiptNo && l.IsDeleted == 0).ToListAsync(ct);
        foreach (var line in existing) { line.IsDeleted = 1; line.DeletedBy = _ctx.CurrentUserNo(); line.DeletedAt = DateTime.UtcNow; }

        decimal subTotal = 0, taxTotal = 0;
        int lineNo = 1;

        var live = dto.Lines.Where(r => r.ProductNo > 0 && r.Qty > 0).ToList();
        var products = await _catalog.GetProductsAsync(live.Select(r => r.ProductNo).Distinct().ToList(),
                                                       receipt.CompanyNo, ct);
        var uom = await PurUomConverter.LoadAsync(_catalog, products.Keys, ct);

        foreach (var r in live)
        {
            if (!products.TryGetValue(r.ProductNo, out var product))
                throw new NotFoundException($"Product not found: {r.ProductNo}");

            if (product.IsBatchTracked == 1 && string.IsNullOrWhiteSpace(r.BatchCode))
                throw new ValidationException($"Batch code is required for batch-tracked product: {product.ProductId}");

            decimal qty = r.Qty;
            decimal unitCost = r.UnitCost;
            decimal taxable = qty * unitCost;
            decimal taxPct = r.TaxRatePct;
            decimal tax = Math.Round(taxable * taxPct / 100m, 4);

            var detail = new PurReceiptDtl
            {
                ReceiptNo = receipt.ReceiptNo,
                LineNo = lineNo++,
                OrderDtlNo = r.OrderDtlNo,
                ProductNo = r.ProductNo,
                UomNo = r.UomNo ?? product.BaseUomNo,
                Qty = qty,
                QtyBase = uom.ToBaseQty(product, r.UomNo ?? product.BaseUomNo, qty),
                UnitCost = unitCost,
                TaxRatePct = taxPct,
                TaxAmount = tax,
                LineTotal = taxable + tax,
                BatchCode = r.BatchCode,
                MfgDate = r.MfgDate,
                ExpiryDate = r.ExpiryDate,
                Remarks = r.Remarks,
                IsActive = 1, IsDeleted = 0,
                CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow
            };
            _db.PurReceiptDtls.Add(detail);
            subTotal += taxable;
            taxTotal += tax;
        }

        receipt.SubTotal = subTotal;
        receipt.TaxAmount = taxTotal;
        receipt.GrandTotal = subTotal + taxTotal;
        await _db.SaveChangesAsync(ct);

        return await GetDetailAsync(receipt.ReceiptNo, ct);
    }

    // ── post ───────────────────────────────────────────────────────────────────

    public async Task<Pur1105ReceiptDto> PostAsync(long receiptNo, CancellationToken ct = default)
    {
        return await _uow.ExecuteAsync(async token => await PostInternalAsync(receiptNo, token), ct);
    }

    private async Task<Pur1105ReceiptDto> PostInternalAsync(long receiptNo, CancellationToken ct)
    {
        var receipt = await _db.PurReceipts.FirstOrDefaultAsync(r => r.ReceiptNo == receiptNo && r.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Receipt not found: {receiptNo}");
        if (receipt.CompanyNo != _ctx.CurrentCompanyNo()) throw new ValidationException("GRN belongs to another company");
        if (receipt.Status == StPosted) return await GetDetailAsync(receiptNo, ct);
        if (receipt.Status != StDraft) throw new ValidationException("Only a Draft GRN can be posted");

        // Period guard
        var rcptDay = DateOnly.FromDateTime(receipt.ReceiptDate);
        var finYear = await _calendar.FindYearForDateAsync(receipt.CompanyNo, rcptDay, ct)
            ?? throw new ValidationException($"No financial year for date {receipt.ReceiptDate:yyyy-MM-dd}");
        var finPeriod = await _calendar.FindPeriodForDateAsync(finYear.FinYearNo, rcptDay, ct)
            ?? throw new ValidationException($"No financial period for date {receipt.ReceiptDate:yyyy-MM-dd}");
        if (finPeriod.PeriodStatus != 1)
            throw new ValidationException($"Cannot post: period '{finPeriod.FinPeriodName}' is not Open");

        var lines = await _db.PurReceiptDtls.Where(l => l.ReceiptNo == receiptNo && l.IsDeleted == 0)
            .OrderBy(l => l.LineNo).ToListAsync(ct);
        if (lines.Count == 0) throw new ValidationException("Nothing to post");

        // Stock IN
        var legs = lines.Select((l, idx) => StockPostingLeg.In(
            receipt.WarehouseNo, l.ProductNo, l.VariantNo, l.BatchNo,
            l.QtyBase, MvPurchase, l.ReceiptDtlNo, l.UnitCost)).ToList();

        var cmd = new StockPostingCommand(receipt.CompanyNo, receipt.BranchNo, RefPurGrn,
            receipt.ReceiptId, receipt.ReceiptNo, receipt.ReceiptDate, finYear.FinYearNo, null, false, legs);
        await _stockPostingService.PostAsync(cmd, ct);

        // Update PO received quantities
        await UpdatePoReceivedAsync(lines, true, ct);

        // GL emit
        await EmitGlAsync(receipt, "GoodsReceiptPosted", false, ct);

        receipt.Status = StPosted;
        receipt.PostedBy = _ctx.CurrentUserNo();
        receipt.PostedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return await GetDetailAsync(receiptNo, ct);
    }

    // ── cancel ─────────────────────────────────────────────────────────────────

    public async Task<Pur1105ReceiptDto> CancelAsync(long receiptNo, string? reason, CancellationToken ct = default)
    {
        return await _uow.ExecuteAsync(async token => await CancelInternalAsync(receiptNo, reason, token), ct);
    }

    private async Task<Pur1105ReceiptDto> CancelInternalAsync(long receiptNo, string? reason, CancellationToken ct)
    {
        var receipt = await _db.PurReceipts.FirstOrDefaultAsync(r => r.ReceiptNo == receiptNo && r.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Receipt not found: {receiptNo}");
        if (receipt.CompanyNo != _ctx.CurrentCompanyNo()) throw new ValidationException("GRN belongs to another company");
        if (receipt.Status != StPosted) throw new ValidationException("Only a Posted GRN can be cancelled");

        // A GRN whose lines have been billed cannot be unwound on its own.
        //
        // Posting the receipt credits GRN clearing; invoicing that line debits it back. Cancelling
        // the receipt afterwards debits GRN clearing a SECOND time, so one delivery clears the
        // accrual twice and the account goes into a debit balance it can never work off — observed
        // at -1,000 after cancelling a GRN that PINV000012 had already billed. The invoice has to
        // be cancelled first, which returns the accrual, and then the receipt can be reversed.
        var billedBy = await (
            from d in _db.PurReceiptDtls.AsNoTracking()
            join il in _db.PurInvoiceDtls.AsNoTracking() on d.ReceiptDtlNo equals il.ReceiptDtlNo
            join i in _db.PurInvoices.AsNoTracking() on il.InvoiceNo equals i.InvoiceNo
            where d.ReceiptNo == receiptNo && d.IsDeleted == 0
                  && il.IsDeleted == 0 && i.IsDeleted == 0 && i.Status != InvCancelled
            select i.InvoiceId).FirstOrDefaultAsync(ct);

        if (billedBy != null)
            throw new ValidationException(
                $"These goods are billed on invoice {billedBy} — cancel that invoice before cancelling this GRN");

        // Period guard
        var rcptDay = DateOnly.FromDateTime(receipt.ReceiptDate);
        var finYear = await _calendar.FindYearForDateAsync(receipt.CompanyNo, rcptDay, ct)
            ?? throw new ValidationException($"No financial year for date {receipt.ReceiptDate:yyyy-MM-dd}");
        var finPeriod = await _calendar.FindPeriodForDateAsync(finYear.FinYearNo, rcptDay, ct)
            ?? throw new ValidationException($"No financial period for date {receipt.ReceiptDate:yyyy-MM-dd}");
        if (finPeriod.PeriodStatus != 1)
            throw new ValidationException($"Cannot cancel: period '{finPeriod.FinPeriodName}' is not Open");

        // GL emit (reversing)
        await EmitGlAsync(receipt, "GoodsReceiptReversed", true, ct);

        // Update PO received quantities (subtract)
        var lines = await _db.PurReceiptDtls.Where(l => l.ReceiptNo == receiptNo && l.IsDeleted == 0).ToListAsync(ct);
        await UpdatePoReceivedAsync(lines, false, ct);

        receipt.Status = StCancelled;
        receipt.CancelledBy = _ctx.CurrentUserNo();
        receipt.CancelledAt = DateTime.UtcNow;
        receipt.CancelReason = reason;
        await _db.SaveChangesAsync(ct);

        return await GetDetailAsync(receiptNo, ct);
    }

    public async Task DeleteAsync(long receiptNo, CancellationToken ct = default)
    {
        var receipt = await _db.PurReceipts.FirstOrDefaultAsync(r => r.ReceiptNo == receiptNo && r.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Receipt not found: {receiptNo}");
        if (receipt.CompanyNo != _ctx.CurrentCompanyNo()) throw new ValidationException("GRN belongs to another company");
        if (receipt.Status != StDraft) throw new ValidationException("Only a Draft GRN can be deleted");

        var lines = await _db.PurReceiptDtls.Where(l => l.ReceiptNo == receiptNo && l.IsDeleted == 0).ToListAsync(ct);
        foreach (var l in lines) { l.IsDeleted = 1; l.DeletedBy = _ctx.CurrentUserNo(); l.DeletedAt = DateTime.UtcNow; }

        receipt.IsDeleted = 1; receipt.IsActive = 0;
        receipt.DeletedBy = _ctx.CurrentUserNo(); receipt.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    // ── helpers ────────────────────────────────────────────────────────────────

    private async Task UpdatePoReceivedAsync(List<PurReceiptDtl> lines, bool add, CancellationToken ct)
    {
        foreach (var line in lines)
        {
            if (!line.OrderDtlNo.HasValue) continue;
            var orderLine = await _db.PurOrderDtls.FirstOrDefaultAsync(l => l.OrderDtlNo == line.OrderDtlNo.Value && l.IsDeleted == 0, ct);
            if (orderLine == null) continue;
            decimal current = orderLine.ReceivedQtyBase;
            orderLine.ReceivedQtyBase = add ? current + line.QtyBase : Math.Max(0, current - line.QtyBase);
        }
    }

    private async Task EmitGlAsync(PurReceipt receipt, string eventType, bool reverse, CancellationToken ct)
    {
        decimal amount = receipt.SubTotal;
        if (amount <= 0) return;

        var legs = new List<GlPostingPayload.Leg>
        {
            new() { LegKey = "INVENTORY", Amount = amount, DrCr = reverse ? "cr" : "dr" },
            new() { LegKey = "GRN_CLEARING", Amount = amount, DrCr = reverse ? "dr" : "cr" }
        };

        var payload = new GlPostingPayload
        {
            VoucherDate = receipt.ReceiptDate,
            Narration = (reverse ? "GRN reversal " : "GRN ") + receipt.ReceiptId,
            BranchNo = receipt.BranchNo,
            Legs = legs
        };

        var ev = new EventOutbox
        {
            CompanyNo = receipt.CompanyNo,
            BranchNo = receipt.BranchNo,
            EventType = eventType,
            AggregateType = "PUR_GRN",
            AggregateId = receipt.ReceiptNo.ToString(),
            Payload = JsonSerializer.Serialize(payload),
            Status = 1,
            CreatedAt = DateTime.UtcNow
        };
        _db.EventOutboxes.Add(ev);
        await _db.SaveChangesAsync(ct);
    }

    private async Task<PurReceipt> RequireAsync(long receiptNo, CancellationToken ct)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context required");
        var r = await _db.PurReceipts.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ReceiptNo == receiptNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Receipt not found: {receiptNo}");
        if (r.CompanyNo != companyNo) throw new ValidationException("Receipt belongs to another company");
        return r;
    }
}
