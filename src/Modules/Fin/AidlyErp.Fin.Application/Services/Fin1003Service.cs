using AidlyErp.Fin.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Abstractions;
using AidlyErp.Shared.Contracts;
using AidlyErp.Sys.Contracts;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Fin.Application.Dto;
using AidlyErp.Fin.Domain;

namespace AidlyErp.Fin.Application.Services;

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
    private readonly IFinDbContext _db;
    private readonly ICompanyBranchContext _ctx;

    public Fin1003Service(IFinDbContext db, ICompanyBranchContext ctx)
    {
        _db = db;
        _ctx = ctx;
    }

    public async Task<List<Fin1003VoucherTypeDto>> GetListAsync(CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? 0;
        var rows = await _db.FinVoucherTypes
            .AsNoTracking()
            .Where(t => t.CompanyNo == companyNo && t.IsDeleted == 0)
            .OrderBy(t => t.VoucherTypeNo)
            .ToListAsync(ct);
        return rows.Select(t => ToDto(t)).ToList();
    }

    public async Task<Fin1003VoucherTypeDto> GetDetailAsync(long typeNo, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? 0;
        var type = await _db.FinVoucherTypes.AsNoTracking().FirstOrDefaultAsync(t => t.VoucherTypeNo == typeNo && t.CompanyNo == companyNo && t.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Voucher type not found: {typeNo}");
        return ToDto(type);
    }

    public async Task<Fin1003VoucherTypeDto> SaveAsync(Fin1003VoucherTypeDto dto, CancellationToken ct = default)
    {
        if (dto.VoucherTypeNo.HasValue && dto.VoucherTypeNo.Value > 0)
        {
            long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
            var type = await _db.FinVoucherTypes.FirstOrDefaultAsync(t => t.VoucherTypeNo == dto.VoucherTypeNo.Value && t.CompanyNo == companyNo && t.IsDeleted == 0, ct)
                ?? throw new NotFoundException($"Voucher type not found: {dto.VoucherTypeNo}");

            if (dto.BaseKind < 1 || dto.BaseKind > 8)
                throw new ValidationException("base_kind must be 1..8 (Journal/Payment/Receipt/Contra/Sales/Purchase/Opening/Closing)");

            if (dto.VoucherTypeName != null) type.VoucherTypeName = dto.VoucherTypeName.Trim();
            type.BaseKind = dto.BaseKind;
            type.Prefix = dto.Prefix;
            type.OrderSl = dto.OrderSl;
            // IsSystem is immutable after creation — not updated from DTO
            type.IsActive = dto.IsActive;
            type.UpdatedBy = _ctx.CurrentUserNo(); type.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return ToDto(type);
        }
        else
        {
            long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
            if (string.IsNullOrWhiteSpace(dto.VoucherTypeCode) && string.IsNullOrWhiteSpace(dto.Prefix))
                throw new ValidationException("Voucher type ID is required");
            if (string.IsNullOrWhiteSpace(dto.VoucherTypeName)) throw new ValidationException("Type name is required");

            if (dto.BaseKind < 1 || dto.BaseKind > 8)
                throw new ValidationException("base_kind must be 1..8 (Journal/Payment/Receipt/Contra/Sales/Purchase/Opening/Closing)");

            string code = string.IsNullOrWhiteSpace(dto.VoucherTypeCode) ? (dto.Prefix ?? "VCH").Trim().ToUpperInvariant() : dto.VoucherTypeCode.Trim().ToUpperInvariant();

            if (await _db.FinVoucherTypes.AnyAsync(t => t.VoucherTypeCode == code && t.CompanyNo == companyNo && t.IsDeleted == 0, ct))
                throw new ValidationException($"Voucher type ID already exists: {code}");

            var type = new FinVoucherType
            {
                CompanyNo = companyNo,
                VoucherTypeCode = code,
                VoucherTypeName = dto.VoucherTypeName.Trim(),
                BaseKind = dto.BaseKind,
                Prefix = dto.Prefix,
                DefaultDrAccountNo = dto.DefaultDrAccountNo,
                DefaultCrAccountNo = dto.DefaultCrAccountNo,
                IsSystem = dto.IsSystem,
                OrderSl = dto.OrderSl,
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
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
        var type = await _db.FinVoucherTypes.FirstOrDefaultAsync(t => t.VoucherTypeNo == typeNo && t.CompanyNo == companyNo && t.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Voucher type not found: {typeNo}");

        // System voucher types cannot be deleted
        if (type.IsSystem == 1)
            throw new ValidationException("System voucher types cannot be deleted");

        if (await _db.FinVouchers.AnyAsync(v => v.VoucherTypeNo == typeNo && v.IsDeleted == 0, ct))
            throw new ValidationException("Cannot delete: vouchers already use this voucher type");

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
        DefaultDrAccountNo = t.DefaultDrAccountNo,
        DefaultCrAccountNo = t.DefaultCrAccountNo,
        IsSystem = t.IsSystem,
        OrderSl = t.OrderSl,
        RequiresApproval = t.RequiresApproval,
        IsAutoNumbered = t.IsAutoNumbered,
        IsActive = t.IsActive ?? 0,
        RowVersion = t.RowVersion
    };
}
