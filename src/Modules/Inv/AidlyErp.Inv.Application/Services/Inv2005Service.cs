using AidlyErp.Inv.Application.Dto;
using AidlyErp.Inv.Application.Interfaces;
using AidlyErp.Inv.Domain;
using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Security;
using Microsoft.EntityFrameworkCore;

namespace AidlyErp.Inv.Application.Services;

public interface IInv2005Service
{
    Task<List<Inv2005ReorderDto>> GetListAsync(long warehouseNo, CancellationToken ct = default);
    Task<List<Inv2005ReorderDto>> SaveBulkAsync(long warehouseNo, List<Inv2005ReorderDto> rows,
                                                CancellationToken ct = default);
    Task DeleteAsync(long reorderNo, CancellationToken ct = default);
}

/// <summary>
/// INV_2005 Reorder Level — the per-warehouse replenishment policy, edited as a grid, so the form
/// saves the whole warehouse's rows in one call rather than row by row.
/// </summary>
public class Inv2005Service : IInv2005Service
{
    private const short Deleted = 0;

    private readonly IInvDbContext _db;
    private readonly ICompanyBranchContext _ctx;

    public Inv2005Service(IInvDbContext db, ICompanyBranchContext ctx)
    {
        _db = db;
        _ctx = ctx;
    }

    public async Task<List<Inv2005ReorderDto>> GetListAsync(long warehouseNo, CancellationToken ct = default)
    {
        long companyNo = Company();
        await RequireWarehouseAsync(warehouseNo, ct);

        var rows = await _db.InvReorders.AsNoTracking()
            .Where(r => r.CompanyNo == companyNo && r.WarehouseNo == warehouseNo && r.IsDeleted == Deleted)
            .OrderBy(r => r.ReorderNo)
            .ToListAsync(ct);

        var products = await ProductNamesAsync(rows.Select(r => r.ProductNo), ct);
        return rows.Select(r => ToDto(r, products.GetValueOrDefault(r.ProductNo))).ToList();
    }

    public async Task<List<Inv2005ReorderDto>> SaveBulkAsync(long warehouseNo, List<Inv2005ReorderDto> rows,
                                                             CancellationToken ct = default)
    {
        long companyNo = Company();
        await RequireWarehouseAsync(warehouseNo, ct);

        rows ??= new List<Inv2005ReorderDto>();

        var existing = await _db.InvReorders
            .Where(r => r.CompanyNo == companyNo && r.WarehouseNo == warehouseNo && r.IsDeleted == Deleted)
            .ToListAsync(ct);

        // A policy is identified by product + variant, so the same product can be tracked per variant.
        var existingByKey = new Dictionary<(long, long), InvReorder>();
        foreach (var r in existing) existingByKey.TryAdd((r.ProductNo, r.VariantNo ?? 0), r);

        var kept = new HashSet<(long, long)>();
        foreach (var dto in rows)
        {
            if (dto.ProductNo <= 0) continue;

            var key = (dto.ProductNo, dto.VariantNo ?? 0);
            if (!kept.Add(key))
                throw new ValidationException("Duplicate product/variant row in the reorder list");

            if (!existingByKey.TryGetValue(key, out var e))
            {
                e = new InvReorder
                {
                    CompanyNo = companyNo,
                    WarehouseNo = warehouseNo,
                    ProductNo = dto.ProductNo,
                    VariantNo = dto.VariantNo,
                    CreatedBy = _ctx.CurrentUserNo(),
                    CreatedAt = DateTime.UtcNow,
                    IsDeleted = 0
                };
                _db.InvReorders.Add(e);
            }

            e.ReorderLevel = dto.ReorderLevel ?? 0m;
            e.ReorderQty = dto.ReorderQty ?? 0m;
            e.MinStock = dto.MinStock ?? 0m;
            e.MaxStock = dto.MaxStock;
            e.PreferredSupplierNo = dto.PreferredSupplierNo;
            e.LeadTimeDays = dto.LeadTimeDays;
            e.IsActive = dto.IsActive ?? 1;
            e.UpdatedBy = _ctx.CurrentUserNo();
            e.UpdatedAt = DateTime.UtcNow;
        }

        // Rows the grid dropped are policies the user removed.
        foreach (var r in existing)
        {
            if (!kept.Contains((r.ProductNo, r.VariantNo ?? 0)))
                r.PerformSoftDelete(_ctx.CurrentUserNo());
        }

        await _db.SaveChangesAsync(ct);
        return await GetListAsync(warehouseNo, ct);
    }

    public async Task DeleteAsync(long reorderNo, CancellationToken ct = default)
    {
        long companyNo = Company();

        var e = await _db.InvReorders.FirstOrDefaultAsync(
                    r => r.ReorderNo == reorderNo && r.IsDeleted == Deleted, ct)
                ?? throw new NotFoundException($"Reorder policy not found: {reorderNo}");

        if (e.CompanyNo != companyNo) throw new ValidationException("Reorder policy belongs to another company");

        e.PerformSoftDelete(_ctx.CurrentUserNo());
        await _db.SaveChangesAsync(ct);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private async Task RequireWarehouseAsync(long warehouseNo, CancellationToken ct)
    {
        if (warehouseNo <= 0) throw new ValidationException("Warehouse is required");

        long companyNo = Company();
        bool exists = await _db.InvWarehouses.AnyAsync(
            w => w.WarehouseNo == warehouseNo && w.CompanyNo == companyNo && w.IsDeleted == Deleted, ct);

        if (!exists) throw new NotFoundException("Warehouse not found");
    }

    private async Task<Dictionary<long, string>> ProductNamesAsync(IEnumerable<long> productNos, CancellationToken ct)
    {
        var nos = productNos.Distinct().ToList();
        if (nos.Count == 0) return new Dictionary<long, string>();

        return await _db.InvProducts.AsNoTracking()
            .Where(p => nos.Contains(p.ProductNo))
            .ToDictionaryAsync(p => p.ProductNo, p => p.ProductName, ct);
    }

    private static Inv2005ReorderDto ToDto(InvReorder r, string? productName) => new()
    {
        ReorderNo = r.ReorderNo,
        WarehouseNo = r.WarehouseNo,
        ProductNo = r.ProductNo,
        ProductName = productName,
        VariantNo = r.VariantNo,
        ReorderLevel = r.ReorderLevel,
        ReorderQty = r.ReorderQty,
        MinStock = r.MinStock,
        MaxStock = r.MaxStock,
        PreferredSupplierNo = r.PreferredSupplierNo,
        LeadTimeDays = r.LeadTimeDays,
        IsActive = r.IsActive,
        RowVersion = r.RowVersion
    };

    private long Company() =>
        _ctx.CurrentCompanyNo() ?? throw new ValidationException("No active company in context");
}
