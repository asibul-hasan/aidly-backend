using AidlyErp.Inv.Contracts;
using AidlyErp.Shared.Core.Exceptions;

namespace AidlyErp.Inv.Application.Services;

/// <summary>
/// Converts a document line quantity into the product's base UOM — the only quantity the stock
/// engine accepts. Factors are loaded once per document and applied in memory, so a long line grid
/// costs one query rather than one per row.
/// </summary>
internal static class InvUomMath
{
    /// <summary>
    /// Throws rather than assuming a factor of 1: silently treating a carton as a single piece
    /// would corrupt stock in a way nothing downstream can detect.
    /// </summary>
    public static decimal ToBaseQty(ProductInfo product, long? uomNo, decimal qty,
                                    IReadOnlyDictionary<(long ProductNo, long UomNo), decimal> factors)
    {
        if (uomNo is null || uomNo <= 0) throw new ValidationException("UOM is required");
        if (uomNo == product.BaseUomNo) return qty;

        if (!factors.TryGetValue((product.ProductNo, uomNo.Value), out decimal factor))
            throw new ValidationException($"No UOM conversion defined for product {product.ProductId}");

        return qty * factor;
    }
}
