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
// Fin1002Service — Account Group Setup
// ═══════════════════════════════════════════════════════════════════════════

public interface IFin1002Service
{
    Task<List<Fin1002AccountGroupDto>> GetListAsync(CancellationToken ct = default);
    Task<Fin1002AccountGroupDto> GetDetailAsync(long accountGroupNo, CancellationToken ct = default);
    Task<Fin1002AccountGroupDto> SaveAsync(Fin1002AccountGroupDto dto, CancellationToken ct = default);
    Task DeleteAsync(long accountGroupNo, CancellationToken ct = default);
}

public class Fin1002Service : IFin1002Service
{
    private readonly IFinDbContext _db;
    private readonly ICompanyBranchContext _ctx;

    public Fin1002Service(IFinDbContext db, ICompanyBranchContext ctx)
    {
        _db = db;
        _ctx = ctx;
    }

    public async Task<List<Fin1002AccountGroupDto>> GetListAsync(CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? 0;
        var parentNames = await _db.FinAccountGroups.AsNoTracking().Where(g => g.IsDeleted == 0).ToDictionaryAsync(g => g.AccountGroupNo, g => g.GroupName, ct);

        var rows = await _db.FinAccountGroups
            .AsNoTracking()
            .Where(g => g.CompanyNo == companyNo && g.IsDeleted == 0)
            .OrderBy(g => g.RootType).ThenBy(g => g.DisplayOrder).ThenBy(g => g.GroupCode)
            .ToListAsync(ct);

        return rows.Select(g => ToDto(g, g.ParentGroupNo.HasValue ? parentNames.GetValueOrDefault(g.ParentGroupNo.Value) : null)).ToList();
    }

