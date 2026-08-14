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

public interface IInv1101Service
{
    Task<Inv1101LookupDto> GetLookupsAsync(CancellationToken ct = default);
    Task<List<Inv1101OpeningDto>> GetListAsync(CancellationToken ct = default);
    Task<Inv1101OpeningDto> GetDetailAsync(long adjustmentNo, CancellationToken ct = default);
    Task<Inv1101OpeningDto> SaveAsync(Inv1101OpeningDto dto, CancellationToken ct = default);
    Task<Inv1101OpeningDto> PostAsync(long adjustmentNo, CancellationToken ct = default);
    Task DeleteAsync(long adjustmentNo, CancellationToken ct = default);
}

/// <summary>
/// INV_1101 Opening Stock — go-live balances held as an <c>inv_stock_adjustment</c> with
/// <c>adjustment_type = 2</c>. Draft → Posted; posting builds an all-IN stock command and is
/// refused once the financial year already carries any non-opening movement.
/// </summary>
public class Inv1101Service : IInv1101Service
{
    private const short Deleted = 0;
    private const short OpeningType = 2;
    private const short StatusDraft = 1, StatusPosted = 3;

    /// <summary><c>inv_stock_ledger</c> movement type and ref-doc type for an opening balance.</summary>
    private const short MvOpening = 1, RefOpening = 1;

    private const string DocType = "INV_ADJ";

    private readonly IInvDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    private readonly IInvStockPostingService _postingService;
    private readonly IInvLookupService _lookups;
    private readonly IInvPeriodResolver _periodResolver;
    private readonly IDocSequenceGenerator _docSeq;

