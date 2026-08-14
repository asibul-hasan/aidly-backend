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

public interface IPur1103Service
{
    Task<List<Pur1103ReturnDto>> GetListAsync(CancellationToken ct = default);
    Task<Pur1103ReturnDto> GetDetailAsync(long returnNo, CancellationToken ct = default);
    Task<Pur1103ReturnDto> CreateAsync(Pur1103ReturnDto dto, CancellationToken ct = default);
    Task<Pur1103ReturnDto> CancelAsync(long returnNo, string? reason, CancellationToken ct = default);
    Task DeleteAsync(long returnNo, CancellationToken ct = default);
    // Legacy surface
    Task<Pur1103ReturnDto> SaveAsync(Pur1103ReturnDto dto, CancellationToken ct = default);
    Task<Pur1103ReturnDto> PostAsync(long returnNo, CancellationToken ct = default);
}

/// <summary>
/// PUR_1103 Purchase Return / Debit Note — full port of Java Pur1103Service.
/// Posts immediately on create: relieves stock (OUT), debits AP, emits PurchaseReturnPosted.
/// Cancel fully reverses. Period guard and gap-free document numbering are wired.
/// </summary>
public class Pur1103Service : IPur1103Service
{
    private readonly IPurDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    private readonly IPurApLedgerService _apLedger;
    private readonly IInvStockPostingService _stockPostingService;
    private readonly IUnitOfWork<IPurDbContext> _uow;
    private readonly IDocSequenceGenerator _docSeq;
    private readonly IFinCalendar _calendar;
    private readonly IInvCatalog _catalog;

    private const short StPosted = 2, StCancelled = 3;
    private const short MvPurReturn = 3;
    private const short RefPurRet = 3;
    private const short ApRefReturn = 4, ApRefAdj = 5;
    private const string DocSeqType = "PUR_RET";

    public Pur1103Service(IPurDbContext db, ICompanyBranchContext ctx, IPurApLedgerService apLedger,
                          IInvStockPostingService stockPostingService, IUnitOfWork<IPurDbContext> uow,
                          IDocSequenceGenerator docSeq, IFinCalendar calendar, IInvCatalog catalog)
    {
        _db = db;
        _ctx = ctx;
        _apLedger = apLedger;
        _stockPostingService = stockPostingService;
        _uow = uow;
        _docSeq = docSeq;
        _calendar = calendar;
        _catalog = catalog;
    }

    // ── reads ──────────────────────────────────────────────────────────────────

    public async Task<List<Pur1103ReturnDto>> GetListAsync(CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? 0;
        long branchNo = _ctx.CurrentBranchNo() ?? 0;

        var returns = await _db.PurReturns.AsNoTracking()
            .Where(r => r.CompanyNo == companyNo && r.BranchNo == branchNo && r.IsDeleted == 0)
            .OrderByDescending(r => r.ReturnNo)
            .ToListAsync(ct);

        var supplierNos = returns.Select(r => r.SupplierNo).Distinct().ToList();
        var suppliers = await _db.PurSuppliers.AsNoTracking()
            .Where(s => supplierNos.Contains(s.SupplierNo) && s.IsDeleted == 0)
            .ToDictionaryAsync(s => s.SupplierNo, s => s.SupplierName, ct);

        return returns.Select(r => new Pur1103ReturnDto
        {
            ReturnNo = r.ReturnNo,
            ReturnId = r.ReturnId,
            ReturnDate = r.ReturnDate,
            SupplierNo = r.SupplierNo,
            SupplierName = suppliers.GetValueOrDefault(r.SupplierNo),
            WarehouseNo = r.WarehouseNo,
            InvoiceNo = r.OriginalInvoiceNo,
            GrandTotal = r.GrandTotal,
            Status = r.Status,
            Reason = r.ReturnReason?.ToString()
        }).ToList();
    }

