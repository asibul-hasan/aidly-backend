using AidlyErp.Inv.Application.Dto;
using AidlyErp.Inv.Application.Interfaces;
using AidlyErp.Inv.Domain;
using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Sys.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AidlyErp.Inv.Application.Services;

public interface IInv1001Service
{
    Task<List<Inv1001ProductDto>> GetListAsync(CancellationToken ct = default);
    Task<Inv1001ProductDto> GetDetailAsync(long productNo, CancellationToken ct = default);
    Task<Inv1001LookupDto> GetLookupsAsync(CancellationToken ct = default);
    Task<Inv1001ProductDto> SaveAsync(Inv1001ProductDto dto, CancellationToken ct = default);
    Task DeleteAsync(long productNo, CancellationToken ct = default);
    Task<Inv1001BarcodeResolveDto?> ResolveBarcodeAsync(string barcode, CancellationToken ct = default);
}

/// <summary>
/// INV_1001 Product Master — the catalogue everything else transacts on. Owns the product header
/// plus three child collections edited on the same form: variants, barcodes and UOM conversions.
/// Once stock has moved, the tracking flags and base UOM freeze, because changing them would
/// reinterpret quantities already in the ledger.
/// </summary>
public class Inv1001Service : IInv1001Service
{
    private const short Deleted = 0;

    private readonly IInvDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    private readonly IVatTaxLookup _vatTaxLookup;

    public Inv1001Service(IInvDbContext db, ICompanyBranchContext ctx, IVatTaxLookup vatTaxLookup)
    {
        _db = db;
        _ctx = ctx;
        _vatTaxLookup = vatTaxLookup;
    }

    // ── reads ────────────────────────────────────────────────────────────────

    public async Task<List<Inv1001ProductDto>> GetListAsync(CancellationToken ct = default)
    {
        long companyNo = Company();

        var rows = await _db.InvProducts.AsNoTracking()
            .Where(p => p.CompanyNo == companyNo && p.IsDeleted == Deleted)
            .OrderBy(p => p.ProductNo)
            .ToListAsync(ct);

        var names = await NameMapsAsync(rows, ct);
        return rows.Select(p => ToDto(p, names)).ToList();
    }

    public async Task<Inv1001ProductDto> GetDetailAsync(long productNo, CancellationToken ct = default)
    {
        var p = await RequireProductAsync(productNo, ct);

        var names = await NameMapsAsync(new[] { p }, ct);
        var dto = ToDto(p, names);
        dto.HasMovements = await HasMovementsAsync(productNo, ct);

        dto.Variants = await _db.InvProductVariants.AsNoTracking()
            .Where(v => v.ProductNo == productNo && v.IsDeleted == Deleted)
            .OrderBy(v => v.VariantNo)
            .Select(v => new Inv1001VariantDto
            {
                VariantNo = v.VariantNo,
                VariantSku = v.VariantSku,
                VariantName = v.VariantName,
                Barcode = v.Barcode,
                Attr1ValueNo = v.Attr1ValueNo,
                Attr2ValueNo = v.Attr2ValueNo,
                Attr3ValueNo = v.Attr3ValueNo,
                PurchasePrice = v.PurchasePrice,
                SalePrice = v.SalePrice,
                Mrp = v.Mrp,
                IsActive = v.IsActive
            })
            .ToListAsync(ct);

        dto.Barcodes = await _db.InvProductBarcodes.AsNoTracking()
            .Where(b => b.ProductNo == productNo && b.IsDeleted == Deleted)
            .OrderBy(b => b.BarcodeNo)
            .Select(b => new Inv1001BarcodeDto
            {
                BarcodeNo = b.BarcodeNo,
                VariantNo = b.VariantNo,
                Barcode = b.Barcode,
                UomNo = b.UomNo,
                PackQty = b.PackQty,
                BarcodeType = b.BarcodeType,
                IsPrimary = b.IsPrimary,
                IsActive = b.IsActive
            })
            .ToListAsync(ct);

        var conversions = await _db.InvUomConversions.AsNoTracking()
            .Where(c => c.ProductNo == productNo && c.IsDeleted == Deleted)
            .OrderBy(c => c.UomConversionNo)
            .ToListAsync(ct);

        var uomNames = await UomNamesAsync(conversions.Select(c => c.FromUomNo), ct);
        dto.UomConversions = conversions.Select(c => new Inv1001UomConvDto
        {
            UomConversionNo = c.UomConversionNo,
            FromUomNo = c.FromUomNo,
            FromUomName = uomNames.GetValueOrDefault(c.FromUomNo),
            ToBaseFactor = c.ToBaseFactor,
            IsActive = c.IsActive
        }).ToList();

        return dto;
    }

