using AidlyErp.Inv.Application.Dto;
using AidlyErp.Inv.Application.Interfaces;
using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Security;
using Microsoft.EntityFrameworkCore;

namespace AidlyErp.Inv.Application.Services;

public interface IInv2004Service
{
    Task<List<Inv2004BatchDto>> GetBatchesAsync(long? warehouseNo, long? productNo, string? status,
                                                int? nearDays, CancellationToken ct = default);
}

/// <summary>
/// INV_2004 Batch &amp; Expiry — a read-only dashboard over the live stock cells that carry a batch.
/// Rows come from <c>inv_stock</c> rather than <c>inv_batch</c> so a batch with no stock left stops
/// showing, and each row is one batch in one warehouse.
/// </summary>
public class Inv2004Service : IInv2004Service
{
    private const short Deleted = 0;
    private const int DefaultNearDays = 30;

    private readonly IInvDbContext _db;
    private readonly ICompanyBranchContext _ctx;

    public Inv2004Service(IInvDbContext db, ICompanyBranchContext ctx)
    {
        _db = db;
        _ctx = ctx;
    }

    public async Task<List<Inv2004BatchDto>> GetBatchesAsync(long? warehouseNo, long? productNo, string? status,
                                                             int? nearDays, CancellationToken ct = default)
    {
        long companyNo = Company(), branchNo = Branch();
        int days = nearDays is null or <= 0 ? DefaultNearDays : nearDays.Value;

        var cells = await _db.InvStocks.AsNoTracking()
            .Where(s => s.CompanyNo == companyNo && s.BranchNo == branchNo && s.BatchNo != null
                        && (warehouseNo == null || s.WarehouseNo == warehouseNo)
                        && (productNo == null || s.ProductNo == productNo))
            .ToListAsync(ct);

        if (cells.Count == 0) return new List<Inv2004BatchDto>();

        var batchNos = cells.Select(s => s.BatchNo!.Value).Distinct().ToList();
        var batches = await _db.InvBatches.AsNoTracking()
            .Where(b => batchNos.Contains(b.BatchNo) && b.IsDeleted == Deleted)
            .ToDictionaryAsync(b => b.BatchNo, ct);

        var productNos = cells.Select(s => s.ProductNo).Distinct().ToList();
        var products = await _db.InvProducts.AsNoTracking()
            .Where(p => productNos.Contains(p.ProductNo))
            .ToDictionaryAsync(p => p.ProductNo, p => p.ProductName, ct);

        var warehouseNos = cells.Select(s => s.WarehouseNo).Distinct().ToList();
        var warehouses = await _db.InvWarehouses.AsNoTracking()
            .Where(w => warehouseNos.Contains(w.WarehouseNo))
            .ToDictionaryAsync(w => w.WarehouseNo, w => w.WarehouseName, ct);

        var today = DateTime.UtcNow.Date;
        var rows = new List<Inv2004BatchDto>();

        foreach (var s in cells)
        {
            // A cell whose batch was deleted has nothing meaningful to show.
            if (!batches.TryGetValue(s.BatchNo!.Value, out var batch)) continue;

            var row = new Inv2004BatchDto
            {
                BatchNo = batch.BatchNo,
                BatchCode = batch.BatchCode,
                ProductNo = s.ProductNo,
                ProductName = products.GetValueOrDefault(s.ProductNo),
                VariantNo = s.VariantNo,
                MfgDate = batch.MfgDate,
                ExpiryDate = batch.ExpiryDate,
                ReceivedCost = batch.ReceivedCost,
                WarehouseNo = s.WarehouseNo,
                WarehouseName = warehouses.GetValueOrDefault(s.WarehouseNo),
                QtyOnHand = s.QtyOnHand,
                QtyAvailable = s.QtyAvailable ?? 0m,
                StockValue = s.StockValue ?? 0m
            };

            if (batch.ExpiryDate is null)
            {
                row.ExpiryStatus = "no_expiry";
            }
            else
            {
                int toExpiry = (int)(batch.ExpiryDate.Value.Date - today).TotalDays;
                row.DaysToExpiry = toExpiry;
                row.ExpiryStatus = toExpiry < 0 ? "expired" : toExpiry <= days ? "near_expiry" : "ok";
            }

            rows.Add(row);
        }

        if (!string.IsNullOrWhiteSpace(status))
            rows = rows.Where(r => string.Equals(r.ExpiryStatus, status, StringComparison.OrdinalIgnoreCase)).ToList();

        // Soonest to expire first — that is the order the dashboard is read in.
        return rows
            .OrderBy(r => r.ExpiryDate ?? DateTime.MaxValue)
            .ThenBy(r => r.ProductName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private long Company() =>
        _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");

    private long Branch() =>
        _ctx.CurrentBranchNo() ?? throw new ValidationException("Branch context is required");
}