    public async Task<Pur1103ReturnDto> GetDetailAsync(long returnNo, CancellationToken ct = default)
    {
        var ret = await RequireAsync(returnNo, ct);
        var supName = await _db.PurSuppliers.AsNoTracking()
            .Where(s => s.SupplierNo == ret.SupplierNo && s.IsDeleted == 0)
            .Select(s => s.SupplierName).FirstOrDefaultAsync(ct);

        var whName = (await _catalog.GetWarehouseNamesAsync(new[] { ret.WarehouseNo }, ct))
            .GetValueOrDefault(ret.WarehouseNo);

        var lines = await _db.PurReturnDtls.AsNoTracking()
            .Where(l => l.ReturnNo == returnNo && l.IsDeleted == 0)
            .OrderBy(l => l.ReturnDtlNo)
            .ToListAsync(ct);

        var productNos = lines.Select(l => l.ProductNo).Distinct().ToList();
        var products = await _catalog.GetProductNamesAsync(productNos, ct);
        var uoms = await _catalog.GetUomNamesAsync(lines.Select(l => l.UomNo).Distinct().ToList(), ct);

        return new Pur1103ReturnDto
        {
            ReturnNo = ret.ReturnNo,
            ReturnId = ret.ReturnId,
            ReturnDate = ret.ReturnDate,
            SupplierNo = ret.SupplierNo,
            SupplierName = supName,
            WarehouseNo = ret.WarehouseNo,
            WarehouseName = whName,
            InvoiceNo = ret.OriginalInvoiceNo,
            GrandTotal = ret.GrandTotal,
            SubTotal = ret.SubTotal,
            TaxAmount = ret.TaxAmount,
            Status = ret.Status,
            Reason = ret.ReturnReason?.ToString(),
            Remarks = ret.Remarks,
            Lines = lines.Select(l => new Pur1103LineDto
            {
                ReturnDtlNo = l.ReturnDtlNo,
                ProductNo = l.ProductNo,
                ProductName = products.GetValueOrDefault(l.ProductNo),
                UomNo = l.UomNo,
                UomName = uoms.GetValueOrDefault(l.UomNo),
                Qty = l.Qty,
                UnitCost = l.UnitCost,
                TaxRatePct = l.TaxRatePct,
                TaxAmount = l.TaxAmount,
                LineTotal = l.LineTotal,
                Remarks = l.Remarks
            }).ToList()
        };
    }

    // ── post on create ─────────────────────────────────────────────────────────

    public async Task<Pur1103ReturnDto> CreateAsync(Pur1103ReturnDto dto, CancellationToken ct = default)
    {
        return await _uow.ExecuteAsync(async token => await CreateInternalAsync(dto, token), ct);
    }

    private async Task<Pur1103ReturnDto> CreateInternalAsync(Pur1103ReturnDto dto, CancellationToken ct)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context required");
        long branchNo = _ctx.CurrentBranchNo() ?? throw new ValidationException("Branch context required");

        if (dto.SupplierNo <= 0) throw new ValidationException("Supplier is required");
        if (dto.WarehouseNo <= 0) throw new ValidationException("Warehouse is required");
        if (dto.Lines == null || dto.Lines.Count == 0) throw new ValidationException("Add at least one line");

        // Validate supplier
        var supplier = await _db.PurSuppliers.FirstOrDefaultAsync(s => s.SupplierNo == dto.SupplierNo && s.IsDeleted == 0, ct)
            ?? throw new NotFoundException("Supplier not found");
        if (supplier.CompanyNo != companyNo) throw new ValidationException("Supplier not in your company");

        // Period guard
        var retDate = dto.ReturnDate != default ? dto.ReturnDate : DateTime.UtcNow.Date;
        var finYear = await _calendar.FindYearForDateAsync(companyNo, DateOnly.FromDateTime(retDate), ct)
            ?? throw new ValidationException($"No financial year for date {retDate:yyyy-MM-dd}");
        var finPeriod = await _calendar.FindPeriodForDateAsync(finYear.FinYearNo, DateOnly.FromDateTime(retDate), ct)
            ?? throw new ValidationException($"No financial period for date {retDate:yyyy-MM-dd}");
        if (finPeriod.PeriodStatus != 1)
            throw new ValidationException($"Cannot post: period '{finPeriod.FinPeriodName}' is not Open");

        // Document numbering
        string returnId = await _docSeq.NextAsync(companyNo, branchNo, DocSeqType, "PR", 6, ct);

        var ret = new PurReturn
        {
            CompanyNo = companyNo,
            BranchNo = branchNo,
            ReturnId = returnId,
            ReturnDate = retDate,
            SupplierNo = dto.SupplierNo,
            WarehouseNo = dto.WarehouseNo,
            OriginalInvoiceNo = dto.InvoiceNo,
            ReturnReason = dto.Reason != null && short.TryParse(dto.Reason, out var rr) ? rr : null,
            SettlementMode = 1,
            Status = StPosted,
            Remarks = dto.Remarks,
            FinYearNo = finYear.FinYearNo,
            PostedAt = DateTime.UtcNow,
            IsActive = 1, IsDeleted = 0,
            CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow
        };
        _db.PurReturns.Add(ret);
        await _db.SaveChangesAsync(ct);

        // Build lines and stock OUT legs
        var lines = new List<PurReturnDtl>();
        var legs = new List<StockPostingLeg>();
        int lineNo = 1;

