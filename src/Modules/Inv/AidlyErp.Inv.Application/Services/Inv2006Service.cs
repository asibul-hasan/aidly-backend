using AidlyErp.Inv.Application.Dto;
using AidlyErp.Inv.Application.Interfaces;
using AidlyErp.Inv.Domain;
using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Sys.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AidlyErp.Inv.Application.Services;

public interface IInv2006Service
{
    Task<List<Inv2006CountDto>> GetListAsync(CancellationToken ct = default);
    Task<Inv2006CountDto> GetDetailAsync(long countNo, CancellationToken ct = default);
    Task<Inv2006CountDto> StartAsync(Inv2006StartCountDto dto, CancellationToken ct = default);
    Task<Inv2006CountDto> SaveCountsAsync(long countNo, List<Inv2006CountLineDto> lines,
                                          CancellationToken ct = default);
    Task<Inv2006CountDto> PostAsync(long countNo, CancellationToken ct = default);
    Task<Inv2006CountDto> CancelAsync(long countNo, CancellationToken ct = default);
    Task DeleteAsync(long countNo, CancellationToken ct = default);
}

/// <summary>
/// INV_2006 Physical Count. Draft → Counting → Review → Posted.
///
/// <para>Opening a sheet snapshots what the system holds. That snapshot is then frozen: the
/// variance must reflect what the counter found against what was believed <i>at that moment</i>,
/// otherwise a sale made mid-count gets blamed on the person holding the clipboard.</para>
///
/// <para>Posting builds an <c>inv_stock_adjustment</c> of type Count and puts it through the
/// posting engine. The count sheet itself never writes stock.</para>
/// </summary>
public class Inv2006Service : IInv2006Service
{
    private const short Deleted = 0;
    private const short StDraft = 1, StCounting = 2, StReview = 3, StPosted = 4, StCancelled = 5;

    /// <summary><c>adjustment_type</c> 5 = physical count, which posts as movement type 10.</summary>
    private const short AdjTypeCount = 5;

    private const string DocType = "INV_COUNT";

    private readonly IInvDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    private readonly IInv1102Service _adjustments;
    private readonly IDocSequenceGenerator _docSeq;

    public Inv2006Service(IInvDbContext db, ICompanyBranchContext ctx, IInv1102Service adjustments,
                          IDocSequenceGenerator docSeq)
    {
        _db = db;
        _ctx = ctx;
        _adjustments = adjustments;
        _docSeq = docSeq;
    }

    // ── reads ────────────────────────────────────────────────────────────────

    public async Task<List<Inv2006CountDto>> GetListAsync(CancellationToken ct = default)
    {
        long companyNo = Company(), branchNo = Branch();

        var rows = await _db.InvPhysicalCounts.AsNoTracking()
            .Where(c => c.CompanyNo == companyNo && c.BranchNo == branchNo && c.IsDeleted == Deleted)
            .OrderByDescending(c => c.CountNo)
            .ToListAsync(ct);

        var warehouses = await WarehouseNamesAsync(rows.Select(r => r.WarehouseNo), ct);
        return rows.Select(c => ToDto(c, warehouses.GetValueOrDefault(c.WarehouseNo))).ToList();
    }

    public async Task<Inv2006CountDto> GetDetailAsync(long countNo, CancellationToken ct = default)
    {
        var sheet = await RequireAsync(countNo, ct);
        var warehouses = await WarehouseNamesAsync(new[] { sheet.WarehouseNo }, ct);
        var dto = ToDto(sheet, warehouses.GetValueOrDefault(sheet.WarehouseNo));

        var lines = await _db.InvPhysicalCountDtls.AsNoTracking()
            .Where(d => d.CountNo == countNo)
            .OrderBy(d => d.LineNo)
            .ToListAsync(ct);

        dto.Lines = await ToLineDtosAsync(lines, ct);
        dto.LineCount = dto.Lines.Count;
        dto.VarianceLineCount = dto.Lines.Count(l => l.VarianceQty != 0);
        dto.VarianceValueTotal = dto.Lines.Sum(l => l.VarianceValue);

        return dto;
    }

