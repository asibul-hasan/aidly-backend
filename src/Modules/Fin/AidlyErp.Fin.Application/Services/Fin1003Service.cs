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
    Task<Fin1003LookupsDto> GetLookupsAsync(CancellationToken ct = default);
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

    public async Task<Fin1003LookupsDto> GetLookupsAsync(CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? 0;
        var accounts = await _db.FinAccounts
            .AsNoTracking()
            .Where(a => a.CompanyNo == companyNo && a.IsPostable == 1 && a.IsActive == 1 && a.IsDeleted == 0)
            .OrderBy(a => a.AccountCode)
            .Select(a => new Fin1003AccountLookupDto
            {
                AccountNo = a.AccountNo,
                AccountCode = a.AccountCode,
                AccountName = a.AccountName,
                RootType = a.RootType,
                ControlType = a.ControlType
            })
            .ToListAsync(ct);

        return new Fin1003LookupsDto { Accounts = accounts };
    }

    public async Task<List<Fin1003VoucherTypeDto>> GetListAsync(CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? 0;
        var rows = await _db.FinVoucherTypes
            .AsNoTracking()
            .Where(t => t.CompanyNo == companyNo && t.IsDeleted == 0)
            .OrderBy(t => t.OrderSl)
            .ThenBy(t => t.VoucherTypeNo)
            .ToListAsync(ct);

        var accountIds = rows
            .SelectMany(r => new[] { r.DefaultDrAccountNo, r.DefaultCrAccountNo })
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var accountNames = accountIds.Count > 0
            ? await _db.FinAccounts
                .AsNoTracking()
                .Where(a => accountIds.Contains(a.AccountNo))
                .ToDictionaryAsync(a => a.AccountNo, a => $"{a.AccountCode} - {a.AccountName}", ct)
            : new Dictionary<long, string>();

        return rows.Select(t => ToDto(t, accountNames)).ToList();
    }

    public async Task<Fin1003VoucherTypeDto> GetDetailAsync(long typeNo, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? 0;
        var type = await _db.FinVoucherTypes.AsNoTracking().FirstOrDefaultAsync(t => t.VoucherTypeNo == typeNo && t.CompanyNo == companyNo && t.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Voucher type not found: {typeNo}");

        var accountNames = new Dictionary<long, string>();
        var accountIds = new[] { type.DefaultDrAccountNo, type.DefaultCrAccountNo }.Where(id => id.HasValue).Select(id => id!.Value).ToList();
        if (accountIds.Count > 0)
        {
            accountNames = await _db.FinAccounts
                .AsNoTracking()
                .Where(a => accountIds.Contains(a.AccountNo))
                .ToDictionaryAsync(a => a.AccountNo, a => $"{a.AccountCode} - {a.AccountName}", ct);
        }

        return ToDto(type, accountNames);
    }

    public async Task<Fin1003VoucherTypeDto> SaveAsync(Fin1003VoucherTypeDto dto, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");

        if (dto.VoucherTypeNo.HasValue && dto.VoucherTypeNo.Value > 0)
        {
            var type = await _db.FinVoucherTypes.FirstOrDefaultAsync(t => t.VoucherTypeNo == dto.VoucherTypeNo.Value && t.CompanyNo == companyNo && t.IsDeleted == 0, ct)
                ?? throw new NotFoundException($"Voucher type not found: {dto.VoucherTypeNo}");

            // 1. Base Kind (if provided)
            if (dto.BaseKind.HasValue)
            {
                if (dto.BaseKind.Value < 1 || dto.BaseKind.Value > 8)
                    throw new ValidationException("base_kind must be 1..8 (Journal/Payment/Receipt/Contra/Sales/Purchase/Opening/Closing)");
                type.BaseKind = dto.BaseKind.Value;
            }

            // 2. Voucher Type Code / ID (if provided)
            if (!string.IsNullOrWhiteSpace(dto.VoucherTypeCode) || !string.IsNullOrWhiteSpace(dto.VoucherTypeId))
            {
                string code = (dto.VoucherTypeCode ?? dto.VoucherTypeId!).Trim().ToUpperInvariant();
                if (!string.Equals(type.VoucherTypeCode, code, StringComparison.OrdinalIgnoreCase))
                {
                    if (type.IsSystem == 1)
                        throw new ValidationException("System voucher type code cannot be changed");

                    if (await _db.FinVoucherTypes.AnyAsync(t => t.VoucherTypeCode == code && t.VoucherTypeNo != type.VoucherTypeNo && t.CompanyNo == companyNo && t.IsDeleted == 0, ct))
                        throw new ValidationException($"Voucher type ID already exists: {code}");

                    type.VoucherTypeCode = code;
                }
            }

            // 3. Voucher Type Name (if provided)
            if (dto.VoucherTypeName != null || dto.TypeName != null)
            {
                string name = (dto.VoucherTypeName ?? dto.TypeName ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(name))
                    throw new ValidationException("Type name is required");
                type.VoucherTypeName = name;
            }

            // 4. Prefix (if provided)
            if (dto.Prefix != null || dto.NumberPrefix != null)
            {
                string? prefix = (dto.Prefix ?? dto.NumberPrefix)?.Trim();
                type.Prefix = string.IsNullOrWhiteSpace(prefix) ? type.VoucherTypeCode : prefix;
            }

            // 5. Default Dr Account (if provided: >0 sets account, 0 clears account)
            if (dto.DefaultDrAccountNo.HasValue)
            {
                type.DefaultDrAccountNo = dto.DefaultDrAccountNo.Value > 0 ? dto.DefaultDrAccountNo.Value : null;
            }

            // 6. Default Cr Account (if provided: >0 sets account, 0 clears account)
            if (dto.DefaultCrAccountNo.HasValue)
            {
                type.DefaultCrAccountNo = dto.DefaultCrAccountNo.Value > 0 ? dto.DefaultCrAccountNo.Value : null;
            }

            // 7. Order sequence (if provided)
            if (dto.OrderSl.HasValue)
            {
                type.OrderSl = dto.OrderSl.Value;
            }

            // 8. Approval required (if provided)
            if (dto.RequiresApproval.HasValue)
            {
                type.RequiresApproval = dto.RequiresApproval.Value;
            }

            // 9. Auto-numbered (if provided)
            if (dto.IsAutoNumbered.HasValue)
            {
                type.IsAutoNumbered = dto.IsAutoNumbered.Value;
            }

            // 10. Active status (if provided)
            if (dto.IsActive.HasValue)
            {
                type.IsActive = dto.IsActive.Value;
            }

            type.UpdatedBy = _ctx.CurrentUserNo();
            type.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);
            return await GetDetailAsync(type.VoucherTypeNo, ct);
        }
        else
        {
            if (!dto.BaseKind.HasValue || dto.BaseKind.Value < 1 || dto.BaseKind.Value > 8)
                throw new ValidationException("base_kind must be 1..8 (Journal/Payment/Receipt/Contra/Sales/Purchase/Opening/Closing)");

            string code = (dto.VoucherTypeCode ?? dto.VoucherTypeId ?? dto.Prefix ?? dto.NumberPrefix ?? "VCH").Trim().ToUpperInvariant();
            string name = (dto.VoucherTypeName ?? dto.TypeName ?? string.Empty).Trim();
            string? prefix = (dto.Prefix ?? dto.NumberPrefix)?.Trim();

            if (string.IsNullOrWhiteSpace(code))
                throw new ValidationException("Voucher type ID is required");
            if (string.IsNullOrWhiteSpace(name))
                throw new ValidationException("Type name is required");

            if (await _db.FinVoucherTypes.AnyAsync(t => t.VoucherTypeCode == code && t.CompanyNo == companyNo && t.IsDeleted == 0, ct))
                throw new ValidationException($"Voucher type ID already exists: {code}");

            var type = new FinVoucherType
            {
                CompanyNo = companyNo,
                VoucherTypeCode = code,
                VoucherTypeName = name,
                BaseKind = dto.BaseKind.Value,
                Prefix = string.IsNullOrWhiteSpace(prefix) ? code : prefix,
                DefaultDrAccountNo = dto.DefaultDrAccountNo > 0 ? dto.DefaultDrAccountNo : null,
                DefaultCrAccountNo = dto.DefaultCrAccountNo > 0 ? dto.DefaultCrAccountNo : null,
                IsSystem = dto.IsSystem ?? 0,
                OrderSl = dto.OrderSl ?? 0,
                RequiresApproval = dto.RequiresApproval ?? 0,
                IsAutoNumbered = dto.IsAutoNumbered ?? 1,
                IsActive = dto.IsActive ?? 1,
                IsDeleted = 0,
                CreatedBy = _ctx.CurrentUserNo(),
                CreatedAt = DateTime.UtcNow
            };

            _db.FinVoucherTypes.Add(type);
            await _db.SaveChangesAsync(ct);
            return await GetDetailAsync(type.VoucherTypeNo, ct);
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

        type.IsDeleted = 1;
        type.IsActive = 0;
        type.DeletedBy = _ctx.CurrentUserNo();
        type.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private static Fin1003VoucherTypeDto ToDto(FinVoucherType t, Dictionary<long, string>? accountNames = null) => new()
    {
        VoucherTypeNo = t.VoucherTypeNo,
        VoucherTypeCode = t.VoucherTypeCode,
        VoucherTypeName = t.VoucherTypeName,
        BaseKind = t.BaseKind,
        Prefix = t.Prefix,
        DefaultDrAccountNo = t.DefaultDrAccountNo,
        DefaultDrAccountName = t.DefaultDrAccountNo.HasValue && accountNames != null && accountNames.TryGetValue(t.DefaultDrAccountNo.Value, out var drName) ? drName : null,
        DefaultCrAccountNo = t.DefaultCrAccountNo,
        DefaultCrAccountName = t.DefaultCrAccountNo.HasValue && accountNames != null && accountNames.TryGetValue(t.DefaultCrAccountNo.Value, out var crName) ? crName : null,
        IsSystem = t.IsSystem,
        OrderSl = t.OrderSl,
        RequiresApproval = t.RequiresApproval,
        IsAutoNumbered = t.IsAutoNumbered,
        IsActive = t.IsActive ?? 0,
        RowVersion = t.RowVersion
    };
}