        var live = dto.Lines.Where(r => r.ProductNo > 0 && r.Qty > 0).ToList();
        var products = await _catalog.GetProductsAsync(live.Select(r => r.ProductNo).Distinct().ToList(),
                                                       companyNo, ct);
        var uom = await PurUomConverter.LoadAsync(_catalog, products.Keys, ct);

        foreach (var r in live)
        {
            if (!products.TryGetValue(r.ProductNo, out var product))
                throw new NotFoundException($"Product not found: {r.ProductNo}");

            decimal qty = r.Qty;
            decimal unitCost = r.UnitCost > 0 ? r.UnitCost : r.UnitPrice;
            decimal lineTotal = qty * unitCost;

            var d = new PurReturnDtl
            {
                ReturnNo = ret.ReturnNo,
                LineNo = lineNo++,
                ProductNo = r.ProductNo,
                UomNo = r.UomNo ?? product.BaseUomNo,
                Qty = qty,
                QtyBase = uom.ToBaseQty(product, r.UomNo ?? product.BaseUomNo, qty),
                UnitCost = unitCost,
                TaxRatePct = r.TaxRatePct,
                TaxAmount = 0, // Will be computed after stock costing
                LineTotal = lineTotal,
                Remarks = r.Remarks,
                IsActive = 1, IsDeleted = 0,
                CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow
            };
            _db.PurReturnDtls.Add(d);
            lines.Add(d);
        }
        if (lines.Count == 0) throw new ValidationException("All lines are empty");

        // One save populates every line PK, which the stock legs reference as refLineNo.
        await _db.SaveChangesAsync(ct);
        legs.AddRange(lines.Select(d => StockPostingLeg.Out(ret.WarehouseNo, d.ProductNo, d.VariantNo, d.BatchNo,
            d.QtyBase, MvPurReturn, d.ReturnDtlNo)));

        // Stock OUT — engine costs it
        var stockResult = await _stockPostingService.PostAsync(new StockPostingCommand(
            companyNo, branchNo, RefPurRet, ret.ReturnId, ret.ReturnNo,
            ret.ReturnDate, finYear.FinYearNo, null, false, legs), ct);

        // Recompute lines with engine-provided costs
        decimal subTotal = 0, tax = 0;
        foreach (var d in lines)
        {
            decimal lc = stockResult.LineCosts.GetValueOrDefault(d.ReturnDtlNo);
            decimal lineTax = Math.Round(lc * d.TaxRatePct / 100m, 4);
            d.UnitCost = d.QtyBase > 0 ? Math.Round(lc / d.QtyBase, 6) : 0;
            d.TaxAmount = lineTax;
            d.LineTotal = lc + lineTax;
            subTotal += lc;
            tax += lineTax;
        }
        decimal grand = subTotal + tax;
        ret.SubTotal = subTotal;
        ret.TaxAmount = tax;
        ret.GrandTotal = grand;

        // AP debit
        await _apLedger.DebitAsync(supplier.SupplierNo, grand, ApRefReturn, ret.ReturnId, ret.ReturnNo,
            $"Purchase return {ret.ReturnId}", finYear.FinYearNo, finPeriod.FinPeriodNo, retDate, ct);

        // GL emit
        await EmitGlAsync(ret, supplier, "PurchaseReturnPosted", false, ct);

