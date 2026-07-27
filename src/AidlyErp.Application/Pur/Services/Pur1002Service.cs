using Microsoft.EntityFrameworkCore;
using AidlyErp.Application.Common.Exceptions;
using AidlyErp.Application.Common.Interfaces;
using AidlyErp.Application.Common.Security;
using AidlyErp.Application.Pur.Dto;
using AidlyErp.Domain.Pur;

namespace AidlyErp.Application.Pur.Services;

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
    private readonly IApplicationDbContext _db;
    private readonly ICompanyBranchContext _ctx;

    public Pur1002Service(IApplicationDbContext db, ICompanyBranchContext ctx)
    {
        _db = db;
        _ctx = ctx;
    }

    public async Task<List<Pur1002PriceRowDto>> GetRowsAsync(long supplierNo, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
        await RequireSupplierAsync(supplierNo, companyNo, ct);

        return await _db.PurSupplierProducts.AsNoTracking()
            .Where(p => p.SupplierNo == supplierNo && p.IsDeleted == 0)
            .OrderBy(p => p.SupProdNo)
            .Select(p => new Pur1002PriceRowDto
            {
                SupplierNo = p.SupplierNo,
                ProductNo = p.ProductNo,
                UnitPrice = p.LastPurchasePrice ?? 0m
            })
            .ToListAsync(ct);
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

        // Build a lookup by ProductNo for update matching.
        var existingByProduct = existing.ToDictionary(p => p.ProductNo);

        var kept = new HashSet<long>();
        foreach (var dto in rows)
        {
            if (dto.ProductNo <= 0) continue;

            // Validate product exists.
            var product = await _db.InvProducts.AsNoTracking()
                .FirstOrDefaultAsync(p => p.ProductNo == dto.ProductNo && p.CompanyNo == companyNo && p.IsDeleted == 0, ct)
                ?? throw new NotFoundException($"Product not found: {dto.ProductNo}");

            PurSupplierProduct row;
            if (existingByProduct.TryGetValue(dto.ProductNo, out var existingRow))
            {
                // Update existing row.
                row = existingRow;
            }
            else
            {
                // Create new row.
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

            row.LastPurchasePrice = dto.UnitPrice;
            row.UpdatedBy = _ctx.CurrentUserNo();
            row.UpdatedAt = DateTime.UtcNow;

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

    private async Task RequireSupplierAsync(long supplierNo, long companyNo, CancellationToken ct)
    {
        var supplier = await _db.PurSuppliers.AsNoTracking()
            .FirstOrDefaultAsync(s => s.SupplierNo == supplierNo && s.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Supplier not found: {supplierNo}");

        if (supplier.CompanyNo != companyNo)
            throw new ValidationException("Supplier belongs to another company");
    }

    private static void AssertNoDuplicateRows(List<Pur1002PriceRowDto> rows)
    {
        var seen = new HashSet<long>();
        foreach (var row in rows)
        {
            if (row.ProductNo <= 0) continue;
            if (!seen.Add(row.ProductNo))
                throw new ValidationException($"Duplicate product in supplier price list: productNo={row.ProductNo}");
        }
    }
}