    // ── open ─────────────────────────────────────────────────────────────────

    public async Task<Inv2006CountDto> StartAsync(Inv2006StartCountDto dto, CancellationToken ct = default)
    {
        long companyNo = Company(), branchNo = Branch();

        if (dto.WarehouseNo <= 0) throw new ValidationException("Warehouse is required");

        bool warehouseOk = await _db.InvWarehouses.AnyAsync(
            w => w.WarehouseNo == dto.WarehouseNo && w.CompanyNo == companyNo
                 && w.BranchNo == branchNo && w.IsDeleted == Deleted, ct);
        if (!warehouseOk) throw new ValidationException("Warehouse does not belong to your branch");

        // Two open sheets on one warehouse would each snapshot a different moment and post
        // conflicting variances.
        bool alreadyOpen = await _db.InvPhysicalCounts.AnyAsync(
            c => c.WarehouseNo == dto.WarehouseNo && c.IsDeleted == Deleted
                 && (c.Status == StDraft || c.Status == StCounting || c.Status == StReview), ct);
        if (alreadyOpen) throw new ValidationException("This warehouse already has an open count sheet");

        var sheet = new InvPhysicalCount
        {
            CompanyNo = companyNo,
            BranchNo = branchNo,
            CountId = await _docSeq.NextAsync(companyNo, branchNo, DocType, "CNT", 6, ct),
            CountDate = dto.CountDate != default ? dto.CountDate.Date : DateTime.UtcNow.Date,
            WarehouseNo = dto.WarehouseNo,
            CountType = dto.CountType is >= 1 and <= 3 ? dto.CountType : (short)1,
            FreezeStock = dto.FreezeStock,
            Status = StCounting,
            Remarks = dto.Remarks,
            IsActive = 1,
            IsDeleted = 0,
            CreatedBy = _ctx.CurrentUserNo(),
            CreatedAt = DateTime.UtcNow
        };

        _db.InvPhysicalCounts.Add(sheet);
        await _db.SaveChangesAsync(ct);

        await SnapshotAsync(sheet, dto.ProductNos, ct);
        await _db.SaveChangesAsync(ct);

        return await GetDetailAsync(sheet.CountNo, ct);
    }

    /// <summary>
    /// Copies the warehouse's live cells onto the sheet. Cells with zero on hand are included when
    /// counting the whole warehouse — finding stock that the system says is not there is exactly
    /// what a stocktake is for.
    /// </summary>
    private async Task SnapshotAsync(InvPhysicalCount sheet, List<long> productNos, CancellationToken ct)
    {
        var query = _db.InvStocks.AsNoTracking()
            .Where(s => s.WarehouseNo == sheet.WarehouseNo && s.CompanyNo == sheet.CompanyNo);

        if (productNos is { Count: > 0 })
            query = query.Where(s => productNos.Contains(s.ProductNo));

        var cells = await query
            .OrderBy(s => s.ProductNo).ThenBy(s => s.VariantNo).ThenBy(s => s.BatchNo)
            .Select(s => new { s.ProductNo, s.VariantNo, s.BatchNo, s.QtyOnHand, s.AvgCost })
            .ToListAsync(ct);

        int lineNo = 1;
        foreach (var cell in cells)
        {
            _db.InvPhysicalCountDtls.Add(new InvPhysicalCountDtl
            {
                CountNo = sheet.CountNo,
                LineNo = lineNo++,
                ProductNo = cell.ProductNo,
                VariantNo = cell.VariantNo,
                BatchNo = cell.BatchNo,
                SystemQty = cell.QtyOnHand,
                // Defaults to the system figure so an uncounted line posts no variance.
                CountedQty = cell.QtyOnHand,
                UnitCost = cell.AvgCost
            });
        }
    }

    // ── counting ─────────────────────────────────────────────────────────────