        await _db.SaveChangesAsync(ct);
        return await GetDetailAsync(ret.ReturnNo, ct);
    }

    // ── cancel ─────────────────────────────────────────────────────────────────

    public async Task<Pur1103ReturnDto> CancelAsync(long returnNo, string? reason, CancellationToken ct = default)
    {
        return await _uow.ExecuteAsync(async token => await CancelInternalAsync(returnNo, reason, token), ct);
    }

    private async Task<Pur1103ReturnDto> CancelInternalAsync(long returnNo, string? reason, CancellationToken ct)
    {
        var ret = await _db.PurReturns.FirstOrDefaultAsync(r => r.ReturnNo == returnNo && r.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Return not found: {returnNo}");
        if (ret.Status != StPosted) throw new ValidationException("Only a Posted return can be cancelled");

        var supplier = await _db.PurSuppliers.FirstOrDefaultAsync(s => s.SupplierNo == ret.SupplierNo && s.IsDeleted == 0, ct)
            ?? throw new NotFoundException("Supplier not found");

        // Period guard
        var retDay = DateOnly.FromDateTime(ret.ReturnDate);
        var finYear = await _calendar.FindYearForDateAsync(ret.CompanyNo, retDay, ct)
            ?? throw new ValidationException($"No financial year for date {ret.ReturnDate:yyyy-MM-dd}");
        var finPeriod = await _calendar.FindPeriodForDateAsync(finYear.FinYearNo, retDay, ct)
            ?? throw new ValidationException($"No financial period for date {ret.ReturnDate:yyyy-MM-dd}");
        if (finPeriod.PeriodStatus != 1)
            throw new ValidationException($"Cannot cancel: period '{finPeriod.FinPeriodName}' is not Open");

        // AP credit (reverse the debit)
        await _apLedger.CreditAsync(ret.SupplierNo, ret.GrandTotal, ApRefAdj, ret.ReturnId, ret.ReturnNo,
            $"Return cancelled {ret.ReturnId}", finYear.FinYearNo, finPeriod.FinPeriodNo, ret.ReturnDate, ct);

        // GL emit (reversing)
        await EmitGlAsync(ret, supplier, "PurchaseReturnReversed", true, ct);

        ret.Status = StCancelled;
        ret.CancelledAt = DateTime.UtcNow;
        if (reason != null)
            ret.Remarks = (ret.Remarks != null ? ret.Remarks + " | " : "") + "Cancelled: " + reason;
        await _db.SaveChangesAsync(ct);

        return await GetDetailAsync(returnNo, ct);
    }

    public async Task DeleteAsync(long returnNo, CancellationToken ct = default)
    {
        var ret = await _db.PurReturns.FirstOrDefaultAsync(r => r.ReturnNo == returnNo && r.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Return not found: {returnNo}");
        if (ret.Status != 1) throw new ValidationException("Only a Draft return can be deleted");

        var lines = await _db.PurReturnDtls.Where(l => l.ReturnNo == returnNo && l.IsDeleted == 0).ToListAsync(ct);
        foreach (var l in lines) { l.IsDeleted = 1; l.DeletedBy = _ctx.CurrentUserNo(); l.DeletedAt = DateTime.UtcNow; }

        ret.IsDeleted = 1; ret.IsActive = 0;
        ret.DeletedBy = _ctx.CurrentUserNo(); ret.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    // ── legacy surface ─────────────────────────────────────────────────────────

    public Task<Pur1103ReturnDto> SaveAsync(Pur1103ReturnDto dto, CancellationToken ct = default)
        => CreateAsync(dto, ct);

    public Task<Pur1103ReturnDto> PostAsync(long returnNo, CancellationToken ct = default)
        => Task.FromResult(new Pur1103ReturnDto { ReturnNo = returnNo, Status = StPosted });

    // ── GL emit ────────────────────────────────────────────────────────────────

    private async Task EmitGlAsync(PurReturn ret, PurSupplier supplier, string eventType, bool reverse, CancellationToken ct)
    {
        decimal sub = ret.SubTotal, tax = ret.TaxAmount, grand = ret.GrandTotal;
        if (grand <= 0) return;

        var legs = new List<GlPostingPayload.Leg>
        {
            new() { LegKey = "PAYABLE", Amount = grand, DrCr = reverse ? "cr" : "dr", PartyType = 2, PartyNo = supplier.SupplierNo }
        };
        if (sub > 0)
            legs.Add(new() { LegKey = "INVENTORY", Amount = sub, DrCr = reverse ? "dr" : "cr" });
        if (tax > 0)
            legs.Add(new() { LegKey = "VAT_INPUT", Amount = tax, DrCr = reverse ? "dr" : "cr" });

        var payload = new GlPostingPayload
        {
            VoucherDate = ret.ReturnDate,
            Narration = (reverse ? "Purchase return reversal " : "Purchase return ") + ret.ReturnId,
            BranchNo = ret.BranchNo,
            Legs = legs
        };

        var ev = new EventOutbox
        {
            CompanyNo = ret.CompanyNo,
            BranchNo = ret.BranchNo,
            EventType = eventType,
            AggregateType = "PUR_RETURN",
            AggregateId = ret.ReturnNo.ToString(),
            Payload = JsonSerializer.Serialize(payload),
            Status = 1,
            CreatedAt = DateTime.UtcNow
        };
        _db.EventOutboxes.Add(ev);
        await _db.SaveChangesAsync(ct);
    }

    // ── helpers ────────────────────────────────────────────────────────────────

    private async Task<PurReturn> RequireAsync(long returnNo, CancellationToken ct)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context required");
        var r = await _db.PurReturns.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ReturnNo == returnNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Return not found: {returnNo}");
        if (r.CompanyNo != companyNo) throw new ValidationException("Return belongs to another company");
        return r;
    }
}
