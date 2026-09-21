using AidlyErp.Inv.Application.Dto;
using AidlyErp.Inv.Application.Interfaces;
using AidlyErp.Inv.Contracts;
using AidlyErp.Inv.Domain;
using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Sys.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AidlyErp.Inv.Application.Services;

public interface IInv2003Service
{
    Task<Inv2003LookupDto> GetLookupsAsync(CancellationToken ct = default);
    Task<List<Inv2003TransferDto>> GetListAsync(CancellationToken ct = default);
    Task<Inv2003TransferDto> GetDetailAsync(long transferNo, CancellationToken ct = default);
    Task<Inv2003TransferDto> SaveAsync(Inv2003TransferDto dto, CancellationToken ct = default);
    Task<Inv2003TransferDto> SubmitAsync(long transferNo, CancellationToken ct = default);
    Task<Inv2003TransferDto> DispatchAsync(long transferNo, CancellationToken ct = default);
    Task<Inv2003TransferDto> ReceiveAsync(long transferNo, Inv2003TransferDto? payload, CancellationToken ct = default);
    Task<Inv2003TransferDto> CancelAsync(long transferNo, CancellationToken ct = default);
    Task DeleteAsync(long transferNo, CancellationToken ct = default);
}

/// <summary>
/// INV_2003 Stock Transfer — Draft → Submitted → Dispatched → Received. Stock moves in two phases
/// through a transit warehouse, so goods in flight are never invisible: dispatch is source→transit,
/// receive is transit→destination. A short receipt leaves the remainder sitting in transit.
/// </summary>
public class Inv2003Service : IInv2003Service
{
    private const short Deleted = 0;
    private const short StDraft = 1, StSubmitted = 2, StDispatched = 3, StReceived = 4,
                        StShortReceived = 5, StCancelled = 6;

    /// <summary>Movement types: 6 transfer-out, 7 transfer-in.</summary>
    private const short MvOut = 6, MvIn = 7;

    /// <summary><c>inv_stock_ledger.ref_doc_type</c>, one per phase so the two legs stay distinguishable.</summary>
    private const short RefDispatch = 6, RefReceive = 7;

    /// <summary><c>inv_warehouse.warehouse_type = 3</c> marks the branch's transit store.</summary>
    private const short TransitType = 3;

    private const string DocType = "INV_TRF";

    private readonly IInvDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    private readonly IInvStockPostingService _postingService;
    private readonly IInvLookupService _lookups;
    private readonly IInvPeriodResolver _periodResolver;
    private readonly IDocSequenceGenerator _docSeq;

    public Inv2003Service(IInvDbContext db, ICompanyBranchContext ctx, IInvStockPostingService postingService,
                          IInvLookupService lookups, IInvPeriodResolver periodResolver,
                          IDocSequenceGenerator docSeq)
    {
        _db = db;
        _ctx = ctx;
        _postingService = postingService;
        _lookups = lookups;
        _periodResolver = periodResolver;
        _docSeq = docSeq;
    }

    // ── reads ────────────────────────────────────────────────────────────────

    public async Task<Inv2003LookupDto> GetLookupsAsync(CancellationToken ct = default) => new()
    {
        Warehouses = await _lookups.GetWarehouseOptionsAsync(ct),
        Products = await _lookups.GetProductOptionsAsync(ct),
        Uoms = await _lookups.GetUomOptionsAsync(ct)
    };

    public async Task<List<Inv2003TransferDto>> GetListAsync(CancellationToken ct = default)
    {
        long companyNo = Company(), branchNo = Branch();

        var rows = await _db.InvStockTransfers.AsNoTracking()
            .Where(t => t.CompanyNo == companyNo && t.FromBranchNo == branchNo && t.IsDeleted == Deleted)
            .OrderByDescending(t => t.TransferNo)
            .ToListAsync(ct);

        var warehouses = await WarehouseNamesAsync(
            rows.Select(r => r.FromWarehouseNo).Concat(rows.Select(r => r.ToWarehouseNo)), ct);

        return rows.Select(t => ToHeaderDto(t, warehouses)).ToList();
    }