    public async Task<Inv2006CountDto> SaveCountsAsync(long countNo, List<Inv2006CountLineDto> lines,
                                                       CancellationToken ct = default)
    {
        var sheet = await RequireAsync(countNo, ct);
        if (sheet.Status is StPosted or StCancelled)
            throw new ValidationException("This count sheet is closed");

        var existing = await _db.InvPhysicalCountDtls
            .Where(d => d.CountNo == countNo)
            .ToListAsync(ct);

        var byId = existing.ToDictionary(d => d.CountDtlNo);

        foreach (var row in lines ?? new List<Inv2006CountLineDto>())
        {
            if (row.CountDtlNo is null || !byId.TryGetValue(row.CountDtlNo.Value, out var line)) continue;
            if (row.CountedQty < 0) throw new ValidationException("Counted quantity cannot be negative");

            // Only the count and its note are the counter's to change; the snapshot is not.
            line.CountedQty = row.CountedQty;
            line.Remarks = row.Remarks;
        }

        sheet.Status = StReview;
        sheet.UpdatedBy = _ctx.CurrentUserNo();
        sheet.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return await GetDetailAsync(countNo, ct);
    }

    // ── post ─────────────────────────────────────────────────────────────────

    public async Task<Inv2006CountDto> PostAsync(long countNo, CancellationToken ct = default)
    {
        var sheet = await RequireAsync(countNo, ct);
        if (sheet.Status == StPosted) throw new ValidationException("This count has already been posted");
        if (sheet.Status == StCancelled) throw new ValidationException("This count was cancelled");

        var lines = await _db.InvPhysicalCountDtls
            .Where(d => d.CountNo == countNo)
            .OrderBy(d => d.LineNo)
            .ToListAsync(ct);

        var variances = lines.Where(l => l.CountedQty != l.SystemQty).ToList();
        if (variances.Count == 0)
        {
            // A clean count is a real outcome; record it rather than forcing an empty adjustment.
            sheet.Status = StPosted;
            sheet.UpdatedBy = _ctx.CurrentUserNo();
            sheet.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return await GetDetailAsync(countNo, ct);
        }

        // Build the adjustment and let INV_1102 own the posting — one write path for stock.
        var adjustment = new Inv1102AdjustmentDto
        {
            AdjustmentDate = sheet.CountDate,
            WarehouseNo = sheet.WarehouseNo,
            AdjustmentType = AdjTypeCount,
            ReasonText = $"Physical count {sheet.CountId}",
            Remarks = sheet.Remarks,
            Lines = variances.Select(v => new Inv1102LineDto
            {
                ProductNo = v.ProductNo,
                VariantNo = v.VariantNo,
                UomNo = null,                        // base UOM; the snapshot is already in base qty
                Direction = v.CountedQty > v.SystemQty ? (short)1 : (short)-1,
                Qty = Math.Abs(v.CountedQty - v.SystemQty),
                UnitCost = v.UnitCost,
                Remarks = v.Remarks
            }).ToList()
        };

        var saved = await _adjustments.SaveAsync(adjustment, ct);
        await _adjustments.SubmitAsync(saved.AdjustmentNo!.Value, ct);
        var posted = await _adjustments.ApproveAsync(saved.AdjustmentNo!.Value, ct);

        sheet.VarianceAdjustmentNo = posted.AdjustmentNo;
        sheet.Status = StPosted;
        sheet.UpdatedBy = _ctx.CurrentUserNo();
        sheet.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return await GetDetailAsync(countNo, ct);
    }

    public async Task<Inv2006CountDto> CancelAsync(long countNo, CancellationToken ct = default)
    {
        var sheet = await RequireAsync(countNo, ct);
        if (sheet.Status == StPosted)
            throw new ValidationException("A posted count cannot be cancelled — reverse its adjustment instead");

        sheet.Status = StCancelled;
        sheet.UpdatedBy = _ctx.CurrentUserNo();
        sheet.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return await GetDetailAsync(countNo, ct);
    }