    public async Task<Inv1001LookupDto> GetLookupsAsync(CancellationToken ct = default)
    {
        long companyNo = Company();

        var categories = await _db.InvCategories.AsNoTracking()
            .Where(c => c.CompanyNo == companyNo && c.IsDeleted == Deleted)
            .OrderBy(c => c.CategoryNo)
            .Select(c => new InvOptionDto(c.CategoryNo, c.CategoryName))
            .ToListAsync(ct);

        var brands = await _db.InvBrands.AsNoTracking()
            .Where(b => b.CompanyNo == companyNo && b.IsDeleted == Deleted)
            .OrderBy(b => b.BrandNo)
            .Select(b => new InvOptionDto(b.BrandNo, b.BrandName))
            .ToListAsync(ct);

        var uoms = await _db.InvUoms.AsNoTracking()
            .Where(u => u.CompanyNo == companyNo && u.IsDeleted == Deleted)
            .OrderBy(u => u.UomNo)
            .Select(u => new InvOptionDto(u.UomNo, u.UomName))
            .ToListAsync(ct);

        var attributes = await _db.InvProductAttributes.AsNoTracking()
            .Where(a => a.CompanyNo == companyNo && a.IsDeleted == Deleted)
            .OrderBy(a => a.OrderSl).ThenBy(a => a.AttributeNo)
            .ToListAsync(ct);

        var attributeNos = attributes.Select(a => a.AttributeNo).ToList();
        var values = await _db.InvProductAttributeValues.AsNoTracking()
            .Where(v => attributeNos.Contains(v.AttributeNo) && v.IsDeleted == Deleted)
            .OrderBy(v => v.OrderSl).ThenBy(v => v.AttributeValueNo)
            .ToListAsync(ct);

        var valuesByAttribute = values.GroupBy(v => v.AttributeNo)
            .ToDictionary(g => g.Key, g => g.Select(v => new InvOptionDto(v.AttributeValueNo, v.ValueName)).ToList());

        return new Inv1001LookupDto
        {
            Categories = categories,
            Brands = brands,
            Uoms = uoms,
            Taxes = await TaxOptionsAsync(companyNo, ct),
            Attributes = attributes.Select(a => new Inv1001AttributeOptionDto
            {
                AttributeNo = a.AttributeNo,
                AttributeName = a.AttributeName,
                Values = valuesByAttribute.GetValueOrDefault(a.AttributeNo) ?? new()
            }).ToList()
        };
    }

    /// <summary>
    /// Scanner entry point. Returns the barcode's own UOM and pack size, so a carton barcode adds
    /// its full pack quantity rather than one unit.
    /// </summary>
    public async Task<Inv1001BarcodeResolveDto?> ResolveBarcodeAsync(string barcode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(barcode)) return null;
        long companyNo = Company();

