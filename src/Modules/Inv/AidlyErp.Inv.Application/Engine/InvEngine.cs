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
            var stock = await _db.InvStocks.FirstOrDefaultAsync(s => s.WarehouseNo == leg.WarehouseNo && s.ProductNo == leg.ItemNo && s.IsDeleted == 0, ct);
            if (stock == null)
            {
                stock = new InvStock
                {
                    WarehouseNo = leg.WarehouseNo,
                    ItemNo = leg.ItemNo,
                    QtyOnHand = 0m,
                    QtyAllocated = 0m,
                    QtyOnOrder = 0m,
                    AvgUnitCost = leg.UnitCost,
                    IsActive = 1, IsDeleted = 0,
                    CreatedAt = DateTime.UtcNow
                };
                _db.InvStocks.Add(stock);
                await _db.SaveChangesAsync(ct);
            }

            decimal lineCost = 0m;

            if (leg.Quantity > 0) // Stock IN
            {
                decimal prevTotalValue = stock.QtyOnHand * stock.AvgUnitCost;
                decimal addedValue = leg.Quantity * leg.UnitCost;
                stock.QtyOnHand += leg.Quantity;
                if (stock.QtyOnHand > 0)
                {
                    stock.AvgUnitCost = (prevTotalValue + addedValue) / stock.QtyOnHand;
                }
                lineCost = addedValue;

                // Add Valuation Layer
                var layer = new InvValuationLayer
                {
                    CompanyNo = cmd.CompanyNo,
                    BranchNo = cmd.BranchNo,
                    WarehouseNo = leg.WarehouseNo,
                    ItemNo = leg.ItemNo,
                    DocType = cmd.RefDocType.ToString(),
                    DocNo = cmd.RefDocNo,
                    ReceiptDate = cmd.TxnDate,
                    QtyReceived = leg.Quantity,
                    QtyRemaining = leg.Quantity,
                    UnitCost = leg.UnitCost,
                    TotalCost = addedValue,
                    IsActive = 1, IsDeleted = 0,
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

                lineCost = qtyToRelieve * stock.AvgUnitCost;
                stock.QtyOnHand -= qtyToRelieve;

                // Relieve FIFO Valuation Layers
                var layers = await _db.InvValuationLayers
                    .Where(l => l.WarehouseNo == leg.WarehouseNo && l.ProductNo == leg.ItemNo && l.QtyRemaining > 0 && l.IsDeleted == 0)
                    .OrderBy(l => l.ReceiptDate)
                    .ToListAsync(ct);

                decimal rem = qtyToRelieve;
                foreach (var layer in layers)
                {
                    if (rem <= 0) break;
                    decimal take = Math.Min(rem, layer.QtyRemaining);
                    layer.QtyRemaining -= take;
                    rem -= take;
                }
            }

            // Record Stock Ledger Movement Entry
            var ledger = new InvStockLedger
            {
                CompanyNo = cmd.CompanyNo,
                BranchNo = cmd.BranchNo,
                WarehouseNo = leg.WarehouseNo,
                ItemNo = leg.ItemNo,
                VariantNo = leg.VariantNo,
                BatchNo = leg.BatchNo,
                TxnDate = cmd.TxnDate,
                RefDocType = cmd.RefDocType.ToString(),
                RefDocNo = cmd.RefDocNo,
                RefDocId = cmd.RefDocId,
                MovementType = leg.MovementType.ToString(),
                Qty = leg.Quantity,
                UnitCost = stock.AvgUnitCost,
                TotalCost = lineCost,
                BalanceQty = stock.QtyOnHand,
                BalanceAvgCost = stock.AvgUnitCost,
                FinYearNo = cmd.FinYearNo,
                FinPeriodNo = cmd.FinPeriodNo,
                IsActive = 1, IsDeleted = 0,
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
