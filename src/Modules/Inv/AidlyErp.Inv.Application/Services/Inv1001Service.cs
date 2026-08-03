using AidlyErp.Inv.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Abstractions;
using AidlyErp.Shared.Contracts;
using AidlyErp.Sys.Contracts;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Inv.Application.Dto;
using AidlyErp.Inv.Domain;

namespace AidlyErp.Inv.Application.Services;

public interface IInv1001Service
{
    Task<List<Inv1001ProductDto>> GetListAsync(CancellationToken ct = default);
    Task<Inv1001ProductDto> GetDetailAsync(long productNo, CancellationToken ct = default);
    Task<Inv1001ProductDto> SaveAsync(Inv1001ProductDto dto, CancellationToken ct = default);
    Task DeleteAsync(long productNo, CancellationToken ct = default);
    Task<Inv1001BarcodeResolveDto?> ResolveBarcodeAsync(string barcode, CancellationToken ct = default);
}

public class Inv1001Service : IInv1001Service
{
    private readonly IInvDbContext _db;
    private readonly ICompanyBranchContext _ctx;

    public Inv1001Service(IInvDbContext db, ICompanyBranchContext ctx)
    {
        _db = db;
        _ctx = ctx;
    }

    public async Task<List<Inv1001ProductDto>> GetListAsync(CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? 0;
        var cats = await _db.InvCategories.AsNoTracking().Where(c => c.IsDeleted == 0).ToDictionaryAsync(c => c.CategoryNo, c => c.CategoryName, ct);
        var uoms = await _db.InvUoms.AsNoTracking().Where(u => u.IsDeleted == 0).ToDictionaryAsync(u => u.UomNo, u => u.UomName, ct);
        var brands = await _db.InvBrands.AsNoTracking().Where(b => b.IsDeleted == 0).ToDictionaryAsync(b => b.BrandNo, b => b.BrandName, ct);

        var list = await _db.InvProducts
            .AsNoTracking()
            .Where(p => p.CompanyNo == companyNo && p.IsDeleted == 0)
            .OrderBy(p => p.ProductName)
            .ToListAsync(ct);

        return list.Select(p => ToDto(p, cats.GetValueOrDefault(p.CategoryNo), uoms.GetValueOrDefault(p.UomNo), p.BrandNo.HasValue ? brands.GetValueOrDefault(p.BrandNo.Value) : null)).ToList();
    }