    public async Task<Inv2003TransferDto> GetDetailAsync(long transferNo, CancellationToken ct = default)
    {
        var t = await RequireAsync(transferNo, ct);

        var warehouses = await WarehouseNamesAsync(new[] { t.FromWarehouseNo, t.ToWarehouseNo }, ct);
        var dto = ToHeaderDto(t, warehouses);

        var lines = await _db.InvStockTransferDtls.AsNoTracking()
            .Where(d => d.TransferNo == transferNo)
            .OrderBy(d => d.LineNo)
            .ToListAsync(ct);

        dto.Lines = await ToLineDtosAsync(lines, ct);
        return dto;
    }

    // ── write ────────────────────────────────────────────────────────────────

    public async Task<Inv2003TransferDto> SaveAsync(Inv2003TransferDto dto, CancellationToken ct = default)
    {
        long companyNo = Company(), branchNo = Branch();

        if (dto.FromWarehouseNo == dto.ToWarehouseNo)
            throw new ValidationException("Source and destination warehouses must differ");

        var from = await RequireBranchWarehouseAsync(dto.FromWarehouseNo, ct);
        var to = await RequireBranchWarehouseAsync(dto.ToWarehouseNo, ct);

        var date = dto.TransferDate != default ? dto.TransferDate : DateTime.UtcNow.Date;
        var finYear = await _periodResolver.ResolveOpenFinYearAsync(branchNo, date, ct);

        InvStockTransfer t;
        if (dto.TransferNo is > 0)
        {
            t = await RequireAsync(dto.TransferNo.Value, ct);
            if (t.Status != StDraft) throw new ValidationException("Only a draft transfer can be edited");
        }
        else
        {
            t = new InvStockTransfer
            {
                CompanyNo = companyNo,
                Status = StDraft,
                TransferId = await _docSeq.NextAsync(companyNo, branchNo, DocType, "TRF", 6, ct),
                CreatedBy = _ctx.CurrentUserNo(),
                CreatedAt = DateTime.UtcNow,
                IsDeleted = 0
            };
            _db.InvStockTransfers.Add(t);
        }

        t.TransferDate = date;
        t.FromBranchNo = branchNo;
        t.FromWarehouseNo = from.WarehouseNo;
        t.ToBranchNo = branchNo;
        t.ToWarehouseNo = to.WarehouseNo;
        t.TransitWarehouseNo = await ResolveTransitAsync(branchNo, dto.TransitWarehouseNo, ct);
        t.Remarks = dto.Remarks;
        t.IsActive = dto.IsActive ?? 1;
        t.UpdatedBy = _ctx.CurrentUserNo();
        t.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        t.TotalQty = await ReconcileLinesAsync(t, dto.Lines, ct);
        await _db.SaveChangesAsync(ct);

        return await GetDetailAsync(t.TransferNo, ct);
    }

    public async Task<Inv2003TransferDto> SubmitAsync(long transferNo, CancellationToken ct = default)
    {
        var t = await RequireAsync(transferNo, ct);
        if (t.Status != StDraft) throw new ValidationException("Only a draft can be submitted");

        bool hasLines = await _db.InvStockTransferDtls.AnyAsync(d => d.TransferNo == transferNo, ct);
        if (!hasLines) throw new ValidationException("Add at least one line");

        t.Status = StSubmitted;
        await _db.SaveChangesAsync(ct);

        return await GetDetailAsync(transferNo, ct);
    }

