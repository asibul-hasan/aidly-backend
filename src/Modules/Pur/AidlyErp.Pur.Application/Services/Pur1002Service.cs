using AidlyErp.Inv.Contracts;
using AidlyErp.Pur.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Abstractions;
using AidlyErp.Shared.Contracts;
using AidlyErp.Sys.Contracts;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Pur.Application.Dto;
using AidlyErp.Pur.Domain;

namespace AidlyErp.Pur.Application.Services;

/// <summary>
/// PUR_1002 Supplier Price List — port of Java Pur1002Service.
/// Manages supplier-product price rows with replace-save semantics.
/// </summary>
public interface IPur1002Service
{
    Task<List<Pur1002PriceRowDto>> GetRowsAsync(long supplierNo, CancellationToken ct = default);
    Task<List<Pur1002PriceRowDto>> SaveRowsAsync(long supplierNo, List<Pur1002PriceRowDto> rows, CancellationToken ct = default);
    Task DeleteRowAsync(long rowNo, CancellationToken ct = default);
}

public class Pur1002Service : IPur1002Service
{
    private readonly IPurDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    private readonly IInvCatalog _catalog;

    public Pur1002Service(IPurDbContext db, ICompanyBranchContext ctx, IInvCatalog catalog)
    {
        _db = db;
        _ctx = ctx;
        _catalog = catalog;
    }

    public async Task<List<Pur1002PriceRowDto>> GetRowsAsync(long supplierNo, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
        var supplier = await RequireSupplierAsync(supplierNo, companyNo, ct);

        var rows = await _db.PurSupplierProducts.AsNoTracking()
            .Where(p => p.SupplierNo == supplierNo && p.IsDeleted == 0)
            .OrderBy(p => p.SupProdNo)
            .ToListAsync(ct);

        var productNos = rows.Select(r => r.ProductNo).Distinct().ToList();
        var products = await _catalog.GetProductNamesAsync(productNos, ct);

        return rows.Select(p => new Pur1002PriceRowDto
        {
            SupplierProductNo = p.SupProdNo,
            SupplierNo = p.SupplierNo,
            SupplierName = supplier.SupplierName,
            ProductNo = p.ProductNo,
            ProductName = products.GetValueOrDefault(p.ProductNo),
            VariantNo = p.VariantNo,
            UomNo = p.UomNo,
            SupplierSku = p.SupplierSku,
            LastPrice = p.LastPrice,
            DiscountPct = p.DiscountPct,
            Moq = p.Moq,
            LeadTimeDays = p.LeadTimeDays,
            IsPreferred = p.IsPreferred,
            UnitPrice = p.LastPrice,
            Remarks = p.Remarks,
            IsActive = p.IsActive ?? 0,
            RowVersion = p.RowVersion
        }).ToList();
    }

    public async Task<List<Pur1002PriceRowDto>> SaveRowsAsync(long supplierNo, List<Pur1002PriceRowDto> rows, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
        await RequireSupplierAsync(supplierNo, companyNo, ct);

        if (rows == null) rows = new();
        AssertNoDuplicateRows(rows);

        // Load existing rows for this supplier.
        var existing = await _db.PurSupplierProducts
            .Where(p => p.SupplierNo == supplierNo && p.IsDeleted == 0)
            .ToListAsync(ct);

        // A supplier may quote the same product in several UOMs (piece and carton), so a row is
        // identified by product + variant + UOM — keying on product alone collapses those.
        var existingByKey = new Dictionary<(long, long, long), PurSupplierProduct>();
        foreach (var p in existing) existingByKey.TryAdd(RowKey(p.ProductNo, p.VariantNo, p.UomNo), p);

        var live = rows.Where(r => r.ProductNo > 0).ToList();
        var products = await _catalog.GetProductsAsync(live.Select(r => r.ProductNo).Distinct().ToList(),
                                                       companyNo, ct);

        var kept = new HashSet<long>();
        foreach (var dto in live)
        {
            if (!products.ContainsKey(dto.ProductNo))
                throw new NotFoundException($"Product not found: {dto.ProductNo}");

            PurSupplierProduct row;
            if (existingByKey.TryGetValue(RowKey(dto.ProductNo, dto.VariantNo, dto.UomNo), out var existingRow))
            {
                row = existingRow;
            }
            else
            {
                row = new PurSupplierProduct
                {
                    SupplierNo = supplierNo,
                    ProductNo = dto.ProductNo,
                    IsDeleted = 0,
                    CreatedBy = _ctx.CurrentUserNo(),
                    CreatedAt = DateTime.UtcNow
                };
                _db.PurSupplierProducts.Add(row);
            }

            row.VariantNo = dto.VariantNo;
            row.UomNo = dto.UomNo;
            row.SupplierSku = dto.SupplierSku;
            row.LastPrice = dto.LastPrice > 0 ? dto.LastPrice : dto.UnitPrice;
            row.DiscountPct = dto.DiscountPct;
            row.Moq = dto.Moq;
            row.LeadTimeDays = dto.LeadTimeDays;
            row.IsPreferred = dto.IsPreferred;
            row.Remarks = dto.Remarks;
            row.IsActive = dto.IsActive > 0 ? dto.IsActive : (short)1;
            row.UpdatedBy = _ctx.CurrentUserNo();
            row.UpdatedAt = DateTime.UtcNow;

            // New rows need their PK before they can be marked as kept.
            await _db.SaveChangesAsync(ct);
            kept.Add(row.SupProdNo);
        }

        // Soft-delete rows not in the new list.
        foreach (var row in existing)
        {
            if (!kept.Contains(row.SupProdNo))
            {
                row.PerformSoftDelete(_ctx.CurrentUserNo());
            }
        }

        await _db.SaveChangesAsync(ct);
        return await GetRowsAsync(supplierNo, ct);
    }

    public async Task DeleteRowAsync(long rowNo, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
        var row = await _db.PurSupplierProducts
            .FirstOrDefaultAsync(p => p.SupProdNo == rowNo && p.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Supplier price row not found: {rowNo}");

        // Verify the supplier belongs to the current company.
        var supplier = await _db.PurSuppliers.AsNoTracking()
            .FirstOrDefaultAsync(s => s.SupplierNo == row.SupplierNo && s.IsDeleted == 0, ct);
        if (supplier == null || supplier.CompanyNo != companyNo)
            throw new ValidationException("Price row belongs to another company");

        row.PerformSoftDelete(_ctx.CurrentUserNo());
        await _db.SaveChangesAsync(ct);
    }

    private async Task<PurSupplier> RequireSupplierAsync(long supplierNo, long companyNo, CancellationToken ct)
    {
        var supplier = await _db.PurSuppliers.AsNoTracking()
            .FirstOrDefaultAsync(s => s.SupplierNo == supplierNo && s.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Supplier not found: {supplierNo}");

        if (supplier.CompanyNo != companyNo)
            throw new ValidationException("Supplier belongs to another company");
        return supplier;
    }

    /// <summary>Identity of a price row: the same product may appear once per variant and UOM.</summary>
    private static (long, long, long) RowKey(long productNo, long? variantNo, long? uomNo) =>
        (productNo, variantNo ?? 0, uomNo ?? 0);

    private static void AssertNoDuplicateRows(List<Pur1002PriceRowDto> rows)
    {
        var seen = new HashSet<(long, long, long)>();
        foreach (var row in rows)
        {
            if (row.ProductNo <= 0 || row.UomNo <= 0) continue;
            if (!seen.Add(RowKey(row.ProductNo, row.VariantNo, row.UomNo)))
                throw new ValidationException("Duplicate product/UOM row in supplier price list");
        }
    }
}
