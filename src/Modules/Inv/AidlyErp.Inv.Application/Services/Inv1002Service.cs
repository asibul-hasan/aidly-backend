using System.Security.Cryptography;
using AidlyErp.Inv.Application.Dto;
using AidlyErp.Inv.Application.Interfaces;
using AidlyErp.Inv.Domain;
using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Security;
using Microsoft.EntityFrameworkCore;

namespace AidlyErp.Inv.Application.Services;

public interface IInv1002Service
{
    Task<Inv1002LookupDto> GetLookupsAsync(CancellationToken ct = default);
    Task<List<Inv1002BarcodeDto>> GetBarcodesAsync(long productNo, CancellationToken ct = default);
    Task<string> NextCodeAsync(CancellationToken ct = default);
    Task<Inv1002BarcodeDto> SaveAsync(Inv1002BarcodeDto dto, CancellationToken ct = default);
    Task DeleteAsync(long barcodeNo, CancellationToken ct = default);
}

/// <summary>
/// INV_1002 Barcode Generator — the multi-barcode map a scanner resolves against. A product may
/// carry one barcode per selling UOM; <c>pack_qty</c> is what turns a scanned carton into pieces.
/// </summary>
public class Inv1002Service : IInv1002Service
{
    private const short Deleted = 0;

    private readonly IInvDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    private readonly IInvLookupService _lookups;

    public Inv1002Service(IInvDbContext db, ICompanyBranchContext ctx, IInvLookupService lookups)
    {
        _db = db;
        _ctx = ctx;
        _lookups = lookups;
    }

    public async Task<Inv1002LookupDto> GetLookupsAsync(CancellationToken ct = default) => new()
    {
        Products = await _lookups.GetProductOptionsAsync(ct),
        Uoms = await _lookups.GetUomOptionsAsync(ct)
    };

    public async Task<List<Inv1002BarcodeDto>> GetBarcodesAsync(long productNo, CancellationToken ct = default)
    {
        long companyNo = Company();

        var rows = await _db.InvProductBarcodes.AsNoTracking()
            .Where(b => b.ProductNo == productNo && b.CompanyNo == companyNo && b.IsDeleted == Deleted)
            .OrderBy(b => b.BarcodeNo)
            .ToListAsync(ct);

        if (rows.Count == 0) return new List<Inv1002BarcodeDto>();

        string? productName = await _db.InvProducts.AsNoTracking()
            .Where(p => p.ProductNo == productNo)
            .Select(p => p.ProductName)
            .FirstOrDefaultAsync(ct);

        var uomNos = rows.Select(r => r.UomNo).Distinct().ToList();
        var uoms = await _db.InvUoms.AsNoTracking()
            .Where(u => uomNos.Contains(u.UomNo))
            .ToDictionaryAsync(u => u.UomNo, u => u.UomName, ct);

        return rows.Select(b => ToDto(b, productName, uoms)).ToList();
    }

    /// <summary>
    /// An internal EAN-13 in the GS1 "20" restricted-circulation prefix, so generated codes can
    /// never collide with a manufacturer's real barcode. Retries until the company has no such code.
    /// </summary>
    public async Task<string> NextCodeAsync(CancellationToken ct = default)
    {
        long companyNo = Company();

        for (int attempt = 0; attempt < 50; attempt++)
        {
            string code = GenerateEan13();
            bool taken = await _db.InvProductBarcodes
                .AnyAsync(b => b.CompanyNo == companyNo && b.Barcode == code && b.IsDeleted == Deleted, ct);
            if (!taken) return code;
        }

        throw new ValidationException("Could not generate a unique barcode, please retry");
    }