    /// <summary>Phase one: source warehouse → transit, costed at the source cell's average cost.</summary>
    public async Task<Inv2003TransferDto> DispatchAsync(long transferNo, CancellationToken ct = default)
    {
        long companyNo = Company();
        var t = await RequireAsync(transferNo, ct);
        if (t.Status != StSubmitted) throw new ValidationException("Only a submitted transfer can be dispatched");

        long transit = t.TransitWarehouseNo ?? throw new ValidationException("No transit warehouse configured");
        var finYear = await _periodResolver.ResolveOpenFinYearAsync(t.FromBranchNo, t.TransferDate, ct);

        var lines = await _db.InvStockTransferDtls
            .Where(d => d.TransferNo == transferNo)
            .OrderBy(d => d.LineNo)
            .ToListAsync(ct);
        if (lines.Count == 0) throw new ValidationException("Nothing to dispatch");

        // One query for the source cells rather than one per line.
        var costs = await SourceAvgCostsAsync(t.FromWarehouseNo, lines, ct);

        var legs = new List<StockPostingLeg>();
        decimal totalValue = 0m;
        foreach (var d in lines)
        {
            decimal avgCost = costs.GetValueOrDefault(CellKey(d), 0m);
            d.UnitCost = avgCost;
            d.LineValue = Math.Round(d.QtySentBase * avgCost, 4);
            totalValue += d.LineValue;

            legs.Add(StockPostingLeg.Out(t.FromWarehouseNo, d.ProductNo, d.VariantNo, d.BatchNo,
                                         d.QtySentBase, MvOut, d.TransferDtlNo));
            legs.Add(StockPostingLeg.In(transit, d.ProductNo, d.VariantNo, d.BatchNo,
                                        d.QtySentBase, MvIn, d.TransferDtlNo, avgCost));
        }

        await _postingService.PostAsync(new StockPostingCommand(
            companyNo, t.FromBranchNo, RefDispatch, t.TransferId, t.TransferNo,
            t.TransferDate, finYear.FinYearNo, null, false, legs), ct);

        t.TotalValue = totalValue;
        t.Status = StDispatched;
        t.DispatchedBy = _ctx.CurrentUserNo();
        t.DispatchedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return await GetDetailAsync(transferNo, ct);
    }

    /// <summary>
    /// Phase two: transit → destination. Receiving less than was dispatched is allowed and marks the
    /// transfer short-received; the shortfall stays in transit for investigation.
    /// </summary>
    public async Task<Inv2003TransferDto> ReceiveAsync(long transferNo, Inv2003TransferDto? payload,
                                                       CancellationToken ct = default)
    {
        long companyNo = Company();
        var t = await RequireAsync(transferNo, ct);
        if (t.Status != StDispatched) throw new ValidationException("Only a dispatched transfer can be received");

        long transit = t.TransitWarehouseNo ?? throw new ValidationException("No transit warehouse configured");
        var finYear = await _periodResolver.ResolveOpenFinYearAsync(t.ToBranchNo, t.TransferDate, ct);

        var received = payload?.Lines?
            .Where(r => r.TransferDtlNo.HasValue && r.QtyReceived.HasValue)
            .ToDictionary(r => r.TransferDtlNo!.Value, r => r.QtyReceived!.Value)
            ?? new Dictionary<long, decimal>();

        var lines = await _db.InvStockTransferDtls
            .Where(d => d.TransferNo == transferNo)
            .OrderBy(d => d.LineNo)
            .ToListAsync(ct);

        var legs = new List<StockPostingLeg>();
        bool shortReceipt = false;

        foreach (var d in lines)
        {
            // Silence means "all of it arrived", matching the Java default.
            decimal recv = received.GetValueOrDefault(d.TransferDtlNo, d.QtySent);
            if (recv < 0) throw new ValidationException("Received qty cannot be negative");
            if (recv > d.QtySent) throw new ValidationException("Cannot receive more than dispatched");
            if (recv < d.QtySent) shortReceipt = true;

            // Reuse the line's own sent-to-base ratio so the receipt converts identically.
            decimal factor = d.QtySent == 0 ? 1m : Math.Round(d.QtySentBase / d.QtySent, 6);
            decimal recvBase = Math.Round(recv * factor, 4);

            d.QtyReceived = recv;
            d.QtyReceivedBase = recvBase;

            if (recvBase > 0)
            {
                legs.Add(StockPostingLeg.Out(transit, d.ProductNo, d.VariantNo, d.BatchNo,
                                             recvBase, MvOut, d.TransferDtlNo));
                legs.Add(StockPostingLeg.In(t.ToWarehouseNo, d.ProductNo, d.VariantNo, d.BatchNo,
                                            recvBase, MvIn, d.TransferDtlNo, d.UnitCost));
            }
        }

        if (legs.Count == 0) throw new ValidationException("Nothing received");

        await _postingService.PostAsync(new StockPostingCommand(
            companyNo, t.ToBranchNo, RefReceive, t.TransferId, t.TransferNo,
            t.TransferDate, finYear.FinYearNo, null, false, legs), ct);

        t.Status = shortReceipt ? StShortReceived : StReceived;
        t.ReceivedBy = _ctx.CurrentUserNo();
        t.ReceivedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return await GetDetailAsync(transferNo, ct);
    }

