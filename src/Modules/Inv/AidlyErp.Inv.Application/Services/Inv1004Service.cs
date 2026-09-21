using AidlyErp.Inv.Application.Dto;
using AidlyErp.Inv.Application.Interfaces;
using AidlyErp.Inv.Domain;
using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Security;
using Microsoft.EntityFrameworkCore;

namespace AidlyErp.Inv.Application.Services;

public interface IInv1004Service
{
    Task<List<Inv1004BrandDto>> GetListAsync(CancellationToken ct = default);
    Task<Inv1004BrandDto> SaveAsync(Inv1004BrandDto dto, CancellationToken ct = default);
    Task DeleteAsync(long brandNo, CancellationToken ct = default);
}

/// <summary>INV_1004 Brand — flat company-scoped master.</summary>
public class Inv1004Service : IInv1004Service
{
    private const short Deleted = 0;

    private readonly IInvDbContext _db;
    private readonly ICompanyBranchContext _ctx;

    public Inv1004Service(IInvDbContext db, ICompanyBranchContext ctx)
    {
        _db = db;
        _ctx = ctx;
    }

    public async Task<List<Inv1004BrandDto>> GetListAsync(CancellationToken ct = default)
    {
        long companyNo = Company();

        var rows = await _db.InvBrands.AsNoTracking()
            .Where(b => b.CompanyNo == companyNo && b.IsDeleted == Deleted)
            .OrderBy(b => b.BrandNo)
            .ToListAsync(ct);

        return rows.Select(ToDto).ToList();
    }

    public async Task<Inv1004BrandDto> SaveAsync(Inv1004BrandDto dto, CancellationToken ct = default)
    {
        long companyNo = Company();

        if (string.IsNullOrWhiteSpace(dto.BrandId)) throw new ValidationException("Brand code is required");
        if (string.IsNullOrWhiteSpace(dto.BrandName)) throw new ValidationException("Brand name is required");

        InvBrand e;
        if (dto.BrandNo is > 0)
        {
            e = await _db.InvBrands.FirstOrDefaultAsync(b => b.BrandNo == dto.BrandNo.Value && b.IsDeleted == Deleted, ct)
                ?? throw new NotFoundException("Brand not found");

            if (e.CompanyNo != companyNo) throw new ValidationException("Brand belongs to another company");

            if (!string.Equals(e.BrandId, dto.BrandId, StringComparison.OrdinalIgnoreCase)
                && await CodeTakenAsync(companyNo, dto.BrandId!, e.BrandNo, ct))
            {
                throw new ValidationException($"Brand code already exists: {dto.BrandId}");
            }
        }
        else
        {
            if (await CodeTakenAsync(companyNo, dto.BrandId!, null, ct))
                throw new ValidationException($"Brand code already exists: {dto.BrandId}");

            e = new InvBrand
            {
                CompanyNo = companyNo,
                CreatedBy = _ctx.CurrentUserNo(),
                CreatedAt = DateTime.UtcNow,
                IsDeleted = 0
            };
            _db.InvBrands.Add(e);
        }

        e.BrandId = dto.BrandId!;
        e.BrandName = dto.BrandName!;
        e.BrandNameNls = dto.BrandNameNls;
        e.Manufacturer = dto.Manufacturer;
        e.ImagePath = dto.ImagePath;
        e.Remarks = dto.Remarks;
        e.IsActive = dto.IsActive ?? 1;
        e.UpdatedBy = _ctx.CurrentUserNo();
        e.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return ToDto(e);
    }

    public async Task DeleteAsync(long brandNo, CancellationToken ct = default)
    {
        long companyNo = Company();

        var e = await _db.InvBrands.FirstOrDefaultAsync(b => b.BrandNo == brandNo && b.IsDeleted == Deleted, ct)
                ?? throw new NotFoundException("Brand not found");

        if (e.CompanyNo != companyNo) throw new ValidationException("Brand belongs to another company");

        bool inUse = await _db.InvProducts.AnyAsync(p => p.BrandNo == brandNo && p.IsDeleted == Deleted, ct);
        if (inUse) throw new ValidationException("Products are assigned to this brand — reassign them first");

        e.PerformSoftDelete(_ctx.CurrentUserNo());
        await _db.SaveChangesAsync(ct);
    }

    private async Task<bool> CodeTakenAsync(long companyNo, string brandId, long? excludeNo, CancellationToken ct) =>
        await _db.InvBrands.AnyAsync(
            b => b.CompanyNo == companyNo && b.BrandId == brandId && b.IsDeleted == Deleted
                 && (excludeNo == null || b.BrandNo != excludeNo), ct);

    private static Inv1004BrandDto ToDto(InvBrand e) => new()
    {
        BrandNo = e.BrandNo,
        BrandId = e.BrandId,
        BrandName = e.BrandName,
        BrandNameNls = e.BrandNameNls,
        Manufacturer = e.Manufacturer,
        ImagePath = e.ImagePath,
        Remarks = e.Remarks,
        IsActive = e.IsActive,
        RowVersion = e.RowVersion
    };

    private long Company() =>
        _ctx.CurrentCompanyNo() ?? throw new ValidationException("No active company in context");
}
