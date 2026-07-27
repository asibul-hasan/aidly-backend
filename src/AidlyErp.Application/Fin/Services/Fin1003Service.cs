using Microsoft.EntityFrameworkCore;
using AidlyErp.Application.Common.Exceptions;
using AidlyErp.Application.Common.Interfaces;
using AidlyErp.Application.Common.Security;
using AidlyErp.Application.Fin.Dto;
using AidlyErp.Domain.Fin;

namespace AidlyErp.Application.Fin.Services;

// ═══════════════════════════════════════════════════════════════════════════
// Fin1003Service — Voucher Type Setup
// ═══════════════════════════════════════════════════════════════════════════

public interface IFin1003Service
{
    Task<List<Fin1003VoucherTypeDto>> GetListAsync(CancellationToken ct = default);
    Task<Fin1003VoucherTypeDto> GetDetailAsync(long typeNo, CancellationToken ct = default);
    Task<Fin1003VoucherTypeDto> SaveAsync(Fin1003VoucherTypeDto dto, CancellationToken ct = default);
    Task DeleteAsync(long typeNo, CancellationToken ct = default);
}

public class Fin1003Service : IFin1003Service
{
    private readonly IApplicationDbContext _db;
    private readonly ICompanyBranchContext _ctx;

    public Fin1003Service(IApplicationDbContext db, ICompanyBranchContext ctx)
    {
        _db = db;
        _ctx = ctx;
    }

    public async Task<List<Fin1003VoucherTypeDto>> GetListAsync(CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? 0;
        return await _db.FinVoucherTypes
            .AsNoTracking()
            .Where(t => t.CompanyNo == companyNo && t.IsDeleted == 0)
            .OrderBy(t => t.VoucherTypeNo)
            .Select(t => ToDto(t))
            .ToListAsync(ct);
    }

    public async Task<Fin1003VoucherTypeDto> GetDetailAsync(long typeNo, CancellationToken ct = default)
    {
        var type = await _db.FinVoucherTypes.AsNoTracking().FirstOrDefaultAsync(t => t.VoucherTypeNo == typeNo && t.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Voucher type not found: {typeNo}");
        return ToDto(type);
    }

    public async Task<Fin1003VoucherTypeDto> SaveAsync(Fin1003VoucherTypeDto dto, CancellationToken ct = default)
    {
        if (dto.VoucherTypeNo.HasValue && dto.VoucherTypeNo.Value > 0)
        {
            var type = await _db.FinVoucherTypes.FirstOrDefaultAsync(t => t.VoucherTypeNo == dto.VoucherTypeNo.Value && t.IsDeleted == 0, ct)
                ?? throw new NotFoundException($"Voucher type not found: {dto.VoucherTypeNo}");
            if (dto.VoucherTypeName != null) type.VoucherTypeName = dto.VoucherTypeName.Trim();
            type.BaseKind = dto.BaseKind;
            type.Prefix = dto.Prefix;
            type.RequiresApproval = dto.RequiresApproval;
            type.IsAutoNumbered = dto.IsAutoNumbered;
            type.IsActive = dto.IsActive;
            type.UpdatedBy = _ctx.CurrentUserNo(); type.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return ToDto(type);
        }
        else
        {
            long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
            if (string.IsNullOrWhiteSpace(dto.VoucherTypeName)) throw new ValidationException("Voucher type name is required");
            string code = string.IsNullOrWhiteSpace(dto.VoucherTypeCode) ? dto.Prefix ?? "VCH" : dto.VoucherTypeCode.Trim().ToUpperInvariant();

            if (await _db.FinVoucherTypes.AnyAsync(t => t.VoucherTypeCode == code && t.CompanyNo == companyNo && t.IsDeleted == 0, ct))
                throw new ValidationException($"Voucher type code already exists: {code}");

            var type = new FinVoucherType
            {
                CompanyNo = companyNo,
                VoucherTypeCode = code,
                VoucherTypeName = dto.VoucherTypeName.Trim(),
                BaseKind = dto.BaseKind,
                Prefix = dto.Prefix,
                RequiresApproval = dto.RequiresApproval,
                IsAutoNumbered = dto.IsAutoNumbered,
                IsActive = dto.IsActive,
                IsDeleted = 0,
                CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow
            };

            _db.FinVoucherTypes.Add(type);
            await _db.SaveChangesAsync(ct);
            return ToDto(type);
        }
    }

    public async Task DeleteAsync(long typeNo, CancellationToken ct = default)
    {
        var type = await _db.FinVoucherTypes.FirstOrDefaultAsync(t => t.VoucherTypeNo == typeNo && t.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Voucher type not found: {typeNo}");

        if (await _db.FinVouchers.AnyAsync(v => v.VoucherTypeNo == typeNo && v.IsDeleted == 0, ct))
            throw new ValidationException("Cannot delete voucher type with existing vouchers");

        type.IsDeleted = 1; type.IsActive = 0;
        type.DeletedBy = _ctx.CurrentUserNo(); type.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private static Fin1003VoucherTypeDto ToDto(FinVoucherType t) => new()
    {
        VoucherTypeNo = t.VoucherTypeNo,
        VoucherTypeCode = t.VoucherTypeCode,
        VoucherTypeName = t.VoucherTypeName,
        BaseKind = t.BaseKind,
        Prefix = t.Prefix,
        RequiresApproval = t.RequiresApproval,
        IsAutoNumbered = t.IsAutoNumbered,
        IsActive = t.IsActive,
        RowVersion = t.RowVersion
    };
}
