using AidlyErp.Inv.Contracts;
using AidlyErp.Shared.Core.Exceptions;

namespace AidlyErp.Pur.Application.Services;

/// <summary>
/// Converts a document line quantity into the product's base UOM, which is the only quantity the
/// stock engine accepts. Buying a carton of 12 must relieve 12 pieces, not 1 — before this existed
/// the ported services wrote the raw quantity straight into <c>qty_base</c>.
/// </summary>
/// <remarks>
/// Built once per document from a single batched factor query, then applied per line in memory, so
/// a 40-line invoice still costs one round trip rather than forty.
/// </remarks>
internal sealed class PurUomConverter
{
    private readonly IReadOnlyDictionary<(long ProductNo, long UomNo), decimal> _factors;

    private PurUomConverter(IReadOnlyDictionary<(long, long), decimal> factors) => _factors = factors;

    /// <summary>Loads the conversion factors for every product the document touches.</summary>
    public static async Task<PurUomConverter> LoadAsync(IInvCatalog catalog, IEnumerable<long> productNos,
                                                        CancellationToken ct = default)
    {
        var distinct = productNos.Where(no => no > 0).Distinct().ToList();
        return new PurUomConverter(await catalog.GetUomFactorsAsync(distinct, ct));
    }

    /// <summary>
    /// Base quantity for <paramref name="qty"/> of <paramref name="product"/> expressed in
    /// <paramref name="uomNo"/>. Throws when the UOM is missing or has no conversion defined —
    /// silently assuming a factor of 1 would corrupt stock.
    /// </summary>
    public decimal ToBaseQty(ProductInfo product, long? uomNo, decimal qty)
    {
        if (uomNo is null || uomNo <= 0) throw new ValidationException("UOM is required");
        if (uomNo == product.BaseUomNo) return qty;

        if (!_factors.TryGetValue((product.ProductNo, uomNo.Value), out decimal factor))
            throw new ValidationException($"No UOM conversion defined for product {product.ProductId}");

        return qty * factor;
    }
}