        var bc = await _db.InvProductBarcodes.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Barcode == barcode && b.CompanyNo == companyNo && b.IsDeleted == Deleted, ct);

        var product = bc is null
            ? await _db.InvProducts.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Barcode == barcode && p.CompanyNo == companyNo && p.IsDeleted == Deleted, ct)
            : await _db.InvProducts.AsNoTracking()
                .FirstOrDefaultAsync(p => p.ProductNo == bc.ProductNo && p.IsDeleted == Deleted, ct);

        if (product is null) return null;

        string? variantName = null;
        if (bc?.VariantNo is not null)
        {
            variantName = await _db.InvProductVariants.AsNoTracking()
                .Where(v => v.VariantNo == bc.VariantNo)
                .Select(v => v.VariantName)
                .FirstOrDefaultAsync(ct);
        }

        return new Inv1001BarcodeResolveDto
        {
            Barcode = barcode,
            ProductNo = product.ProductNo,
            ProductId = product.ProductId,
            ProductName = product.ProductName,
            VariantNo = bc?.VariantNo,
            VariantName = variantName,
            UomNo = bc?.UomNo ?? product.BaseUomNo,
            PackQty = bc?.PackQty ?? 1m,
            UnitPrice = product.SalePrice
        };
    }

    // ── write ────────────────────────────────────────────────────────────────

    public async Task<Inv1001ProductDto> SaveAsync(Inv1001ProductDto dto, CancellationToken ct = default)
    {
        long companyNo = Company();
        await ValidateHeaderAsync(dto, ct);

        InvProduct e;
        bool isNew = dto.ProductNo is null or <= 0;

        if (!isNew)
        {
            e = await RequireProductAsync(dto.ProductNo!.Value, ct);

            if (!string.Equals(e.ProductId, dto.ProductId, StringComparison.OrdinalIgnoreCase)
                && await CodeTakenAsync(companyNo, dto.ProductId!, e.ProductNo, ct))
            {
                throw new ValidationException($"Product code already exists: {dto.ProductId}");
            }

            if (await HasMovementsAsync(e.ProductNo, ct)) GuardFrozenFields(e, dto);
        }
        else
        {
            if (await CodeTakenAsync(companyNo, dto.ProductId!, null, ct))
                throw new ValidationException($"Product code already exists: {dto.ProductId}");

            e = new InvProduct
            {
                CompanyNo = companyNo,
                CreatedBy = _ctx.CurrentUserNo(),
                CreatedAt = DateTime.UtcNow,
                IsDeleted = 0
            };
            _db.InvProducts.Add(e);
        }

        if (!string.IsNullOrWhiteSpace(dto.Barcode)
            && (isNew || !string.Equals(dto.Barcode, e.Barcode, StringComparison.OrdinalIgnoreCase))
            && await ProductBarcodeTakenAsync(companyNo, dto.Barcode!, isNew ? null : e.ProductNo, ct))
        {
            throw new ValidationException($"Barcode already assigned to another product: {dto.Barcode}");
        }

        ApplyHeader(e, dto);

        // The product PK is needed before its children can reference it.
        await _db.SaveChangesAsync(ct);

        await ReconcileVariantsAsync(e.ProductNo, dto.Variants, ct);
        await ReconcileBarcodesAsync(e.ProductNo, companyNo, dto.Barcodes, ct);
        await ReconcileUomConversionsAsync(e.ProductNo, dto.UomConversions, ct);

        await _db.SaveChangesAsync(ct);
        return await GetDetailAsync(e.ProductNo, ct);
    }

    public async Task DeleteAsync(long productNo, CancellationToken ct = default)
    {
        var e = await RequireProductAsync(productNo, ct);

        if (await HasMovementsAsync(productNo, ct))
            throw new ValidationException("Cannot delete a product with stock movements — discontinue it instead");

        long? user = _ctx.CurrentUserNo();

        var variants = await _db.InvProductVariants
            .Where(v => v.ProductNo == productNo && v.IsDeleted == Deleted).ToListAsync(ct);
        foreach (var v in variants) v.PerformSoftDelete(user);

        var barcodes = await _db.InvProductBarcodes
            .Where(b => b.ProductNo == productNo && b.IsDeleted == Deleted).ToListAsync(ct);
        foreach (var b in barcodes) b.PerformSoftDelete(user);

        var conversions = await _db.InvUomConversions
            .Where(c => c.ProductNo == productNo && c.IsDeleted == Deleted).ToListAsync(ct);
        foreach (var c in conversions) c.PerformSoftDelete(user);

        e.PerformSoftDelete(user);
        await _db.SaveChangesAsync(ct);
    }

    // ── child collections ────────────────────────────────────────────────────

    private async Task ReconcileVariantsAsync(long productNo, List<Inv1001VariantDto> rows, CancellationToken ct)
    {
        rows ??= new List<Inv1001VariantDto>();

        var existing = await _db.InvProductVariants
            .Where(v => v.ProductNo == productNo && v.IsDeleted == Deleted).ToListAsync(ct);
        var byId = existing.ToDictionary(v => v.VariantNo);

        var seenSku = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var kept = new HashSet<long>();

        foreach (var r in rows)
        {
            string sku = r.VariantSku?.Trim() ?? string.Empty;
            if (!seenSku.Add(sku)) throw new ValidationException($"Duplicate variant SKU: {r.VariantSku}");

            InvProductVariant? v = r.VariantNo is > 0 ? byId.GetValueOrDefault(r.VariantNo.Value) : null;
            if (v is null)
            {
                v = new InvProductVariant
                {
                    ProductNo = productNo,
                    CreatedBy = _ctx.CurrentUserNo(),
                    CreatedAt = DateTime.UtcNow,
                    IsDeleted = 0
                };
                _db.InvProductVariants.Add(v);
            }

            v.VariantSku = sku;
            v.VariantName = r.VariantName ?? sku;
            v.Barcode = string.IsNullOrWhiteSpace(r.Barcode) ? null : r.Barcode;
            v.Attr1ValueNo = r.Attr1ValueNo;
            v.Attr2ValueNo = r.Attr2ValueNo;
            v.Attr3ValueNo = r.Attr3ValueNo;
            v.PurchasePrice = r.PurchasePrice;
            v.SalePrice = r.SalePrice;
            v.Mrp = r.Mrp;
            v.IsActive = r.IsActive ?? 1;
            v.UpdatedBy = _ctx.CurrentUserNo();
            v.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);
            kept.Add(v.VariantNo);
        }

        foreach (var v in existing)
            if (!kept.Contains(v.VariantNo)) v.PerformSoftDelete(_ctx.CurrentUserNo());
    }

    private async Task ReconcileBarcodesAsync(long productNo, long companyNo, List<Inv1001BarcodeDto> rows,
                                              CancellationToken ct)
    {
        rows ??= new List<Inv1001BarcodeDto>();

        var existing = await _db.InvProductBarcodes
            .Where(b => b.ProductNo == productNo && b.IsDeleted == Deleted).ToListAsync(ct);
        var byId = existing.ToDictionary(b => b.BarcodeNo);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var kept = new HashSet<long>();

        foreach (var r in rows)
        {
            string code = r.Barcode?.Trim() ?? string.Empty;
            if (!seen.Add(code)) throw new ValidationException($"Duplicate barcode: {r.Barcode}");

            InvProductBarcode? b = r.BarcodeNo is > 0 ? byId.GetValueOrDefault(r.BarcodeNo.Value) : null;
            if (b is null)
            {
                b = new InvProductBarcode
                {
                    CompanyNo = companyNo,
                    ProductNo = productNo,
                    CreatedBy = _ctx.CurrentUserNo(),
                    CreatedAt = DateTime.UtcNow,
                    IsDeleted = 0
                };
                _db.InvProductBarcodes.Add(b);
            }

            b.VariantNo = r.VariantNo;
            b.Barcode = code;
            b.UomNo = r.UomNo ?? 0;
            b.PackQty = r.PackQty ?? 1m;
            b.BarcodeType = r.BarcodeType ?? 1;
            b.IsPrimary = r.IsPrimary ?? 0;
            b.IsActive = r.IsActive ?? 1;
            b.UpdatedBy = _ctx.CurrentUserNo();
            b.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);
            kept.Add(b.BarcodeNo);
        }

        foreach (var b in existing)
            if (!kept.Contains(b.BarcodeNo)) b.PerformSoftDelete(_ctx.CurrentUserNo());
    }

    /// <summary>
    /// Keyed on <c>from_uom_no</c> rather than the PK, and matched across soft-deleted rows too:
    /// the table is uniquely constrained on (product, from-UOM), so re-adding a previously removed
    /// conversion must resurrect that row instead of inserting a colliding one.
    /// </summary>
    private async Task ReconcileUomConversionsAsync(long productNo, List<Inv1001UomConvDto> rows, CancellationToken ct)
    {
        rows ??= new List<Inv1001UomConvDto>();

        var all = await _db.InvUomConversions.Where(c => c.ProductNo == productNo).ToListAsync(ct);
        var byFromUom = new Dictionary<long, InvUomConversion>();
        foreach (var c in all) byFromUom.TryAdd(c.FromUomNo, c);

        var seen = new HashSet<long>();
        var keptFromUom = new HashSet<long>();

        foreach (var r in rows)
        {
            if (r.FromUomNo is null or <= 0) throw new ValidationException("Conversion needs a from-UOM");
            if (!seen.Add(r.FromUomNo.Value)) throw new ValidationException("Duplicate UOM conversion");
            if (r.ToBaseFactor is null or <= 0)
                throw new ValidationException("Conversion factor must be greater than zero");

            if (!byFromUom.TryGetValue(r.FromUomNo.Value, out var c))
            {
                c = new InvUomConversion
                {
                    ProductNo = productNo,
                    FromUomNo = r.FromUomNo.Value,
                    CreatedBy = _ctx.CurrentUserNo(),
                    CreatedAt = DateTime.UtcNow
                };
                _db.InvUomConversions.Add(c);
            }

            c.ToBaseFactor = r.ToBaseFactor.Value;
            c.IsActive = 1;
            c.IsDeleted = 0;
            c.UpdatedBy = _ctx.CurrentUserNo();
            c.UpdatedAt = DateTime.UtcNow;

            keptFromUom.Add(r.FromUomNo.Value);
        }

        foreach (var c in all)
            if (c.IsDeleted == Deleted && !keptFromUom.Contains(c.FromUomNo))
                c.PerformSoftDelete(_ctx.CurrentUserNo());
    }

    // ── validation ───────────────────────────────────────────────────────────

    private async Task ValidateHeaderAsync(Inv1001ProductDto d, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(d.ProductId)) throw new ValidationException("Product code is required");
        if (string.IsNullOrWhiteSpace(d.ProductName)) throw new ValidationException("Product name is required");

        if (d.ProductType is null or < 1 or > 4)
            throw new ValidationException("Product type must be 1=Standard, 2=VariantParent, 3=Service or 4=Bundle");

        if (d.BaseUomNo <= 0) throw new ValidationException("Base UOM is required");

        // Expiry dates live on batches, so tracking one without the other has nowhere to record it.
        if (d.IsExpiryTracked == 1 && d.IsBatchTracked != 1)
            throw new ValidationException("Expiry tracking requires batch tracking to be enabled");

        if (d.MinSalePrice is not null && d.Mrp is not null && d.MinSalePrice > d.Mrp)
            throw new ValidationException("Minimum sale price cannot exceed MRP");

        long companyNo = Company();
        bool categoryExists = await _db.InvCategories.AnyAsync(
            c => c.CategoryNo == d.CategoryNo && c.CompanyNo == companyNo && c.IsDeleted == Deleted, ct);
        if (!categoryExists) throw new NotFoundException("Category not found");
    }

    /// <summary>
    /// Blocks edits that would reinterpret quantities already written to the ledger — switching the
    /// base UOM or turning batch tracking on retroactively makes existing rows mean something else.
    /// </summary>
    private static void GuardFrozenFields(InvProduct e, Inv1001ProductDto d)
    {
        bool changed = e.BaseUomNo != d.BaseUomNo
                       || e.IsStockTracked != (d.IsStockTracked ?? 1)
                       || e.IsBatchTracked != (d.IsBatchTracked ?? 0)
                       || e.IsExpiryTracked != (d.IsExpiryTracked ?? 0)
                       || e.IsSerialTracked != (d.IsSerialTracked ?? 0);

        if (changed)
            throw new ValidationException("Tracking flags and base UOM are locked — stock movements exist for this product");
    }

    private static void ApplyHeader(InvProduct e, Inv1001ProductDto d)
    {
        e.ProductId = d.ProductId!.Trim();
        e.ProductName = d.ProductName!.Trim();
        e.ProductNameNls = d.ProductNameNls;
        e.ShortName = d.ShortName;
        e.ProductType = d.ProductType!.Value;
        e.CategoryNo = d.CategoryNo;
        e.BrandNo = d.BrandNo;
        e.BaseUomNo = d.BaseUomNo;
        e.PurchaseUomNo = d.PurchaseUomNo;
        e.SalesUomNo = d.SalesUomNo;
        e.VatTaxNo = d.VatTaxNo;
        e.IsTaxInclusive = d.IsTaxInclusive ?? 0;
        e.HsnSacCode = d.HsnSacCode;
        e.IsStockTracked = d.IsStockTracked ?? 1;
        e.IsBatchTracked = d.IsBatchTracked ?? 0;
        e.IsExpiryTracked = d.IsExpiryTracked ?? 0;
        e.IsSerialTracked = d.IsSerialTracked ?? 0;
        e.HasVariants = d.HasVariants ?? 0;
        e.ShelfLifeDays = d.ShelfLifeDays;
        e.CostPrice = d.CostPrice ?? 0m;
        e.PurchasePrice = d.PurchasePrice ?? 0m;
        e.SalePrice = d.SalePrice ?? 0m;
        e.Mrp = d.Mrp ?? 0m;
        e.MinSalePrice = d.MinSalePrice;
        e.DefaultMarginPct = d.DefaultMarginPct;
        e.ReorderLevel = d.ReorderLevel ?? 0m;
        e.ReorderQty = d.ReorderQty ?? 0m;
        e.MinStock = d.MinStock ?? 0m;
        e.MaxStock = d.MaxStock;
        e.WeightGm = d.WeightGm;
        e.Barcode = string.IsNullOrWhiteSpace(d.Barcode) ? null : d.Barcode.Trim();
        e.ImagePath = d.ImagePath;
        e.IsSellable = d.IsSellable ?? 1;
        e.IsPurchasable = d.IsPurchasable ?? 1;
        e.AllowDiscount = d.AllowDiscount ?? 1;
        e.Remarks = d.Remarks;
        e.IsActive = d.IsActive ?? 1;
        e.UpdatedAt = DateTime.UtcNow;
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private async Task<bool> HasMovementsAsync(long productNo, CancellationToken ct) =>
        await _db.InvStockLedgers.AnyAsync(l => l.ProductNo == productNo, ct);

    private async Task<bool> CodeTakenAsync(long companyNo, string productId, long? excludeNo, CancellationToken ct) =>
        await _db.InvProducts.AnyAsync(
            p => p.CompanyNo == companyNo && p.ProductId == productId && p.IsDeleted == Deleted
                 && (excludeNo == null || p.ProductNo != excludeNo), ct);

    private async Task<bool> ProductBarcodeTakenAsync(long companyNo, string barcode, long? excludeNo,
                                                      CancellationToken ct) =>
        await _db.InvProducts.AnyAsync(
            p => p.CompanyNo == companyNo && p.Barcode == barcode && p.IsDeleted == Deleted
                 && (excludeNo == null || p.ProductNo != excludeNo), ct);

    private async Task<InvProduct> RequireProductAsync(long productNo, CancellationToken ct)
    {
        var p = await _db.InvProducts.FirstOrDefaultAsync(x => x.ProductNo == productNo && x.IsDeleted == Deleted, ct)
                ?? throw new NotFoundException($"Product not found: {productNo}");

        if (p.CompanyNo != Company()) throw new ValidationException("Product does not belong to your company");
        return p;
    }

    private async Task<List<InvOptionDto>> TaxOptionsAsync(long companyNo, CancellationToken ct)
    {
        // VAT codes are SYS master data; INV only needs their labels for the dropdown.
        var vatTaxNos = await _db.InvProducts.AsNoTracking()
            .Where(p => p.CompanyNo == companyNo && p.VatTaxNo != null && p.IsDeleted == Deleted)
            .Select(p => p.VatTaxNo!.Value)
            .Distinct()
            .ToListAsync(ct);

        var categoryTaxNos = await _db.InvCategories.AsNoTracking()
            .Where(c => c.CompanyNo == companyNo && c.DefaultVatTaxNo != null && c.IsDeleted == Deleted)
            .Select(c => c.DefaultVatTaxNo!.Value)
            .Distinct()
            .ToListAsync(ct);

        var all = vatTaxNos.Concat(categoryTaxNos).Distinct().ToList();
        if (all.Count == 0) return new List<InvOptionDto>();

        var codes = await _vatTaxLookup.GetTaxCodesAsync(all, ct);
        return all.Select(no => new InvOptionDto(no, codes.GetValueOrDefault(no, no.ToString()))).ToList();
    }

    private async Task<Dictionary<long, string>> UomNamesAsync(IEnumerable<long> uomNos, CancellationToken ct)
    {
        var nos = uomNos.Distinct().ToList();
        if (nos.Count == 0) return new Dictionary<long, string>();

        return await _db.InvUoms.AsNoTracking()
            .Where(u => nos.Contains(u.UomNo))
            .ToDictionaryAsync(u => u.UomNo, u => u.UomName, ct);
    }

    private sealed record NameMaps(Dictionary<long, string> Categories, Dictionary<long, string> Brands,
                                   Dictionary<long, string> Uoms);

    private async Task<NameMaps> NameMapsAsync(IEnumerable<InvProduct> products, CancellationToken ct)
    {
        var list = products.ToList();

        var categoryNos = list.Select(p => p.CategoryNo).Distinct().ToList();
        var categories = await _db.InvCategories.AsNoTracking()
            .Where(c => categoryNos.Contains(c.CategoryNo))
            .ToDictionaryAsync(c => c.CategoryNo, c => c.CategoryName, ct);

        var brandNos = list.Where(p => p.BrandNo.HasValue).Select(p => p.BrandNo!.Value).Distinct().ToList();
        var brands = brandNos.Count == 0
            ? new Dictionary<long, string>()
            : await _db.InvBrands.AsNoTracking()
                .Where(b => brandNos.Contains(b.BrandNo))
                .ToDictionaryAsync(b => b.BrandNo, b => b.BrandName, ct);

        var uoms = await UomNamesAsync(list.Select(p => p.BaseUomNo), ct);

        return new NameMaps(categories, brands, uoms);
    }

    private static Inv1001ProductDto ToDto(InvProduct p, NameMaps names) => new()
    {
        ProductNo = p.ProductNo,
        ProductId = p.ProductId,
        ProductName = p.ProductName,
        ProductNameNls = p.ProductNameNls,
        ShortName = p.ShortName,
        ProductType = p.ProductType,
        CategoryNo = p.CategoryNo,
        CategoryName = names.Categories.GetValueOrDefault(p.CategoryNo),
        BrandNo = p.BrandNo,
        BrandName = p.BrandNo is null ? null : names.Brands.GetValueOrDefault(p.BrandNo.Value),
        BaseUomNo = p.BaseUomNo,
        BaseUomName = names.Uoms.GetValueOrDefault(p.BaseUomNo),
        PurchaseUomNo = p.PurchaseUomNo,
        SalesUomNo = p.SalesUomNo,
        VatTaxNo = p.VatTaxNo,
        IsTaxInclusive = p.IsTaxInclusive,
        HsnSacCode = p.HsnSacCode,
        IsStockTracked = p.IsStockTracked,
        IsBatchTracked = p.IsBatchTracked,
        IsExpiryTracked = p.IsExpiryTracked,
        IsSerialTracked = p.IsSerialTracked,
        HasVariants = p.HasVariants,
        ShelfLifeDays = p.ShelfLifeDays,
        CostPrice = p.CostPrice,
        PurchasePrice = p.PurchasePrice,
        SalePrice = p.SalePrice,
        Mrp = p.Mrp,
        MinSalePrice = p.MinSalePrice,
        DefaultMarginPct = p.DefaultMarginPct,
        ReorderLevel = p.ReorderLevel,
        ReorderQty = p.ReorderQty,
        MinStock = p.MinStock,
        MaxStock = p.MaxStock,
        WeightGm = p.WeightGm,
        Barcode = p.Barcode,
        ImagePath = p.ImagePath,
        IsSellable = p.IsSellable,
        IsPurchasable = p.IsPurchasable,
        AllowDiscount = p.AllowDiscount,
        Remarks = p.Remarks,
        IsActive = p.IsActive,
        RowVersion = p.RowVersion
    };

    private long Company() =>
        _ctx.CurrentCompanyNo() ?? throw new ValidationException("No active company in context");
}
