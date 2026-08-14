using AidlyErp.Inv.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Abstractions;
using AidlyErp.Shared.Contracts;
using AidlyErp.Sys.Contracts;
using AidlyErp.Inv.Domain;

namespace AidlyErp.Inv.Application.Engine;

using AidlyErp.Inv.Contracts;


public class InvStockPostingService : IInvStockPostingService
{
    private readonly IInvDbContext _db;

    public InvStockPostingService(IInvDbContext db)
    {
        _db = db;
    }

    public async Task<StockPostingResult> PostAsync(StockPostingCommand cmd, CancellationToken ct = default)
    {
        if (cmd.Legs == null || cmd.Legs.Count == 0)
            return new StockPostingResult(0m, new());

        decimal totalCost = 0m;
        var lineCosts = new Dictionary<long, decimal>();

        foreach (var leg in cmd.Legs)
        {
            var stock = await _db.InvStocks.FirstOrDefaultAsync(s => s.WarehouseNo == leg.WarehouseNo && s.ProductNo == leg.ItemNo, ct);
            if (stock == null)
            {
                stock = new InvStock
                {
                    CompanyNo = cmd.CompanyNo,
                    BranchNo = cmd.BranchNo,
                    WarehouseNo = leg.WarehouseNo,
                    ProductNo = leg.ItemNo,
                    QtyOnHand = 0m,
                    QtyReserved = 0m,
                    AvgCost = leg.UnitCost,
                    LastCost = leg.UnitCost,
                    CreatedAt = DateTime.UtcNow
                };
                _db.InvStocks.Add(stock);
                await _db.SaveChangesAsync(ct);
            }

            decimal lineCost = 0m;

            if (leg.Quantity > 0) // Stock IN
            {
                decimal prevTotalValue = stock.QtyOnHand * stock.AvgCost;
                decimal addedValue = leg.Quantity * leg.UnitCost;
                stock.QtyOnHand += leg.Quantity;
                if (stock.QtyOnHand > 0)
                {
                    stock.AvgCost = (prevTotalValue + addedValue) / stock.QtyOnHand;
                }
                stock.LastCost = leg.UnitCost;
                stock.LastMovementAt = DateTime.UtcNow;
                lineCost = addedValue;

                // Add Valuation Layer
                var layer = new InvValuationLayer
                {
                    CompanyNo = cmd.CompanyNo,
                    WarehouseNo = leg.WarehouseNo,
                    ProductNo = leg.ItemNo,
                    ReceiptLedgerNo = 0, // will be set after ledger is saved
                    ReceiptDate = cmd.TxnDate,
                    OriginalQty = leg.Quantity,
                    RemainingQty = leg.Quantity,
                    UnitCost = leg.UnitCost,
                    CreatedAt = DateTime.UtcNow
                };
                _db.InvValuationLayers.Add(layer);
            }
            else if (leg.Quantity < 0) // Stock OUT
            {
                decimal qtyToRelieve = Math.Abs(leg.Quantity);
                if (!cmd.AllowNegativeStock && stock.QtyOnHand < qtyToRelieve)
                {
                    throw new ValidationException($"Insufficient stock for item {leg.ItemNo} in warehouse {leg.WarehouseNo}. Available: {stock.QtyOnHand}, Requested: {qtyToRelieve}");
                }

                lineCost = qtyToRelieve * stock.AvgCost;
                stock.QtyOnHand -= qtyToRelieve;
                stock.LastMovementAt = DateTime.UtcNow;

                // Relieve FIFO Valuation Layers
                var layers = await _db.InvValuationLayers
                    .Where(l => l.WarehouseNo == leg.WarehouseNo && l.ProductNo == leg.ItemNo && l.RemainingQty > 0)
                    .OrderBy(l => l.ReceiptDate)
                    .ToListAsync(ct);

                decimal rem = qtyToRelieve;
                foreach (var layer in layers)
                {
                    if (rem <= 0) break;
                    decimal take = Math.Min(rem, layer.RemainingQty);
                    layer.RemainingQty -= take;
                    if (layer.RemainingQty <= 0) layer.IsExhausted = 1;
                    rem -= take;
                }
            }

            // Record Stock Ledger Movement Entry
            var ledger = new InvStockLedger
            {
                CompanyNo = cmd.CompanyNo,
                BranchNo = cmd.BranchNo,
                WarehouseNo = leg.WarehouseNo,
                ProductNo = leg.ItemNo,
                VariantNo = leg.VariantNo,
                BatchNo = leg.BatchNo,
                MovementDate = cmd.TxnDate,
                FinYearNo = cmd.FinYearNo ?? 0,
                FinPeriodNo = cmd.FinPeriodNo,
                MovementType = leg.MovementType,
                Direction = leg.Quantity > 0 ? (short)1 : (short)-1,
                QtyBase = Math.Abs(leg.Quantity),
                UnitCost = stock.AvgCost,
                TotalCost = lineCost,
                BalanceAfter = stock.QtyOnHand,
                AvgCostAfter = stock.AvgCost,
                RefDocType = cmd.RefDocType,
                RefDocNo = cmd.RefDocNo.ToString(),
                RefDocPk = long.TryParse(cmd.RefDocId, out var rp) ? rp : null,
                CreatedBy = 0,
                CreatedAt = DateTime.UtcNow
            };
            _db.InvStockLedgers.Add(ledger);

            totalCost += lineCost;
            if (leg.LineNo.HasValue)
            {
                lineCosts[leg.LineNo.Value] = lineCost;
            }
        }

        await _db.SaveChangesAsync(ct);
        return new StockPostingResult(totalCost, lineCosts);
    }
}
