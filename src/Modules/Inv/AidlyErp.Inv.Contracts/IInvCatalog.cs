namespace AidlyErp.Inv.Contracts;

/// <summary>Public read model of a product. Not the <c>InvProduct</c> entity — that stays inside INV.</summary>
public sealed record ProductInfo(
    long ProductNo,
    string? ProductId,
    string? ProductName,
    long? CompanyNo,
    /// <summary>Always null — products are company-scoped in this schema, not branch-scoped.</summary>
    long? BranchNo,
    /// <summary>The UOM stock is held in. A line priced in another UOM must be converted to this one.</summary>
    long BaseUomNo,
    /// <summary>1 when every movement of this product must name a batch.</summary>
    short IsBatchTracked,
    /// <summary>Classification, so a consuming module can group spend or sales by category/brand.</summary>
    long? CategoryNo = null,
    long? BrandNo = null);

/// <summary>
/// What a selling module needs to price a line: the list price, the floor below which a discount
/// needs approval, the printed price, and the tax treatment.
/// </summary>
public sealed record ProductPricingInfo(
    long ProductNo,
    string? ProductId,
    string? ProductName,
    decimal SalePrice,
    decimal? MinSalePrice,
    decimal Mrp,
    long? VatTaxNo,
    /// <summary>1 when <see cref="SalePrice"/> already contains the tax.</summary>
    short IsTaxInclusive,
    long BaseUomNo,
    short AllowDiscount,
    /// <summary>Carried so a selling module can scope a promotion by category or brand
    /// without a second query per cart.</summary>
    long? CategoryNo,
    long? BrandNo);

/// <summary>Public read model of a warehouse.</summary>
public sealed record WarehouseInfo(
    long WarehouseNo,
    string? WarehouseId,
    string? WarehouseName,
    long? CompanyNo,
    long? BranchNo);

/// <summary>
/// Read access to the inventory catalogue for modules that reference products and warehouses but
/// do not own them — Purchasing validating an order line, SYS filling a dropdown.
/// </summary>
public interface IInvCatalog
{
    /// <summary>
    /// A single product. When <paramref name="companyNo"/> is supplied the product must belong to
    /// that company, matching the tenant check the callers did inline.
    /// </summary>
    Task<ProductInfo?> FindProductAsync(long productNo, long? companyNo = null,
                                        CancellationToken cancellationToken = default);

    /// <summary>
    /// Products for a set of product numbers, in one query, keyed by product number. Document
    /// services resolve every line's product through this instead of calling
    /// <see cref="FindProductAsync"/> inside a line loop.
    /// </summary>
    Task<IReadOnlyDictionary<long, ProductInfo>> GetProductsAsync(IReadOnlyCollection<long> productNos,
                                                                  long? companyNo = null,
                                                                  CancellationToken cancellationToken = default);

    /// <summary>
    /// Pricing for a set of products, in one query. The selling module reads list price and the
    /// min-price floor from here rather than trusting whatever the till sent.
    /// </summary>
    Task<IReadOnlyDictionary<long, ProductPricingInfo>> GetPricingAsync(IReadOnlyCollection<long> productNos,
                                                                        long? companyNo = null,
                                                                        CancellationToken cancellationToken = default);

    /// <summary>Product names for a set of product numbers, in one query.</summary>
    Task<IReadOnlyDictionary<long, string>> GetProductNamesAsync(IReadOnlyCollection<long> productNos,
                                                                 CancellationToken cancellationToken = default);

    /// <summary>UOM names for a set of UOM numbers, in one query. Document lines store the UOM
    /// number; every form that lists lines has to show its name.</summary>
    Task<IReadOnlyDictionary<long, string>> GetUomNamesAsync(IReadOnlyCollection<long> uomNos,
                                                             CancellationToken cancellationToken = default);

    Task<WarehouseInfo?> FindWarehouseAsync(long warehouseNo, CancellationToken cancellationToken = default);

    /// <summary>Warehouse names for a set of warehouse numbers, in one query.</summary>
    Task<IReadOnlyDictionary<long, string>> GetWarehouseNamesAsync(IReadOnlyCollection<long> warehouseNos,
                                                                   CancellationToken cancellationToken = default);

    /// <summary>Warehouses filtered by company and/or branch; a <c>null</c> filter is not applied.</summary>
    Task<IReadOnlyList<WarehouseInfo>> ListWarehousesAsync(long? companyNo = null, long? branchNo = null,
                                                           CancellationToken cancellationToken = default);

    /// <summary>
    /// Base-UOM conversion factors for a set of products, keyed by <c>(productNo, fromUomNo)</c>.
    /// A line quantity in <c>fromUomNo</c> is multiplied by the factor to reach base quantity.
    /// Fetched for the whole document at once so line loops stay free of per-row queries.
    /// </summary>
    Task<IReadOnlyDictionary<(long ProductNo, long UomNo), decimal>> GetUomFactorsAsync(
        IReadOnlyCollection<long> productNos, CancellationToken cancellationToken = default);
}
