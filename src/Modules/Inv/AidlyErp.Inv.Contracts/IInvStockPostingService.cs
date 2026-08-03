namespace AidlyErp.Inv.Contracts;


public enum CostingMethod
{
    WeightedAvg = 1,
    Fifo = 2,
    Lifo = 3,
    Standard = 4
}

public record StockPostingLeg(
    long WarehouseNo,
    long ItemNo,
    long? VariantNo,
    long? BatchNo,
    decimal Quantity,
    short MovementType, // 1=In 2=Out 3=Transfer 4=Adjustment
    long? LineNo,
    decimal UnitCost = 0m)
{
    public static StockPostingLeg In(long warehouseNo, long itemNo, long? variantNo, long? batchNo, decimal qty, short movementType, long? lineNo, decimal unitCost)
        => new(warehouseNo, itemNo, variantNo, batchNo, Math.Abs(qty), movementType, lineNo, unitCost);

    public static StockPostingLeg Out(long warehouseNo, long itemNo, long? variantNo, long? batchNo, decimal qty, short movementType, long? lineNo)
        => new(warehouseNo, itemNo, variantNo, batchNo, -Math.Abs(qty), movementType, lineNo);
}

public record StockPostingCommand(
    long CompanyNo,
    long BranchNo,
    short RefDocType,
    string RefDocId,
    long RefDocNo,
    DateTime TxnDate,
    long? FinYearNo,
    long? FinPeriodNo,
    bool AllowNegativeStock,
    List<StockPostingLeg> Legs);

public record StockPostingResult(
    decimal TotalCost,
    Dictionary<long, decimal> LineCosts);

public interface IInvStockPostingService
{
    Task<StockPostingResult> PostAsync(StockPostingCommand cmd, CancellationToken ct = default);
}
