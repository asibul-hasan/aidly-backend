namespace AidlyErp.Inv.Contracts;

/// <summary>Public read model of a product. Not the <c>InvProduct</c> entity — that stays inside INV.</summary>
public sealed record ProductInfo(
    long ProductNo,
    string? ProductId,
    string? ProductName,
    long? CompanyNo,
    /// <summary>Always null — products are company-scoped in this schema, not branch-scoped.</summary>
    long? BranchNo);

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

    /// <summary>Product names for a set of product numbers, in one query.</summary>
    Task<IReadOnlyDictionary<long, string>> GetProductNamesAsync(IReadOnlyCollection<long> productNos,
                                                                 CancellationToken cancellationToken = default);

    Task<WarehouseInfo?> FindWarehouseAsync(long warehouseNo, CancellationToken cancellationToken = default);

    /// <summary>Warehouse names for a set of warehouse numbers, in one query.</summary>
    Task<IReadOnlyDictionary<long, string>> GetWarehouseNamesAsync(IReadOnlyCollection<long> warehouseNos,
                                                                   CancellationToken cancellationToken = default);

    /// <summary>Warehouses filtered by company and/or branch; a <c>null</c> filter is not applied.</summary>
    Task<IReadOnlyList<WarehouseInfo>> ListWarehousesAsync(long? companyNo = null, long? branchNo = null,
                                                           CancellationToken cancellationToken = default);
}
