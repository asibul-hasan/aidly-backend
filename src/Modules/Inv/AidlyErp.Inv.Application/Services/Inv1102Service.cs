using System.Text.Json;
using AidlyErp.Fin.Contracts;
using AidlyErp.Inv.Application.Dto;
using AidlyErp.Inv.Application.Interfaces;
using AidlyErp.Inv.Contracts;
using AidlyErp.Inv.Domain;
using AidlyErp.Shared.Core;
using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Sys.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AidlyErp.Inv.Application.Services;

public interface IInv1102Service
{
    Task<Inv1102LookupDto> GetLookupsAsync(CancellationToken ct = default);
    Task<List<Inv1102AdjustmentDto>> GetListAsync(CancellationToken ct = default);
    Task<Inv1102AdjustmentDto> GetDetailAsync(long adjustmentNo, CancellationToken ct = default);
    Task<Inv1102AdjustmentDto> SaveAsync(Inv1102AdjustmentDto dto, CancellationToken ct = default);
    Task<Inv1102AdjustmentDto> SubmitAsync(long adjustmentNo, CancellationToken ct = default);
    Task<Inv1102AdjustmentDto> ApproveAsync(long adjustmentNo, CancellationToken ct = default);
    Task<Inv1102AdjustmentDto> CancelAsync(long adjustmentNo, CancellationToken ct = default);
    Task DeleteAsync(long adjustmentNo, CancellationToken ct = default);
}

/// <summary>
/// INV_1102 Stock Adjustment — Draft → Submitted → Posted, with Cancel writing a reversing stock
/// movement. Every quantity change goes through the posting engine; this service never touches
/// <c>inv_stock</c> itself.
/// </summary>
public class Inv1102Service : IInv1102Service
{
    private const short Deleted = 0;
    private const short StDraft = 1, StSubmitted = 2, StPosted = 3, StCancelled = 4;

    /// <summary><c>inv_stock_ledger.ref_doc_type</c> for an adjustment.</summary>
    private const short RefAdj = 8;

    /// <summary>Movement types: 8 adjustment-in, 9 adjustment-out, 10 stock count.</summary>
    private const short MvAdjIn = 8, MvAdjOut = 9, MvCount = 10;

    /// <summary><c>adjustment_type = 5</c> is a physical count, which posts as a count movement.</summary>
    private const short AdjTypeCount = 5;

    /// <summary>1=Manual 3=Damage 4=Loss 5=Count 6=Found. Type 2 belongs to INV_1101 Opening Stock.</summary>
    private static readonly HashSet<short> ValidTypes = new() { 1, 3, 4, 5, 6 };

    private const string DocType = "INV_ADJ";

    private readonly IInvDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    private readonly IInvStockPostingService _postingService;
    private readonly IInvLookupService _lookups;
    private readonly IInvPeriodResolver _periodResolver;
    private readonly IDocSequenceGenerator _docSeq;

