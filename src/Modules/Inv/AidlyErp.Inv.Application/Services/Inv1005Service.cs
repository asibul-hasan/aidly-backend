using AidlyErp.Inv.Application.Dto;
using AidlyErp.Inv.Application.Interfaces;
using AidlyErp.Inv.Domain;
using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Security;
using Microsoft.EntityFrameworkCore;

namespace AidlyErp.Inv.Application.Services;

public interface IInv1005Service
{
    Task<List<Inv1005UomDto>> GetListAsync(CancellationToken ct = default);
    Task<Inv1005UomDto> SaveAsync(Inv1005UomDto dto, CancellationToken ct = default);
    Task DeleteAsync(long uomNo, CancellationToken ct = default);
}

/// <summary>
/// INV_1005 Unit of Measure. <c>decimal_places</c> is what decides whether a quantity may be
/// fractional, so it is validated rather than accepted blindly.
/// </summary>
public class Inv1005Service : IInv1005Service
{
    private const short Deleted = 0;

    private readonly IInvDbContext _db;
    private readonly ICompanyBranchContext _ctx;

    public Inv1005Service(IInvDbContext db, ICompanyBranchContext ctx)
    {
        _db = db;
        _ctx = ctx;
    }

    public async Task<List<Inv1005UomDto>> GetListAsync(CancellationToken ct = default)
    {
        long companyNo = Company();

        var rows = await _db.InvUoms.AsNoTracking()
            .Where(u => u.CompanyNo == companyNo && u.IsDeleted == Deleted)
            .OrderBy(u => u.UomNo)
            .ToListAsync(ct);

        return rows.Select(ToDto).ToList();
    }

    public async Task<Inv1005UomDto> SaveAsync(Inv1005UomDto dto, CancellationToken ct = default)
    {
        long companyNo = Company();

        if (string.IsNullOrWhiteSpace(dto.UomId)) throw new ValidationException("UOM code is required");
        if (string.IsNullOrWhiteSpace(dto.UomName)) throw new ValidationException("UOM name is required");
        if (dto.UomType is null or < 1 or > 4)
            throw new ValidationException("UOM type must be 1=Count, 2=Weight, 3=Volume or 4=Length");
        if (dto.DecimalPlaces is < 0 or > 4)
            throw new ValidationException("Decimal places must be between 0 and 4");

        InvUom e;
        if (dto.UomNo is > 0)
        {
            e = await _db.InvUoms.FirstOrDefaultAsync(u => u.UomNo == dto.UomNo.Value && u.IsDeleted == Deleted, ct)
                ?? throw new NotFoundException("UOM not found");

            if (e.CompanyNo != companyNo) throw new ValidationException("UOM belongs to another company");

            if (!string.Equals(e.UomId, dto.UomId, StringComparison.OrdinalIgnoreCase)
                && await CodeTakenAsync(companyNo, dto.UomId!, e.UomNo, ct))
            {
                throw new ValidationException($"UOM code already exists: {dto.UomId}");
            }
        }
        else
        {
            if (await CodeTakenAsync(companyNo, dto.UomId!, null, ct))
                throw new ValidationException($"UOM code already exists: {dto.UomId}");

            e = new InvUom
            {
                CompanyNo = companyNo,
                CreatedBy = _ctx.CurrentUserNo(),
                CreatedAt = DateTime.UtcNow,
                IsDeleted = 0
            };
            _db.InvUoms.Add(e);
        }

        e.UomId = dto.UomId!;
        e.UomName = dto.UomName!;
        e.UomNameNls = dto.UomNameNls;
        e.UomType = dto.UomType!.Value;
        e.DecimalPlaces = dto.DecimalPlaces ?? 0;
        e.Remarks = dto.Remarks;
        e.IsActive = dto.IsActive ?? 1;
        e.UpdatedBy = _ctx.CurrentUserNo();
        e.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return ToDto(e);
    }

    public async Task DeleteAsync(long uomNo, CancellationToken ct = default)
    {
        long companyNo = Company();

        var e = await _db.InvUoms.FirstOrDefaultAsync(u => u.UomNo == uomNo && u.IsDeleted == Deleted, ct)
                ?? throw new NotFoundException("UOM not found");

        if (e.CompanyNo != companyNo) throw new ValidationException("UOM belongs to another company");

        // Deleting a base UOM would leave products with quantities in no unit at all.
        bool isBaseUom = await _db.InvProducts.AnyAsync(p => p.BaseUomNo == uomNo && p.IsDeleted == Deleted, ct);
        if (isBaseUom) throw new ValidationException("Products use this UOM as their base — reassign them first");

        bool inConversion = await _db.InvUomConversions.AnyAsync(c => c.FromUomNo == uomNo && c.IsDeleted == Deleted, ct);
        if (inConversion) throw new ValidationException("UOM conversions reference this UOM — remove them first");

        e.PerformSoftDelete(_ctx.CurrentUserNo());
        await _db.SaveChangesAsync(ct);
    }

    private async Task<bool> CodeTakenAsync(long companyNo, string uomId, long? excludeNo, CancellationToken ct) =>
        await _db.InvUoms.AnyAsync(
            u => u.CompanyNo == companyNo && u.UomId == uomId && u.IsDeleted == Deleted
                 && (excludeNo == null || u.UomNo != excludeNo), ct);

    private static Inv1005UomDto ToDto(InvUom e) => new()
    {
        UomNo = e.UomNo,
        UomId = e.UomId,
        UomName = e.UomName,
        UomNameNls = e.UomNameNls,
        UomType = e.UomType,
        DecimalPlaces = e.DecimalPlaces,
        Remarks = e.Remarks,
        IsActive = e.IsActive,
        RowVersion = e.RowVersion
    };

    private long Company() =>
        _ctx.CurrentCompanyNo() ?? throw new ValidationException("No active company in context");
}