    public async Task<Fin1002AccountGroupDto> GetDetailAsync(long accountGroupNo, CancellationToken ct = default)
    {
        var grp = await _db.FinAccountGroups.AsNoTracking().FirstOrDefaultAsync(g => g.AccountGroupNo == accountGroupNo && g.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Account group not found: {accountGroupNo}");

        string? parentName = grp.ParentGroupNo.HasValue
            ? await _db.FinAccountGroups.Where(p => p.AccountGroupNo == grp.ParentGroupNo.Value).Select(p => p.GroupName).FirstOrDefaultAsync(ct)
            : null;

        return ToDto(grp, parentName);
    }

    public async Task<Fin1002AccountGroupDto> SaveAsync(Fin1002AccountGroupDto dto, CancellationToken ct = default)
    {
        if (dto.AccountGroupNo.HasValue && dto.AccountGroupNo.Value > 0)
        {
            long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
            var grp = await _db.FinAccountGroups.FirstOrDefaultAsync(g => g.AccountGroupNo == dto.AccountGroupNo.Value && g.CompanyNo == companyNo && g.IsDeleted == 0, ct)
                ?? throw new NotFoundException($"Account group not found: {dto.AccountGroupNo}");

            // RootType validation
            if (dto.RootType < 1 || dto.RootType > 5)
                throw new ValidationException("Root type must be 1=Asset 2=Liability 3=Equity 4=Income 5=Expense");

            // Self-referencing parent guard
            if (dto.ParentGroupNo.HasValue && dto.ParentGroupNo.Value == grp.AccountGroupNo)
                throw new ValidationException("A group cannot be its own parent");

            // RootType change guard: prevent if accounts are mapped
            if (dto.RootType != grp.RootType &&
                await _db.FinAccounts.AnyAsync(a => a.AccountGroupNo == grp.AccountGroupNo && a.IsDeleted == 0, ct))
                throw new ValidationException("Root type cannot change while accounts are mapped to this group");

            // NormalBalance change guard: prevent if accounts are mapped
            if (dto.NormalBalance != null && dto.NormalBalance != grp.NormalBalance &&
                await _db.FinAccounts.AnyAsync(a => a.AccountGroupNo == grp.AccountGroupNo && a.IsDeleted == 0, ct))
                throw new ValidationException("Normal balance cannot change while accounts are mapped to this group");

            // Parent group validation
            if (dto.ParentGroupNo.HasValue && dto.ParentGroupNo != grp.ParentGroupNo)
            {
                var parent = await _db.FinAccountGroups.FirstOrDefaultAsync(g => g.AccountGroupNo == dto.ParentGroupNo.Value && g.IsDeleted == 0, ct);
                if (parent != null && parent.CompanyNo != companyNo)
                    throw new ValidationException("Parent group belongs to another company");
            }

            if (dto.GroupName != null) grp.GroupName = dto.GroupName.Trim();
            if (dto.ParentGroupNo != grp.ParentGroupNo) grp.ParentGroupNo = dto.ParentGroupNo;
            grp.RootType = dto.RootType;
            grp.NormalBalance = dto.NormalBalance ?? (dto.RootType is 1 or 5 ? "dr" : "cr");
            grp.DisplayOrder = dto.DisplayOrder;
            grp.IsActive = dto.IsActive;
            grp.UpdatedBy = _ctx.CurrentUserNo(); grp.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            string? parentName = grp.ParentGroupNo.HasValue
                ? await _db.FinAccountGroups.Where(p => p.AccountGroupNo == grp.ParentGroupNo.Value).Select(p => p.GroupName).FirstOrDefaultAsync(ct)
                : null;
            return ToDto(grp, parentName);
        }
        else
        {
            long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
            if (string.IsNullOrWhiteSpace(dto.GroupCode) && string.IsNullOrWhiteSpace(dto.GroupName))
                throw new ValidationException("Group ID is required");
            if (string.IsNullOrWhiteSpace(dto.GroupName)) throw new ValidationException("Group name is required");

            if (dto.RootType < 1 || dto.RootType > 5)
                throw new ValidationException("Root type must be 1=Asset 2=Liability 3=Equity 4=Income 5=Expense");

            // Parent group validation
            if (dto.ParentGroupNo.HasValue)
            {
                var parent = await _db.FinAccountGroups.FirstOrDefaultAsync(g => g.AccountGroupNo == dto.ParentGroupNo.Value && g.IsDeleted == 0, ct)
                    ?? throw new NotFoundException($"Parent group not found: {dto.ParentGroupNo}");
                if (parent.CompanyNo != companyNo)
                    throw new ValidationException("Parent group belongs to another company");
            }

            string code = string.IsNullOrWhiteSpace(dto.GroupCode) ? await NextGroupCodeAsync(companyNo, ct) : dto.GroupCode.Trim().ToUpperInvariant();

            if (await _db.FinAccountGroups.AnyAsync(g => g.GroupCode == code && g.CompanyNo == companyNo && g.IsDeleted == 0, ct))
                throw new ValidationException($"Account group ID already exists: {code}");

            var entity = new FinAccountGroup
            {
                CompanyNo = companyNo,
                GroupCode = code,
                GroupName = dto.GroupName.Trim(),
                ParentGroupNo = dto.ParentGroupNo,
                RootType = dto.RootType,
                NormalBalance = dto.NormalBalance ?? (dto.RootType is 1 or 5 ? "dr" : "cr"),
                DisplayOrder = dto.DisplayOrder,
                IsActive = dto.IsActive,
                IsDeleted = 0,
                CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow
            };

            _db.FinAccountGroups.Add(entity);
            await _db.SaveChangesAsync(ct);
            return ToDto(entity, null);
        }
    }

    public async Task DeleteAsync(long accountGroupNo, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
        var grp = await _db.FinAccountGroups.FirstOrDefaultAsync(g => g.AccountGroupNo == accountGroupNo && g.CompanyNo == companyNo && g.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Account group not found: {accountGroupNo}");

        // Child group protection
        if (await _db.FinAccountGroups.AnyAsync(g => g.ParentGroupNo == accountGroupNo && g.CompanyNo == companyNo && g.IsDeleted == 0, ct))
            throw new ValidationException("Cannot delete: this group has child groups");

        // Account protection
        if (await _db.FinAccounts.AnyAsync(a => a.AccountGroupNo == accountGroupNo && a.CompanyNo == companyNo && a.IsDeleted == 0, ct))
            throw new ValidationException("Cannot delete: accounts are mapped to this group");

        grp.IsDeleted = 1; grp.IsActive = 0;
        grp.DeletedBy = _ctx.CurrentUserNo(); grp.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private async Task<string> NextGroupCodeAsync(long companyNo, CancellationToken ct)
    {
        long count = await _db.FinAccountGroups.CountAsync(g => g.CompanyNo == companyNo, ct) + 1;
        string code;
        do { code = $"GRP{count++:D3}"; }
        while (await _db.FinAccountGroups.AnyAsync(g => g.GroupCode == code && g.CompanyNo == companyNo && g.IsDeleted == 0, ct));
        return code;
    }

    private static Fin1002AccountGroupDto ToDto(FinAccountGroup g, string? parentName) => new()
    {
        AccountGroupNo = g.AccountGroupNo,
        GroupCode = g.GroupCode,
        GroupName = g.GroupName,
        ParentGroupNo = g.ParentGroupNo,
        RootType = g.RootType,
        NormalBalance = g.NormalBalance,
        DisplayOrder = g.DisplayOrder,
        IsActive = g.IsActive ?? 0,
        RowVersion = g.RowVersion,
        ParentGroupName = parentName
    };
}