    public async Task DeleteAsync(long countNo, CancellationToken ct = default)
    {
        var sheet = await RequireAsync(countNo, ct);
        if (sheet.Status == StPosted) throw new ValidationException("A posted count cannot be deleted");

        var lines = await _db.InvPhysicalCountDtls.Where(d => d.CountNo == countNo).ToListAsync(ct);
        _db.InvPhysicalCountDtls.RemoveRange(lines);

        sheet.PerformSoftDelete(_ctx.CurrentUserNo());
        await _db.SaveChangesAsync(ct);
    }

    // ── mapping ──────────────────────────────────────────────────────────────

    private async Task<List<Inv2006CountLineDto>> ToLineDtosAsync(List<InvPhysicalCountDtl> lines,
                                                                  CancellationToken ct)
    {
        if (lines.Count == 0) return new List<Inv2006CountLineDto>();

        var productNos = lines.Select(l => l.ProductNo).Distinct().ToList();
        var products = await _db.InvProducts.AsNoTracking()
            .Where(p => productNos.Contains(p.ProductNo))
            .ToDictionaryAsync(p => p.ProductNo, p => p.ProductName, ct);

        var batchNos = lines.Where(l => l.BatchNo.HasValue).Select(l => l.BatchNo!.Value).Distinct().ToList();
        var batches = batchNos.Count == 0
            ? new Dictionary<long, string>()
            : await _db.InvBatches.AsNoTracking()
                .Where(b => batchNos.Contains(b.BatchNo))
                .ToDictionaryAsync(b => b.BatchNo, b => b.BatchCode, ct);

        return lines.Select(l => new Inv2006CountLineDto
        {
            CountDtlNo = l.CountDtlNo,
            LineNo = l.LineNo,
            ProductNo = l.ProductNo,
            ProductName = products.GetValueOrDefault(l.ProductNo),
            VariantNo = l.VariantNo,
            BatchNo = l.BatchNo,
            BatchCode = l.BatchNo.HasValue ? batches.GetValueOrDefault(l.BatchNo.Value) : null,
            SystemQty = l.SystemQty,
            CountedQty = l.CountedQty,
            VarianceQty = l.CountedQty - l.SystemQty,
            UnitCost = l.UnitCost,
            VarianceValue = Math.Round((l.CountedQty - l.SystemQty) * l.UnitCost, 4),
            Remarks = l.Remarks
        }).ToList();
    }

    private async Task<Dictionary<long, string>> WarehouseNamesAsync(IEnumerable<long> warehouseNos,
                                                                     CancellationToken ct)
    {
        var nos = warehouseNos.Distinct().ToList();
        if (nos.Count == 0) return new Dictionary<long, string>();

        return await _db.InvWarehouses.AsNoTracking()
            .Where(w => nos.Contains(w.WarehouseNo))
            .ToDictionaryAsync(w => w.WarehouseNo, w => w.WarehouseName, ct);
    }

    private static Inv2006CountDto ToDto(InvPhysicalCount c, string? warehouseName) => new()
    {
        CountNo = c.CountNo,
        CountId = c.CountId,
        CountDate = c.CountDate,
        WarehouseNo = c.WarehouseNo,
        WarehouseName = warehouseName,
        CountType = c.CountType,
        Status = c.Status,
        FreezeStock = c.FreezeStock,
        VarianceAdjustmentNo = c.VarianceAdjustmentNo,
        Remarks = c.Remarks,
        IsActive = c.IsActive,
        RowVersion = c.RowVersion
    };

    private async Task<InvPhysicalCount> RequireAsync(long countNo, CancellationToken ct)
    {
        var sheet = await _db.InvPhysicalCounts.FirstOrDefaultAsync(
                        c => c.CountNo == countNo && c.IsDeleted == Deleted, ct)
                    ?? throw new NotFoundException("Count sheet not found");

        if (sheet.CompanyNo != Company()) throw new ValidationException("Count belongs to another company");
        return sheet;
    }

    private long Company() =>
        _ctx.CurrentCompanyNo() ?? throw new ValidationException("No active company in context");

    private long Branch() =>
        _ctx.CurrentBranchNo() ?? throw new ValidationException("No active branch in context");
}