    public async Task<Inv1001ProductDto> GetDetailAsync(long productNo, CancellationToken ct = default)
    {
        var p = await _db.InvProducts.AsNoTracking().FirstOrDefaultAsync(x => x.ProductNo == productNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Product not found: {productNo}");

        var catName = await _db.InvCategories.Where(c => c.CategoryNo == p.CategoryNo).Select(c => c.CategoryName).FirstOrDefaultAsync(ct);
        var uomName = await _db.InvUoms.Where(u => u.UomNo == p.UomNo).Select(u => u.UomName).FirstOrDefaultAsync(ct);
        var brandName = p.BrandNo.HasValue ? await _db.InvBrands.Where(b => b.BrandNo == p.BrandNo.Value).Select(b => b.BrandName).FirstOrDefaultAsync(ct) : null;

        var dto = ToDto(p, catName, uomName, brandName);

        dto.Variants = await _db.InvProductVariants
            .AsNoTracking()
            .Where(v => v.ProductNo == productNo && v.IsDeleted == 0)
            .Select(v => new Inv1001VariantDto
            {
                VariantNo = v.VariantNo,
                ProductNo = v.ProductNo,
                VariantCode = v.VariantCode,
                VariantName = v.VariantName,
                Sku = v.Sku,
                CostPrice = v.CostPrice,
                SellingPrice = v.SellingPrice
            })
            .ToListAsync(ct);

        dto.Barcodes = await _db.InvProductBarcodes
            .AsNoTracking()
            .Where(b => b.ProductNo == productNo && b.IsDeleted == 0)
            .Select(b => new Inv1001BarcodeDto
            {
                BarcodeNo = b.BarcodeNo,
                ProductNo = b.ProductNo,
                VariantNo = b.VariantNo,
                Barcode = b.Barcode,
                BarcodeType = b.BarcodeType,
                IsPrimary = b.IsPrimary
            })
            .ToListAsync(ct);

        return dto;
    }

    public async Task<Inv1001ProductDto> SaveAsync(Inv1001ProductDto dto, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");

        if (string.IsNullOrWhiteSpace(dto.ProductName)) throw new ValidationException("Product name is required");
        if (dto.CategoryNo <= 0) throw new ValidationException("Category is required");
        if (dto.UomNo <= 0) throw new ValidationException("UOM is required");

        InvProduct p;
        if (dto.ProductNo.HasValue && dto.ProductNo.Value > 0)
        {
            p = await _db.InvProducts.FirstOrDefaultAsync(x => x.ProductNo == dto.ProductNo.Value && x.IsDeleted == 0, ct)
                ?? throw new NotFoundException($"Product not found: {dto.ProductNo}");

            p.ProductName = dto.ProductName.Trim();
            p.CategoryNo = dto.CategoryNo;
            p.BrandNo = dto.BrandNo;
            p.UomNo = dto.UomNo;
            p.CostPrice = dto.CostPrice;
            p.SellingPrice = dto.SellingPrice;
            p.Mrp = dto.Mrp;
            p.TaxRate = dto.TaxRate;
            p.HasVariants = dto.HasVariants;
            p.HasBatches = dto.HasBatches;
            p.HasExpiry = dto.HasExpiry;
            p.IsActive = dto.IsActive;
            p.UpdatedBy = _ctx.CurrentUserNo(); p.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            string code = string.IsNullOrWhiteSpace(dto.ProductCode) ? $"PRD{DateTime.UtcNow:yyyyMMddHHmmss}" : dto.ProductCode.Trim().ToUpperInvariant();

            p = new InvProduct
            {
                CompanyNo = companyNo,
                ProductId = code,
                ProductName = dto.ProductName.Trim(),
                CategoryNo = dto.CategoryNo,
                BrandNo = dto.BrandNo,
                UomNo = dto.UomNo,
                CostPrice = dto.CostPrice,
                SellingPrice = dto.SellingPrice,
                Mrp = dto.Mrp,
                TaxRate = dto.TaxRate,
                HasVariants = dto.HasVariants,
                HasBatches = dto.HasBatches,
                HasExpiry = dto.HasExpiry,
                IsStockable = 1,
                Status = "ACTIVE",
                IsActive = dto.IsActive, IsDeleted = 0,
                CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow
            };
            _db.InvProducts.Add(p);
            await _db.SaveChangesAsync(ct);
        }

        await _db.SaveChangesAsync(ct);
        return await GetDetailAsync(p.ProductNo, ct);
    }

    public async Task DeleteAsync(long productNo, CancellationToken ct = default)
    {
        var p = await _db.InvProducts.FirstOrDefaultAsync(x => x.ProductNo == productNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Product not found: {productNo}");

        p.IsDeleted = 1; p.IsActive = 0;
        p.DeletedBy = _ctx.CurrentUserNo(); p.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<Inv1001BarcodeResolveDto?> ResolveBarcodeAsync(string barcode, CancellationToken ct = default)
    {
        var bc = await _db.InvProductBarcodes.AsNoTracking().FirstOrDefaultAsync(b => b.Barcode == barcode && b.IsDeleted == 0, ct);
        if (bc == null) return null;

        var prd = await _db.InvProducts.AsNoTracking().FirstOrDefaultAsync(p => p.ProductNo == bc.ProductNo && p.IsDeleted == 0, ct);
        if (prd == null) return null;

        string? varName = null;
        if (bc.VariantNo.HasValue)
        {
            varName = await _db.InvProductVariants.Where(v => v.VariantNo == bc.VariantNo.Value).Select(v => v.VariantName).FirstOrDefaultAsync(ct);
        }

        return new Inv1001BarcodeResolveDto
        {
            Barcode = barcode,
            ProductNo = prd.ProductNo,
            ProductCode = prd.ProductId,
            ProductName = prd.ProductName,
            VariantNo = bc.VariantNo,
            VariantName = varName,
            UomNo = prd.UomNo,
            UnitPrice = prd.SellingPrice
        };
    }

    private static Inv1001ProductDto ToDto(InvProduct p, string? catName, string? uomName, string? brandName) => new()
    {
        ProductNo = p.ProductNo,
        ProductCode = p.ProductId,
        ProductName = p.ProductName,
        CategoryNo = p.CategoryNo,
        CategoryName = catName,
        BrandNo = p.BrandNo,
        BrandName = brandName,
        UomNo = p.UomNo,
        UomName = uomName,
        CostPrice = p.CostPrice,
        SellingPrice = p.SellingPrice,
        Mrp = p.Mrp,
        TaxRate = p.TaxRate,
        HasVariants = p.HasVariants,
        HasBatches = p.HasBatches,
        HasExpiry = p.HasExpiry,
        IsActive = p.IsActive,
        RowVersion = p.RowVersion
    };
}