    public Inv1101Service(IInvDbContext db, ICompanyBranchContext ctx, IInvStockPostingService postingService,
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

    public async Task<Inv1101LookupDto> GetLookupsAsync(CancellationToken ct = default) => new()
    {
        Warehouses = await _lookups.GetWarehouseOptionsAsync(ct),
        Products = await _lookups.GetProductOptionsAsync(ct),
        Uoms = await _lookups.GetUomOptionsAsync(ct)
    };

    public async Task<List<Inv1101OpeningDto>> GetListAsync(CancellationToken ct = default)
    {
        long companyNo = Company(), branchNo = Branch();

        var rows = await _db.InvStockAdjustments.AsNoTracking()
            .Where(a => a.CompanyNo == companyNo && a.BranchNo == branchNo
                        && a.AdjustmentType == OpeningType && a.IsDeleted == Deleted)
            .OrderByDescending(a => a.AdjustmentNo)
            .ToListAsync(ct);

        var warehouses = await WarehouseNamesAsync(rows.Select(r => r.WarehouseNo), ct);
        return rows.Select(a => ToHeaderDto(a, warehouses)).ToList();
    }

    public async Task<Inv1101OpeningDto> GetDetailAsync(long adjustmentNo, CancellationToken ct = default)
    {
        var a = await RequireOpeningAsync(adjustmentNo, ct);

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

    public async Task<Inv1101OpeningDto> SaveAsync(Inv1101OpeningDto dto, CancellationToken ct = default)
    {
        long companyNo = Company(), branchNo = Branch();

        await RequireWarehouseAsync(dto.WarehouseNo, ct);
        var date = dto.AdjustmentDate != default ? dto.AdjustmentDate : DateTime.UtcNow.Date;
        var finYear = await _periodResolver.ResolveOpenFinYearAsync(branchNo, date, ct);

        InvStockAdjustment a;
        if (dto.AdjustmentNo is > 0)
        {
            a = await RequireOpeningAsync(dto.AdjustmentNo.Value, ct);
            if (a.Status != StatusDraft)
                throw new ValidationException("Only draft opening documents can be edited");
        }
        else
        {
            a = new InvStockAdjustment
            {
                CompanyNo = companyNo,
                BranchNo = branchNo,
                AdjustmentType = OpeningType,
                Status = StatusDraft,
                AdjustmentId = await _docSeq.NextAsync(companyNo, branchNo, DocType, "OPN", 6, ct),
                CreatedBy = _ctx.CurrentUserNo(),
                CreatedAt = DateTime.UtcNow,
                IsDeleted = 0
            };
            _db.InvStockAdjustments.Add(a);
        }

        a.AdjustmentDate = date;
        a.WarehouseNo = dto.WarehouseNo;
        a.ReasonText = dto.ReasonText;
        a.Remarks = dto.Remarks;
        a.FinYearNo = finYear.FinYearNo;
        a.IsActive = dto.IsActive ?? 1;
        a.UpdatedBy = _ctx.CurrentUserNo();
        a.UpdatedAt = DateTime.UtcNow;

        // The header PK is needed before its lines can reference it.
        await _db.SaveChangesAsync(ct);

        a.TotalInValue = await ReconcileLinesAsync(a, dto.Lines, ct);
        a.TotalOutValue = 0m;
        await _db.SaveChangesAsync(ct);

        return await GetDetailAsync(a.AdjustmentNo, ct);
    }

    public async Task<Inv1101OpeningDto> PostAsync(long adjustmentNo, CancellationToken ct = default)
    {
        long companyNo = Company();
        var a = await RequireOpeningAsync(adjustmentNo, ct);

        if (a.Status != StatusDraft) throw new ValidationException("Only draft opening documents can be posted");

        var finYear = await _periodResolver.ResolveOpenFinYearAsync(a.BranchNo, a.AdjustmentDate, ct);

        // Opening balances are only meaningful before the year has any real trading movement.
        bool hasOtherMovement = await _db.InvStockLedgers
            .AnyAsync(l => l.CompanyNo == companyNo && l.FinYearNo == finYear.FinYearNo
                           && l.MovementType != MvOpening, ct);
        if (hasOtherMovement)
            throw new ValidationException("Opening stock is locked — the financial year already has other movements");

        var lines = await _db.InvStockAdjustmentDtls
            .Where(d => d.AdjustmentNo == adjustmentNo)
            .OrderBy(d => d.LineNo)
            .ToListAsync(ct);
        if (lines.Count == 0) throw new ValidationException("Add at least one line before posting");

        var legs = lines.Select(d => StockPostingLeg.In(
            a.WarehouseNo, d.ProductNo, d.VariantNo, d.BatchNo, d.QtyBase, MvOpening, d.AdjustmentDtlNo,
            d.UnitCost)).ToList();

        await _postingService.PostAsync(new StockPostingCommand(
            companyNo, a.BranchNo, RefOpening, a.AdjustmentId, a.AdjustmentNo,
            a.AdjustmentDate, finYear.FinYearNo, null, false, legs), ct);

        a.Status = StatusPosted;
        a.ApprovedBy = _ctx.CurrentUserNo();
        a.ApprovedAt = DateTime.UtcNow;
        a.PostedAt = DateTime.UtcNow;

        EmitOpeningStockPosted(a);
        await _db.SaveChangesAsync(ct);

        return await GetDetailAsync(adjustmentNo, ct);
    }

    public async Task DeleteAsync(long adjustmentNo, CancellationToken ct = default)
    {
        var a = await RequireOpeningAsync(adjustmentNo, ct);
        if (a.Status == StatusPosted) throw new ValidationException("A posted opening document cannot be deleted");

        var lines = await _db.InvStockAdjustmentDtls.Where(d => d.AdjustmentNo == adjustmentNo).ToListAsync(ct);
        _db.InvStockAdjustmentDtls.RemoveRange(lines);

        a.PerformSoftDelete(_ctx.CurrentUserNo());
        await _db.SaveChangesAsync(ct);
    }

    // ── GL hand-off ──────────────────────────────────────────────────────────

    /// <summary>
    /// Stages <c>OpeningStockPosted</c> for FIN: Dr INVENTORY / Cr OPENING_EQUITY. The row goes out
    /// in the caller's transaction, so the event cannot exist without the stock movement.
    /// </summary>
    private void EmitOpeningStockPosted(InvStockAdjustment a)
    {
        if (a.TotalInValue <= 0) return;

        var payload = new GlPostingPayload
        {
            VoucherDate = a.AdjustmentDate,
            Narration = $"Opening stock {a.AdjustmentId}",
            BranchNo = a.BranchNo,
            Legs = new List<GlPostingPayload.Leg>
            {
                new() { LegKey = "INVENTORY", Amount = a.TotalInValue, DrCr = "dr" },
                new() { LegKey = "OPENING_EQUITY", Amount = a.TotalInValue, DrCr = "cr" }
            }
        };

        _db.EventOutboxes.Add(new EventOutbox
        {
            CompanyNo = a.CompanyNo,
            BranchNo = a.BranchNo,
            EventType = "OpeningStockPosted",
            AggregateType = "INV_OPENING",
            AggregateId = a.AdjustmentNo.ToString(),
            Payload = JsonSerializer.Serialize(payload),
            Status = 1,
            CreatedAt = DateTime.UtcNow
        });
    }

    // ── lines ────────────────────────────────────────────────────────────────

    /// <summary>Draft lines are replaced wholesale; returns the document's total IN value.</summary>
    private async Task<decimal> ReconcileLinesAsync(InvStockAdjustment hdr, List<Inv1101LineDto> rows,
                                                    CancellationToken ct)
    {
        var existing = await _db.InvStockAdjustmentDtls.Where(d => d.AdjustmentNo == hdr.AdjustmentNo).ToListAsync(ct);
        _db.InvStockAdjustmentDtls.RemoveRange(existing);

        if (rows == null || rows.Count == 0) return 0m;

        long companyNo = Company();
        var products = await ProductsAsync(rows.Select(r => r.ProductNo), companyNo, ct);
        var factors = await UomFactorsAsync(products.Keys, ct);

        decimal total = 0m;
        int lineNo = 1;
        foreach (var r in rows)
        {
            if (!products.TryGetValue(r.ProductNo, out var product))
                throw new NotFoundException($"Product not found: {r.ProductNo}");

            decimal qtyBase = InvUomMath.ToBaseQty(product, r.UomNo, r.Qty, factors);
            decimal lineValue = Math.Round(qtyBase * r.UnitCost, 4);

            _db.InvStockAdjustmentDtls.Add(new InvStockAdjustmentDtl
            {
                AdjustmentNo = hdr.AdjustmentNo,
                LineNo = lineNo++,
                ProductNo = r.ProductNo,
                VariantNo = r.VariantNo,
                UomNo = r.UomNo ?? product.BaseUomNo,
                Direction = 1,
                Qty = r.Qty,
                QtyBase = qtyBase,
                UnitCost = r.UnitCost,
                LineValue = lineValue,
                Remarks = r.Remarks,
                BatchNo = await ResolveBatchAsync(product, r, ct)
            });

            total += lineValue;
        }

        await _db.SaveChangesAsync(ct);
        return total;
    }

    /// <summary>
    /// Batch-tracked lines find-or-create their <c>inv_batch</c> at draft save, so posting has a
    /// batch to move stock into.
    /// </summary>
    private async Task<long?> ResolveBatchAsync(ProductInfo product, Inv1101LineDto r, CancellationToken ct)
    {
        if (product.IsBatchTracked != 1) return null;
        if (string.IsNullOrWhiteSpace(r.BatchCode))
            throw new ValidationException($"Batch code is required for batch-tracked product: {product.ProductId}");

        long companyNo = Company();
        var batch = await _db.InvBatches.FirstOrDefaultAsync(
            b => b.CompanyNo == companyNo && b.ProductNo == product.ProductNo
                 && b.VariantNo == r.VariantNo && b.BatchCode == r.BatchCode && b.IsDeleted == Deleted, ct);

        if (batch != null) return batch.BatchNo;

        batch = new InvBatch
        {
            CompanyNo = companyNo,
            ProductNo = product.ProductNo,
            VariantNo = r.VariantNo,
            BatchCode = r.BatchCode!,
            MfgDate = r.MfgDate,
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

    private async Task<List<Inv1101LineDto>> ToLineDtosAsync(List<InvStockAdjustmentDtl> lines, CancellationToken ct)
    {
        if (lines.Count == 0) return new List<Inv1101LineDto>();

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
            var dto = new Inv1101LineDto
            {
                AdjustmentDtlNo = l.AdjustmentDtlNo,
                LineNo = l.LineNo,
                ProductNo = l.ProductNo,
                ProductName = products.GetValueOrDefault(l.ProductNo),
                VariantNo = l.VariantNo,
                UomNo = l.UomNo,
                UomName = uoms.GetValueOrDefault(l.UomNo),
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
                dto.MfgDate = b.MfgDate;
                dto.ExpiryDate = b.ExpiryDate;
            }

            return dto;
        }).ToList();
    }

    private static Inv1101OpeningDto ToHeaderDto(InvStockAdjustment a, Dictionary<long, string> warehouses) => new()
    {
        AdjustmentNo = a.AdjustmentNo,
        AdjustmentId = a.AdjustmentId,
        AdjustmentDate = a.AdjustmentDate,
        WarehouseNo = a.WarehouseNo,
        WarehouseName = warehouses.GetValueOrDefault(a.WarehouseNo),
        Status = a.Status,
        FinYearNo = a.FinYearNo,
        ReasonText = a.ReasonText,
        Remarks = a.Remarks,
        TotalInValue = a.TotalInValue,
        IsActive = a.IsActive,
        RowVersion = a.RowVersion
    };

    // ── guards ───────────────────────────────────────────────────────────────

    private async Task<InvStockAdjustment> RequireOpeningAsync(long adjustmentNo, CancellationToken ct)
    {
        long companyNo = Company();
        var a = await _db.InvStockAdjustments.FirstOrDefaultAsync(
                    x => x.AdjustmentNo == adjustmentNo && x.IsDeleted == Deleted, ct)
                ?? throw new NotFoundException("Opening document not found");

        if (a.CompanyNo != companyNo || a.AdjustmentType != OpeningType)
            throw new ValidationException("Not an opening-stock document");

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