    public async Task<Inv2003TransferDto> CancelAsync(long transferNo, CancellationToken ct = default)
    {
        var t = await RequireAsync(transferNo, ct);
        if (t.Status != StDraft && t.Status != StSubmitted)
            throw new ValidationException("Only a draft/submitted transfer can be cancelled (dispatched stock must be received)");

        t.Status = StCancelled;
        await _db.SaveChangesAsync(ct);

        return await GetDetailAsync(transferNo, ct);
    }

    public async Task DeleteAsync(long transferNo, CancellationToken ct = default)
    {
        var t = await RequireAsync(transferNo, ct);
        if (t.Status >= StDispatched) throw new ValidationException("A dispatched/received transfer cannot be deleted");

        var lines = await _db.InvStockTransferDtls.Where(d => d.TransferNo == transferNo).ToListAsync(ct);
        _db.InvStockTransferDtls.RemoveRange(lines);

        t.PerformSoftDelete(_ctx.CurrentUserNo());
        await _db.SaveChangesAsync(ct);
    }

    // ── lines ────────────────────────────────────────────────────────────────

    private async Task<decimal> ReconcileLinesAsync(InvStockTransfer hdr, List<Inv2003TransferLineDto> rows,
                                                    CancellationToken ct)
    {
        var existing = await _db.InvStockTransferDtls.Where(d => d.TransferNo == hdr.TransferNo).ToListAsync(ct);
        _db.InvStockTransferDtls.RemoveRange(existing);

        if (rows == null || rows.Count == 0) return 0m;

        long companyNo = Company();
        var products = await ProductsAsync(rows.Select(r => r.ProductNo), companyNo, ct);
        var factors = await UomFactorsAsync(products.Keys, ct);

        decimal totalQty = 0m;
        int lineNo = 1;
        foreach (var r in rows)
        {
            if (!products.TryGetValue(r.ProductNo, out var product))
                throw new NotFoundException($"Product not found: {r.ProductNo}");

            decimal qtyBase = InvUomMath.ToBaseQty(product, r.UomNo, r.QtySent, factors);

            _db.InvStockTransferDtls.Add(new InvStockTransferDtl
            {
                TransferNo = hdr.TransferNo,
                LineNo = lineNo++,
                ProductNo = r.ProductNo,
                VariantNo = r.VariantNo,
                BatchNo = r.BatchNo,
                UomNo = r.UomNo ?? product.BaseUomNo,
                QtySent = r.QtySent,
                QtySentBase = qtyBase,
                Remarks = r.Remarks
            });

            totalQty += r.QtySent;
        }

        await _db.SaveChangesAsync(ct);
        return totalQty;
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static (long, long, long, long) CellKey(InvStockTransferDtl d) =>
        (d.ProductNo, d.VariantNo ?? 0, d.BatchNo ?? 0, 0);

    private async Task<Dictionary<(long, long, long, long), decimal>> SourceAvgCostsAsync(
        long warehouseNo, List<InvStockTransferDtl> lines, CancellationToken ct)
    {
        var productNos = lines.Select(l => l.ProductNo).Distinct().ToList();

        var cells = await _db.InvStocks.AsNoTracking()
            .Where(s => s.WarehouseNo == warehouseNo && productNos.Contains(s.ProductNo))
            .Select(s => new { s.ProductNo, s.VariantNo, s.BatchNo, s.AvgCost })
            .ToListAsync(ct);

        var map = new Dictionary<(long, long, long, long), decimal>();
        foreach (var c in cells)
            map.TryAdd((c.ProductNo, c.VariantNo ?? 0, c.BatchNo ?? 0, 0), c.AvgCost);
        return map;
    }

    /// <summary>Falls back to the branch's configured transit store when the caller names none.</summary>
    private async Task<long> ResolveTransitAsync(long branchNo, long? provided, CancellationToken ct)
    {
        if (provided is > 0) return provided.Value;

        var transit = await _db.InvWarehouses.AsNoTracking()
            .Where(w => w.BranchNo == branchNo && w.WarehouseType == TransitType && w.IsDeleted == Deleted)
            .OrderBy(w => w.WarehouseNo)
            .Select(w => (long?)w.WarehouseNo)
            .FirstOrDefaultAsync(ct);

        return transit ?? throw new ValidationException("Configure a Transit warehouse (type 3) for this branch");
    }

    private async Task<Dictionary<long, ProductInfo>> ProductsAsync(IEnumerable<long> productNos, long companyNo,
                                                                    CancellationToken ct)
    {
        var nos = productNos.Where(n => n > 0).Distinct().ToList();
        if (nos.Count == 0) return new Dictionary<long, ProductInfo>();

        return await _db.InvProducts.AsNoTracking()
            .Where(p => nos.Contains(p.ProductNo) && p.CompanyNo == companyNo && p.IsDeleted == Deleted)
            .Select(p => new ProductInfo(p.ProductNo, p.ProductId, p.ProductName, p.CompanyNo, null,
                                         p.BaseUomNo, p.IsBatchTracked))
            .ToDictionaryAsync(p => p.ProductNo, ct);
    }

    private async Task<Dictionary<(long, long), decimal>> UomFactorsAsync(IEnumerable<long> productNos,
                                                                          CancellationToken ct)
    {
        var nos = productNos.Distinct().ToList();
        if (nos.Count == 0) return new Dictionary<(long, long), decimal>();

        var rows = await _db.InvUomConversions.AsNoTracking()
            .Where(c => nos.Contains(c.ProductNo) && c.IsDeleted == Deleted)
            .OrderBy(c => c.UomConversionNo)
            .Select(c => new { c.ProductNo, c.FromUomNo, c.ToBaseFactor })
            .ToListAsync(ct);

        var map = new Dictionary<(long, long), decimal>();
        foreach (var r in rows) map.TryAdd((r.ProductNo, r.FromUomNo), r.ToBaseFactor);
        return map;
    }

    private async Task<Dictionary<long, string>> WarehouseNamesAsync(IEnumerable<long> warehouseNos,
                                                                     CancellationToken ct)
    {
        var nos = warehouseNos.Distinct().ToList();
        if (nos.Count == 0) return new Dictionary<long, string>();

        return await _db.InvWarehouses.AsNoTracking()
            .Where(w => nos.Contains(w.WarehouseNo) && w.IsDeleted == Deleted)
            .ToDictionaryAsync(w => w.WarehouseNo, w => w.WarehouseName, ct);
    }

    private async Task<List<Inv2003TransferLineDto>> ToLineDtosAsync(List<InvStockTransferDtl> lines,
                                                                     CancellationToken ct)
    {
        if (lines.Count == 0) return new List<Inv2003TransferLineDto>();

        var productNos = lines.Select(l => l.ProductNo).Distinct().ToList();
        var products = await _db.InvProducts.AsNoTracking()
            .Where(p => productNos.Contains(p.ProductNo))
            .ToDictionaryAsync(p => p.ProductNo, p => p.ProductName, ct);

        var uomNos = lines.Select(l => l.UomNo).Distinct().ToList();
        var uoms = await _db.InvUoms.AsNoTracking()
            .Where(u => uomNos.Contains(u.UomNo))
            .ToDictionaryAsync(u => u.UomNo, u => u.UomName, ct);

        var batchNos = lines.Where(l => l.BatchNo.HasValue).Select(l => l.BatchNo!.Value).Distinct().ToList();
        var batches = batchNos.Count == 0
            ? new Dictionary<long, string>()
            : await _db.InvBatches.AsNoTracking()
                .Where(b => batchNos.Contains(b.BatchNo))
                .ToDictionaryAsync(b => b.BatchNo, b => b.BatchCode, ct);

        return lines.Select(l => new Inv2003TransferLineDto
        {
            TransferDtlNo = l.TransferDtlNo,
            LineNo = l.LineNo,
            ProductNo = l.ProductNo,
            ProductName = products.GetValueOrDefault(l.ProductNo),
            VariantNo = l.VariantNo,
            UomNo = l.UomNo,
            UomName = uoms.GetValueOrDefault(l.UomNo),
            QtySent = l.QtySent,
            QtySentBase = l.QtySentBase,
            QtyReceived = l.QtyReceived,
            UnitCost = l.UnitCost,
            BatchNo = l.BatchNo,
            BatchCode = l.BatchNo.HasValue ? batches.GetValueOrDefault(l.BatchNo.Value) : null,
            Remarks = l.Remarks
        }).ToList();
    }

    private static Inv2003TransferDto ToHeaderDto(InvStockTransfer t, Dictionary<long, string> warehouses) => new()
    {
        TransferNo = t.TransferNo,
        TransferId = t.TransferId,
        TransferDate = t.TransferDate,
        FromWarehouseNo = t.FromWarehouseNo,
        FromWarehouseName = warehouses.GetValueOrDefault(t.FromWarehouseNo),
        ToWarehouseNo = t.ToWarehouseNo,
        ToWarehouseName = warehouses.GetValueOrDefault(t.ToWarehouseNo),
        TransitWarehouseNo = t.TransitWarehouseNo,
        Status = t.Status,
        TotalQty = t.TotalQty,
        TotalValue = t.TotalValue,
        Remarks = t.Remarks,
        IsActive = t.IsActive,
        RowVersion = t.RowVersion
    };

    // ── guards ───────────────────────────────────────────────────────────────

    private async Task<InvStockTransfer> RequireAsync(long transferNo, CancellationToken ct)
    {
        long companyNo = Company();
        var t = await _db.InvStockTransfers.FirstOrDefaultAsync(
                    x => x.TransferNo == transferNo && x.IsDeleted == Deleted, ct)
                ?? throw new NotFoundException("Transfer not found");

        if (t.CompanyNo != companyNo) throw new ValidationException("Transfer belongs to another company");
        return t;
    }

    private async Task<InvWarehouse> RequireBranchWarehouseAsync(long warehouseNo, CancellationToken ct)
    {
        if (warehouseNo <= 0) throw new ValidationException("Warehouse is required");

        var w = await _db.InvWarehouses.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.WarehouseNo == warehouseNo && x.IsDeleted == Deleted, ct)
                ?? throw new NotFoundException("Warehouse not found");

        if (w.BranchNo != Branch()) throw new ValidationException("Warehouse does not belong to your branch");
        return w;
    }

    private long Company() =>
        _ctx.CurrentCompanyNo() ?? throw new ValidationException("No active company in context");

    private long Branch() =>
        _ctx.CurrentBranchNo() ?? throw new ValidationException("No active branch in context");
}