    public Inv1102Service(IInvDbContext db, ICompanyBranchContext ctx, IInvStockPostingService postingService,
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

    public async Task<Inv1102LookupDto> GetLookupsAsync(CancellationToken ct = default) => new()
    {
        Warehouses = await _lookups.GetWarehouseOptionsAsync(ct),
        Products = await _lookups.GetProductOptionsAsync(ct),
        Uoms = await _lookups.GetUomOptionsAsync(ct)
    };

    public async Task<List<Inv1102AdjustmentDto>> GetListAsync(CancellationToken ct = default)
    {
        long companyNo = Company(), branchNo = Branch();

        var rows = await _db.InvStockAdjustments.AsNoTracking()
            .Where(a => a.CompanyNo == companyNo && a.BranchNo == branchNo
                        && a.AdjustmentType != Inv1101Constants.OpeningType && a.IsDeleted == Deleted)
            .OrderByDescending(a => a.AdjustmentNo)
            .ToListAsync(ct);

        var warehouses = await WarehouseNamesAsync(rows.Select(r => r.WarehouseNo), ct);
        return rows.Select(a => ToHeaderDto(a, warehouses)).ToList();
    }

    public async Task<Inv1102AdjustmentDto> GetDetailAsync(long adjustmentNo, CancellationToken ct = default)
    {
        var a = await RequireAdjustmentAsync(adjustmentNo, ct);

        var warehouses = await WarehouseNamesAsync(new[] { a.WarehouseNo }, ct);
        var dto = ToHeaderDto(a, warehouses);

        var lines = await _db.InvStockAdjustmentDtls.AsNoTracking()
            .Where(d => d.AdjustmentNo == adjustmentNo)
            .OrderBy(d => d.LineNo)
            .ToListAsync(ct);

        dto.Lines = await ToLineDtosAsync(lines, ct);
        return dto;
    }

    // ── write ────────────────────────────────────────────────────────────────

    public async Task<Inv1102AdjustmentDto> SaveAsync(Inv1102AdjustmentDto dto, CancellationToken ct = default)
    {
        long companyNo = Company(), branchNo = Branch();

        // Type 2 is reserved for opening stock (INV_1101), so it is not offered here.
        if (!ValidTypes.Contains(dto.AdjustmentType)) throw new ValidationException("Invalid adjustment type");

        // Writing off stock without a stated cause is exactly what a fraud review looks for.
        if (dto.AdjustmentType is 3 or 4 && string.IsNullOrWhiteSpace(dto.ReasonText))
            throw new ValidationException("Reason is required for damage/loss adjustments");

        await RequireWarehouseAsync(dto.WarehouseNo, ct);
        var date = dto.AdjustmentDate != default ? dto.AdjustmentDate : DateTime.UtcNow.Date;
        var finYear = await _periodResolver.ResolveOpenFinYearAsync(branchNo, date, ct);

        InvStockAdjustment a;
        if (dto.AdjustmentNo is > 0)
        {
            a = await RequireAdjustmentAsync(dto.AdjustmentNo.Value, ct);
            if (a.Status != StDraft) throw new ValidationException("Only a draft adjustment can be edited");
        }
        else
        {
            a = new InvStockAdjustment
            {
                CompanyNo = companyNo,
                BranchNo = branchNo,
                Status = StDraft,
                AdjustmentId = await _docSeq.NextAsync(companyNo, branchNo, DocType, "ADJ", 6, ct),
                CreatedBy = _ctx.CurrentUserNo(),
                CreatedAt = DateTime.UtcNow,
                IsDeleted = 0
            };
            _db.InvStockAdjustments.Add(a);
        }

        a.AdjustmentDate = date;
        a.WarehouseNo = dto.WarehouseNo;
        a.AdjustmentType = dto.AdjustmentType > 0 ? dto.AdjustmentType : (short)1;
        a.ReasonText = dto.ReasonText;
        a.Remarks = dto.Remarks;
        a.FinYearNo = finYear.FinYearNo;
        a.IsActive = dto.IsActive ?? 1;
        a.UpdatedBy = _ctx.CurrentUserNo();
        a.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        var (totalIn, totalOut) = await ReconcileLinesAsync(a, dto.Lines, ct);
        a.TotalInValue = totalIn;
        a.TotalOutValue = totalOut;
        await _db.SaveChangesAsync(ct);

        return await GetDetailAsync(a.AdjustmentNo, ct);
    }

    public async Task<Inv1102AdjustmentDto> SubmitAsync(long adjustmentNo, CancellationToken ct = default)
    {
        var a = await RequireAdjustmentAsync(adjustmentNo, ct);
        if (a.Status != StDraft) throw new ValidationException("Only a draft can be submitted");

        bool hasLines = await _db.InvStockAdjustmentDtls.AnyAsync(d => d.AdjustmentNo == adjustmentNo, ct);
        if (!hasLines) throw new ValidationException("Add at least one line before submitting");

        a.Status = StSubmitted;
        a.SubmittedBy = _ctx.CurrentUserNo();
        a.SubmittedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return await GetDetailAsync(adjustmentNo, ct);
    }

    public async Task<Inv1102AdjustmentDto> ApproveAsync(long adjustmentNo, CancellationToken ct = default)
    {
        long companyNo = Company();
        var a = await RequireAdjustmentAsync(adjustmentNo, ct);
        if (a.Status != StSubmitted) throw new ValidationException("Only a submitted adjustment can be approved");

        var finYear = await _periodResolver.ResolveOpenFinYearAsync(a.BranchNo, a.AdjustmentDate, ct);

        var lines = await _db.InvStockAdjustmentDtls
            .Where(d => d.AdjustmentNo == adjustmentNo)
            .OrderBy(d => d.LineNo)
            .ToListAsync(ct);
        if (lines.Count == 0) throw new ValidationException("Nothing to post");

        var legs = lines.Select(d =>
        {
            short mt = a.AdjustmentType == AdjTypeCount ? MvCount : (d.Direction == 1 ? MvAdjIn : MvAdjOut);
            return d.Direction == 1
                ? StockPostingLeg.In(a.WarehouseNo, d.ProductNo, d.VariantNo, d.BatchNo, d.QtyBase, mt,
                                     d.AdjustmentDtlNo, d.UnitCost)
                : StockPostingLeg.Out(a.WarehouseNo, d.ProductNo, d.VariantNo, d.BatchNo, d.QtyBase, mt,
                                      d.AdjustmentDtlNo);
        }).ToList();

        await _postingService.PostAsync(new StockPostingCommand(
            companyNo, a.BranchNo, RefAdj, a.AdjustmentId, a.AdjustmentNo,
            a.AdjustmentDate, finYear.FinYearNo, null, false, legs), ct);

        a.Status = StPosted;
        a.ApprovedBy = _ctx.CurrentUserNo();
        a.ApprovedAt = DateTime.UtcNow;
        a.PostedAt = DateTime.UtcNow;

        EmitGlEvent(a, "StockAdjustmentPosted", reverse: false);
        await _db.SaveChangesAsync(ct);

        return await GetDetailAsync(adjustmentNo, ct);
    }

    public async Task<Inv1102AdjustmentDto> CancelAsync(long adjustmentNo, CancellationToken ct = default)
    {
        long companyNo = Company();
        var a = await RequireAdjustmentAsync(adjustmentNo, ct);
        if (a.Status != StPosted) throw new ValidationException("Only a posted adjustment can be cancelled");

        var finYear = await _periodResolver.ResolveOpenFinYearAsync(a.BranchNo, a.AdjustmentDate, ct);

        // Reverse from the ledger rows the approval actually wrote, so the reversal matches what
        // moved even if the document's lines were something else.
        var originals = await _db.InvStockLedgers.AsNoTracking()
            .Where(l => l.RefDocType == RefAdj && l.RefDocNo == a.AdjustmentId && l.IsReversal == 0)
            .OrderBy(l => l.StockLedgerNo)
            .ToListAsync(ct);
        if (originals.Count == 0) throw new ValidationException("No posted movements found to reverse");

        var legs = originals.Select(r => r.Direction == 1
            ? StockPostingLeg.Out(r.WarehouseNo, r.ProductNo, r.VariantNo, r.BatchNo, r.QtyBase,
                                  r.MovementType, r.RefLineNo)
            : StockPostingLeg.In(r.WarehouseNo, r.ProductNo, r.VariantNo, r.BatchNo, r.QtyBase,
                                 r.MovementType, r.RefLineNo, r.UnitCost)).ToList();

        await _postingService.PostAsync(new StockPostingCommand(
            companyNo, a.BranchNo, RefAdj, a.AdjustmentId, a.AdjustmentNo,
            a.AdjustmentDate, finYear.FinYearNo, null, true, legs), ct);

        a.Status = StCancelled;

        // A distinct event type keeps the posting engine's idempotency guard from deduping the
        // reversal against the original.
        EmitGlEvent(a, "StockAdjustmentReversed", reverse: true);
        await _db.SaveChangesAsync(ct);

        return await GetDetailAsync(adjustmentNo, ct);
    }

    public async Task DeleteAsync(long adjustmentNo, CancellationToken ct = default)
    {
        var a = await RequireAdjustmentAsync(adjustmentNo, ct);
        if (a.Status == StPosted)
            throw new ValidationException("A posted adjustment cannot be deleted — cancel it instead");

        var lines = await _db.InvStockAdjustmentDtls.Where(d => d.AdjustmentNo == adjustmentNo).ToListAsync(ct);
        _db.InvStockAdjustmentDtls.RemoveRange(lines);

        a.PerformSoftDelete(_ctx.CurrentUserNo());
        await _db.SaveChangesAsync(ct);
    }

    // ── GL hand-off ──────────────────────────────────────────────────────────

    /// <summary>
    /// Stages the net inventory movement for FIN: Dr INVENTORY / Cr ADJUSTMENT when stock rises,
    /// flipped when it falls. A pure reclassification nets to zero and emits nothing.
    /// </summary>
    private void EmitGlEvent(InvStockAdjustment a, string eventType, bool reverse)
    {
        decimal net = a.TotalInValue - a.TotalOutValue;
        if (net == 0) return;

        bool stockRose = net > 0;
        decimal amount = Math.Abs(net);

        string inventoryDrCr = stockRose ? "dr" : "cr";
        string adjustmentDrCr = stockRose ? "cr" : "dr";
        if (reverse)
        {
            (inventoryDrCr, adjustmentDrCr) = (adjustmentDrCr, inventoryDrCr);
        }

        var payload = new GlPostingPayload
        {
            VoucherDate = a.AdjustmentDate,
            Narration = (reverse ? "Stock adjustment reversal " : "Stock adjustment ") + a.AdjustmentId,
            BranchNo = a.BranchNo,
            Legs = new List<GlPostingPayload.Leg>
            {
                new() { LegKey = "INVENTORY", Amount = amount, DrCr = inventoryDrCr },
                new() { LegKey = "ADJUSTMENT", Amount = amount, DrCr = adjustmentDrCr }
            }
        };

        _db.EventOutboxes.Add(new EventOutbox
        {
            CompanyNo = a.CompanyNo,
            BranchNo = a.BranchNo,
            EventType = eventType,
            AggregateType = "INV_ADJ",
            AggregateId = a.AdjustmentNo.ToString(),
            Payload = JsonSerializer.Serialize(payload),
            Status = 1,
            CreatedAt = DateTime.UtcNow
        });
    }

    // ── lines ────────────────────────────────────────────────────────────────

    private async Task<(decimal TotalIn, decimal TotalOut)> ReconcileLinesAsync(
        InvStockAdjustment hdr, List<Inv1102LineDto> rows, CancellationToken ct)
    {
        var existing = await _db.InvStockAdjustmentDtls.Where(d => d.AdjustmentNo == hdr.AdjustmentNo).ToListAsync(ct);
        _db.InvStockAdjustmentDtls.RemoveRange(existing);

        if (rows == null || rows.Count == 0) return (0m, 0m);

        long companyNo = Company();
        var products = await ProductsAsync(rows.Select(r => r.ProductNo), companyNo, ct);
        var factors = await UomFactorsAsync(products.Keys, ct);

        decimal totalIn = 0m, totalOut = 0m;
        int lineNo = 1;
        foreach (var r in rows)
        {
            if (!products.TryGetValue(r.ProductNo, out var product))
                throw new NotFoundException($"Product not found: {r.ProductNo}");

            if (r.Direction != 1 && r.Direction != -1)
                throw new ValidationException("Each line needs a direction (increase or decrease)");

            decimal qtyBase = InvUomMath.ToBaseQty(product, r.UomNo, r.Qty, factors);
            decimal lineValue = Math.Round(qtyBase * r.UnitCost, 4);
            short direction = r.Direction;

            _db.InvStockAdjustmentDtls.Add(new InvStockAdjustmentDtl
            {
                AdjustmentNo = hdr.AdjustmentNo,
                LineNo = lineNo++,
                ProductNo = r.ProductNo,
                VariantNo = r.VariantNo,
                UomNo = r.UomNo ?? product.BaseUomNo,
                Direction = direction,
                Qty = r.Qty,
                QtyBase = qtyBase,
                UnitCost = r.UnitCost,
                LineValue = lineValue,
                Remarks = r.Remarks,
                BatchNo = await ResolveBatchAsync(product, r, ct)
            });

            if (direction == 1) totalIn += lineValue; else totalOut += lineValue;
        }

        await _db.SaveChangesAsync(ct);
        return (totalIn, totalOut);
    }

    private async Task<long?> ResolveBatchAsync(ProductInfo product, Inv1102LineDto r, CancellationToken ct)
    {
        if (product.IsBatchTracked != 1) return null;
        if (string.IsNullOrWhiteSpace(r.BatchCode))
            throw new ValidationException($"Batch code is required for batch-tracked product: {product.ProductId}");

        long companyNo = Company();
        var batch = await _db.InvBatches.FirstOrDefaultAsync(
            b => b.CompanyNo == companyNo && b.ProductNo == product.ProductNo
                 && b.VariantNo == r.VariantNo && b.BatchCode == r.BatchCode && b.IsDeleted == Deleted, ct);

        if (batch != null) return batch.BatchNo;

        // Creating a batch in order to take stock out of it would manufacture inventory that never
        // existed; only an increase may open a new batch.
        if (r.Direction == -1) throw new ValidationException($"Batch not found for decrease: {r.BatchCode}");

        batch = new InvBatch
        {
            CompanyNo = companyNo,
            ProductNo = product.ProductNo,
            VariantNo = r.VariantNo,
            BatchCode = r.BatchCode!,
            ExpiryDate = r.ExpiryDate,
            ReceivedCost = r.UnitCost,
            CreatedBy = _ctx.CurrentUserNo(),
            CreatedAt = DateTime.UtcNow,
            IsDeleted = 0
        };
        _db.InvBatches.Add(batch);
        await _db.SaveChangesAsync(ct);
        return batch.BatchNo;
    }

    // ── mapping helpers ──────────────────────────────────────────────────────

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

    private async Task<List<Inv1102LineDto>> ToLineDtosAsync(List<InvStockAdjustmentDtl> lines, CancellationToken ct)
    {
        if (lines.Count == 0) return new List<Inv1102LineDto>();

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
            ? new Dictionary<long, InvBatch>()
            : await _db.InvBatches.AsNoTracking()
                .Where(b => batchNos.Contains(b.BatchNo))
                .ToDictionaryAsync(b => b.BatchNo, ct);

        return lines.Select(l =>
        {
            var dto = new Inv1102LineDto
            {
                AdjustmentDtlNo = l.AdjustmentDtlNo,
                LineNo = l.LineNo,
                ProductNo = l.ProductNo,
                ProductName = products.GetValueOrDefault(l.ProductNo),
                VariantNo = l.VariantNo,
                UomNo = l.UomNo,
                UomName = uoms.GetValueOrDefault(l.UomNo),
                Direction = l.Direction,
                Qty = l.Qty,
                QtyBase = l.QtyBase,
                UnitCost = l.UnitCost,
                LineValue = l.LineValue,
                BatchNo = l.BatchNo,
                Remarks = l.Remarks
            };

            if (l.BatchNo.HasValue && batches.TryGetValue(l.BatchNo.Value, out var b))
            {
                dto.BatchCode = b.BatchCode;
                dto.ExpiryDate = b.ExpiryDate;
            }

            return dto;
        }).ToList();
    }

    private static Inv1102AdjustmentDto ToHeaderDto(InvStockAdjustment a, Dictionary<long, string> warehouses) => new()
    {
        AdjustmentNo = a.AdjustmentNo,
        AdjustmentId = a.AdjustmentId,
        AdjustmentDate = a.AdjustmentDate,
        WarehouseNo = a.WarehouseNo,
        WarehouseName = warehouses.GetValueOrDefault(a.WarehouseNo),
        AdjustmentType = a.AdjustmentType,
        ReasonText = a.ReasonText,
        Status = a.Status,
        TotalInValue = a.TotalInValue,
        TotalOutValue = a.TotalOutValue,
        FinYearNo = a.FinYearNo,
        Remarks = a.Remarks,
        IsActive = a.IsActive,
        RowVersion = a.RowVersion
    };

    // ── guards ───────────────────────────────────────────────────────────────

    private async Task<InvStockAdjustment> RequireAdjustmentAsync(long adjustmentNo, CancellationToken ct)
    {
        long companyNo = Company();
        var a = await _db.InvStockAdjustments.FirstOrDefaultAsync(
                    x => x.AdjustmentNo == adjustmentNo && x.IsDeleted == Deleted, ct)
                ?? throw new NotFoundException("Adjustment not found");

        if (a.CompanyNo != companyNo) throw new ValidationException("Adjustment belongs to another company");
        if (a.AdjustmentType == Inv1101Constants.OpeningType)
            throw new ValidationException("This is an opening-stock document — use INV_1101");

        return a;
    }

    private async Task RequireWarehouseAsync(long warehouseNo, CancellationToken ct)
    {
        if (warehouseNo <= 0) throw new ValidationException("Warehouse is required");

        var w = await _db.InvWarehouses.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.WarehouseNo == warehouseNo && x.IsDeleted == Deleted, ct)
                ?? throw new NotFoundException("Warehouse not found");

        if (w.BranchNo != Branch()) throw new ValidationException("Warehouse does not belong to your branch");
    }

    private long Company() =>
        _ctx.CurrentCompanyNo() ?? throw new ValidationException("No active company in context");

    private long Branch() =>
        _ctx.CurrentBranchNo() ?? throw new ValidationException("No active branch in context");
}

/// <summary>Shared with INV_1101, which stores opening balances in the same adjustment table.</summary>
internal static class Inv1101Constants
{
    public const short OpeningType = 2;
}