    public async Task<Inv1002BarcodeDto> SaveAsync(Inv1002BarcodeDto dto, CancellationToken ct = default)
    {
        long companyNo = Company();

        var product = await RequireProductAsync(dto.ProductNo, ct);

        bool uomExists = await _db.InvUoms.AnyAsync(u => u.UomNo == dto.UomNo && u.IsDeleted == Deleted, ct);
        if (!uomExists) throw new NotFoundException("UOM not found");

        if (dto.PackQty is <= 0) throw new ValidationException("Pack qty must be greater than zero");
        if (string.IsNullOrWhiteSpace(dto.Barcode)) throw new ValidationException("Barcode is required");

        InvProductBarcode e;
        if (dto.BarcodeNo is > 0)
        {
            e = await _db.InvProductBarcodes.FirstOrDefaultAsync(
                    b => b.BarcodeNo == dto.BarcodeNo.Value && b.IsDeleted == Deleted, ct)
                ?? throw new NotFoundException("Barcode not found");

            if (e.CompanyNo != companyNo) throw new ValidationException("Barcode belongs to another company");

            if (!string.Equals(e.Barcode, dto.Barcode, StringComparison.OrdinalIgnoreCase)
                && await BarcodeTakenAsync(companyNo, dto.Barcode, e.BarcodeNo, ct))
            {
                throw new ValidationException($"Barcode already exists: {dto.Barcode}");
            }
        }
        else
        {
            if (await BarcodeTakenAsync(companyNo, dto.Barcode, null, ct))
                throw new ValidationException($"Barcode already exists: {dto.Barcode}");

            e = new InvProductBarcode
            {
                CompanyNo = companyNo,
                CreatedBy = _ctx.CurrentUserNo(),
                CreatedAt = DateTime.UtcNow,
                IsDeleted = 0
            };
            _db.InvProductBarcodes.Add(e);
        }

        e.ProductNo = dto.ProductNo;
        e.VariantNo = dto.VariantNo;
        e.Barcode = dto.Barcode;
        e.UomNo = dto.UomNo;
        e.PackQty = dto.PackQty ?? 1m;
        e.BarcodeType = dto.BarcodeType ?? 1;
        e.IsPrimary = dto.IsPrimary ?? 0;
        e.IsActive = dto.IsActive ?? 1;
        e.UpdatedBy = _ctx.CurrentUserNo();
        e.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        if (e.IsPrimary == 1) await DemoteOtherPrimariesAsync(e, ct);

        var uoms = await _db.InvUoms.AsNoTracking()
            .Where(u => u.UomNo == e.UomNo)
            .ToDictionaryAsync(u => u.UomNo, u => u.UomName, ct);

        return ToDto(e, product.ProductName, uoms);
    }

    public async Task DeleteAsync(long barcodeNo, CancellationToken ct = default)
    {
        long companyNo = Company();

        var e = await _db.InvProductBarcodes.FirstOrDefaultAsync(
                    b => b.BarcodeNo == barcodeNo && b.IsDeleted == Deleted, ct)
                ?? throw new NotFoundException("Barcode not found");

        if (e.CompanyNo != companyNo) throw new ValidationException("Barcode belongs to another company");

        e.PerformSoftDelete(_ctx.CurrentUserNo());
        await _db.SaveChangesAsync(ct);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private async Task<bool> BarcodeTakenAsync(long companyNo, string barcode, long? excludeNo, CancellationToken ct) =>
        await _db.InvProductBarcodes.AnyAsync(
            b => b.CompanyNo == companyNo && b.Barcode == barcode && b.IsDeleted == Deleted
                 && (excludeNo == null || b.BarcodeNo != excludeNo), ct);

    /// <summary>Only one barcode per product/variant may be primary — the one a label print defaults to.</summary>
    private async Task DemoteOtherPrimariesAsync(InvProductBarcode keep, CancellationToken ct)
    {
        var others = await _db.InvProductBarcodes
            .Where(b => b.ProductNo == keep.ProductNo && b.BarcodeNo != keep.BarcodeNo
                        && b.VariantNo == keep.VariantNo && b.IsPrimary == 1 && b.IsDeleted == Deleted)
            .ToListAsync(ct);

        if (others.Count == 0) return;

        foreach (var b in others) b.IsPrimary = 0;
        await _db.SaveChangesAsync(ct);
    }

    private static string GenerateEan13()
    {
        Span<char> digits = stackalloc char[13];
        digits[0] = '2';
        digits[1] = '0';
        for (int i = 2; i < 12; i++) digits[i] = (char)('0' + RandomNumberGenerator.GetInt32(10));

        int sum = 0;
        for (int i = 0; i < 12; i++)
        {
            int d = digits[i] - '0';
            sum += i % 2 == 0 ? d : d * 3;
        }
        digits[12] = (char)('0' + (10 - sum % 10) % 10);

        return new string(digits);
    }

    private async Task<InvProduct> RequireProductAsync(long productNo, CancellationToken ct)
    {
        if (productNo <= 0) throw new ValidationException("Product is required");

        var p = await _db.InvProducts.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.ProductNo == productNo && x.IsDeleted == Deleted, ct)
                ?? throw new NotFoundException($"Product not found: {productNo}");

        if (p.CompanyNo != Company()) throw new ValidationException("Product belongs to another company");
        return p;
    }

    private static Inv1002BarcodeDto ToDto(InvProductBarcode b, string? productName,
                                           Dictionary<long, string> uomNames) => new()
    {
        BarcodeNo = b.BarcodeNo,
        ProductNo = b.ProductNo,
        ProductName = productName,
        VariantNo = b.VariantNo,
        Barcode = b.Barcode,
        UomNo = b.UomNo,
        UomName = uomNames.GetValueOrDefault(b.UomNo),
        PackQty = b.PackQty,
        BarcodeType = b.BarcodeType,
        IsPrimary = b.IsPrimary,
        IsActive = b.IsActive,
        RowVersion = b.RowVersion
    };

    private long Company() =>
        _ctx.CurrentCompanyNo() ?? throw new ValidationException("No active company in context");
}
