using AidlyErp.Inv.Application.Interfaces;
using AidlyErp.Inv.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AidlyErp.Inv.Infrastructure;

/// <summary>INV's side of <see cref="IInvCatalog"/>.</summary>
internal sealed class InvCatalog : IInvCatalog
{
    private const short Deleted = 0;

    private readonly IInvDbContext _db;

    public InvCatalog(IInvDbContext db) => _db = db;

    public async Task<ProductInfo?> FindProductAsync(long productNo, long? companyNo = null,
                                                     CancellationToken cancellationToken = default) =>
        await _db.InvProducts
            .AsNoTracking()
            .Where(p => p.ProductNo == productNo && p.IsDeleted == Deleted
                        && (companyNo == null || p.CompanyNo == companyNo))
            .Select(p => new ProductInfo(p.ProductNo, p.ProductId, p.ProductName, p.CompanyNo, null))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<long, string>> GetProductNamesAsync(
        IReadOnlyCollection<long> productNos, CancellationToken cancellationToken = default)
    {
        if (productNos.Count == 0) return new Dictionary<long, string>();

        return await _db.InvProducts
            .AsNoTracking()
            .Where(p => productNos.Contains(p.ProductNo) && p.IsDeleted == Deleted)
            .ToDictionaryAsync(p => p.ProductNo, p => p.ProductName, cancellationToken);
    }

    public async Task<WarehouseInfo?> FindWarehouseAsync(long warehouseNo,
                                                         CancellationToken cancellationToken = default) =>
        await _db.InvWarehouses
            .AsNoTracking()
            .Where(w => w.WarehouseNo == warehouseNo && w.IsDeleted == Deleted)
            .Select(w => new WarehouseInfo(w.WarehouseNo, w.WarehouseId, w.WarehouseName, w.CompanyNo, w.BranchNo))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<long, string>> GetWarehouseNamesAsync(
        IReadOnlyCollection<long> warehouseNos, CancellationToken cancellationToken = default)
    {
        if (warehouseNos.Count == 0) return new Dictionary<long, string>();

        return await _db.InvWarehouses
            .AsNoTracking()
            .Where(w => warehouseNos.Contains(w.WarehouseNo) && w.IsDeleted == Deleted)
            .ToDictionaryAsync(w => w.WarehouseNo, w => w.WarehouseName, cancellationToken);
    }

    public async Task<IReadOnlyList<WarehouseInfo>> ListWarehousesAsync(
        long? companyNo = null, long? branchNo = null, CancellationToken cancellationToken = default) =>
        await _db.InvWarehouses
            .AsNoTracking()
            .Where(w => w.IsDeleted == Deleted
                        && (companyNo == null || w.CompanyNo == companyNo)
                        && (branchNo == null || w.BranchNo == branchNo))
            .OrderBy(w => w.WarehouseNo)
            .Select(w => new WarehouseInfo(w.WarehouseNo, w.WarehouseId, w.WarehouseName, w.CompanyNo, w.BranchNo))
            .ToListAsync(cancellationToken);
}
