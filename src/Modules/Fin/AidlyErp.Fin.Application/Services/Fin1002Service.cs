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
// Fin1002Service — Account Group Setup (Operating on unified FinAccounts)
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
        var parentNames = await _db.FinAccounts
            .AsNoTracking()
            .Where(g => g.CompanyNo == companyNo && g.IsGroup == 1 && g.IsDeleted == 0)
            .ToDictionaryAsync(g => g.AccountNo, g => g.AccountName, ct);

        var rows = await _db.FinAccounts
            .AsNoTracking()
            .Where(g => g.CompanyNo == companyNo && g.IsGroup == 1 && g.IsDeleted == 0)
            .OrderBy(g => g.RootType).ThenBy(g => g.DisplayOrder).ThenBy(g => g.AccountCode)
            .ToListAsync(ct);

        return rows.Select(g => ToDto(g, g.ParentAccountNo.HasValue ? parentNames.GetValueOrDefault(g.ParentAccountNo.Value) : null)).ToList();
    }

    public async Task<Fin1002AccountGroupDto> GetDetailAsync(long accountGroupNo, CancellationToken ct = default)
    {
        var grp = await _db.FinAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.AccountNo == accountGroupNo && g.IsGroup == 1 && g.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Account group not found: {accountGroupNo}");

        string? parentName = grp.ParentAccountNo.HasValue
            ? await _db.FinAccounts.Where(p => p.AccountNo == grp.ParentAccountNo.Value).Select(p => p.AccountName).FirstOrDefaultAsync(ct)
            : null;

        return ToDto(grp, parentName);
    }

    public async Task<Fin1002AccountGroupDto> SaveAsync(Fin1002AccountGroupDto dto, CancellationToken ct = default)
    {
        if (dto.AccountGroupNo.HasValue && dto.AccountGroupNo.Value > 0)
        {
            long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
            var grp = await _db.FinAccounts.FirstOrDefaultAsync(g => g.AccountNo == dto.AccountGroupNo.Value && g.CompanyNo == companyNo && g.IsDeleted == 0, ct)
                ?? throw new NotFoundException($"Account group not found: {dto.AccountGroupNo}");

            // RootType validation (if provided)
            if (dto.RootType.HasValue)
            {
                if (dto.RootType.Value < 1 || dto.RootType.Value > 5)
                    throw new ValidationException("Root type must be 1=Asset 2=Liability 3=Equity 4=Income 5=Expense");

                // RootType change guard: prevent if accounts are mapped
                if (dto.RootType.Value != grp.RootType &&
                    await _db.FinAccounts.AnyAsync(a => a.ParentAccountNo == grp.AccountNo && a.IsDeleted == 0, ct))
                    throw new ValidationException("Root type cannot change while child accounts are attached to this group");

                grp.RootType = dto.RootType.Value;
            }

            // NormalBalance change guard (if provided)
            if (dto.NormalBalance != null)
            {
                if (dto.NormalBalance != grp.NormalBalance &&
                    await _db.FinAccounts.AnyAsync(a => a.ParentAccountNo == grp.AccountNo && a.IsDeleted == 0, ct))
                    throw new ValidationException("Normal balance cannot change while child accounts are attached to this group");

                grp.NormalBalance = dto.NormalBalance;
            }

            // Parent group validation (if provided)
            if (dto.ParentGroupNo.HasValue)
            {
                if (dto.ParentGroupNo.Value == grp.AccountNo)
                    throw new ValidationException("A group cannot be its own parent");

                if (dto.ParentGroupNo.Value > 0 && dto.ParentGroupNo != grp.ParentAccountNo)
                {
                    var parent = await _db.FinAccounts.FirstOrDefaultAsync(g => g.AccountNo == dto.ParentGroupNo.Value && g.IsDeleted == 0, ct);
                    if (parent != null && parent.CompanyNo != companyNo)
                        throw new ValidationException("Parent group belongs to another company");
                }
                grp.ParentAccountNo = dto.ParentGroupNo.Value > 0 ? dto.ParentGroupNo.Value : null;
            }

            if (dto.GroupName != null) grp.AccountName = dto.GroupName.Trim();
            if (dto.DisplayOrder.HasValue) grp.DisplayOrder = dto.DisplayOrder.Value;
            if (dto.IsControl.HasValue) grp.IsControl = dto.IsControl.Value;
            if (dto.IsActive.HasValue) grp.IsActive = dto.IsActive.Value;
            grp.IsGroup = 1;
            grp.IsPostable = 0;
            grp.UpdatedBy = _ctx.CurrentUserNo(); grp.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            string? parentName = grp.ParentAccountNo.HasValue
                ? await _db.FinAccounts.Where(p => p.AccountNo == grp.ParentAccountNo.Value).Select(p => p.AccountName).FirstOrDefaultAsync(ct)
                : null;
            return ToDto(grp, parentName);
        }
        else
        {
            long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
            if (string.IsNullOrWhiteSpace(dto.GroupCode) && string.IsNullOrWhiteSpace(dto.GroupName))
                throw new ValidationException("Group ID is required");
            if (string.IsNullOrWhiteSpace(dto.GroupName)) throw new ValidationException("Group name is required");

            short rootType = dto.RootType ?? 1;
            if (rootType < 1 || rootType > 5)
                throw new ValidationException("Root type must be 1=Asset 2=Liability 3=Equity 4=Income 5=Expense");

            // Parent group validation
            if (dto.ParentGroupNo.HasValue && dto.ParentGroupNo.Value > 0)
            {
                var parent = await _db.FinAccounts.FirstOrDefaultAsync(g => g.AccountNo == dto.ParentGroupNo.Value && g.IsDeleted == 0, ct)
                    ?? throw new NotFoundException($"Parent group not found: {dto.ParentGroupNo}");
                if (parent.CompanyNo != companyNo)
                    throw new ValidationException("Parent group belongs to another company");
            }

            string code = string.IsNullOrWhiteSpace(dto.GroupCode) ? await NextGroupCodeAsync(companyNo, ct) : dto.GroupCode.Trim().ToUpperInvariant();

            if (await _db.FinAccounts.AnyAsync(g => g.AccountCode == code && g.CompanyNo == companyNo && g.IsDeleted == 0, ct))
                throw new ValidationException($"Account group ID already exists: {code}");

            var entity = new FinAccount
            {
                CompanyNo = companyNo,
                AccountCode = code,
                AccountName = dto.GroupName.Trim(),
                ParentAccountNo = dto.ParentGroupNo.HasValue && dto.ParentGroupNo.Value > 0 ? dto.ParentGroupNo.Value : null,
                RootType = rootType,
                NormalBalance = dto.NormalBalance ?? (rootType is 1 or 5 ? "dr" : "cr"),
                DisplayOrder = dto.DisplayOrder ?? 0,
                IsControl = dto.IsControl ?? 0,
                IsGroup = 1,
                IsPostable = 0,
                IsActive = dto.IsActive ?? 1,
                IsDeleted = 0,
                CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow
            };

            _db.FinAccounts.Add(entity);
            await _db.SaveChangesAsync(ct);
            return ToDto(entity, null);
        }
    }

    public async Task DeleteAsync(long accountGroupNo, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
        var grp = await _db.FinAccounts.FirstOrDefaultAsync(g => g.AccountNo == accountGroupNo && g.CompanyNo == companyNo && g.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Account group not found: {accountGroupNo}");

        // Child node protection
        if (await _db.FinAccounts.AnyAsync(g => g.ParentAccountNo == accountGroupNo && g.CompanyNo == companyNo && g.IsDeleted == 0, ct))
            throw new ValidationException("Cannot delete: this group has child groups or accounts");

        grp.IsDeleted = 1; grp.IsActive = 0;
        grp.DeletedBy = _ctx.CurrentUserNo(); grp.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private async Task<string> NextGroupCodeAsync(long companyNo, CancellationToken ct)
    {
        long count = await _db.FinAccounts.CountAsync(g => g.CompanyNo == companyNo && g.IsGroup == 1, ct) + 1;
        string code;
        do { code = $"GRP{count++:D3}"; }
        while (await _db.FinAccounts.AnyAsync(g => g.AccountCode == code && g.CompanyNo == companyNo && g.IsDeleted == 0, ct));
        return code;
    }

    private static Fin1002AccountGroupDto ToDto(FinAccount g, string? parentName) => new()
    {
        AccountGroupNo = g.AccountNo,
        GroupCode = g.AccountCode,
        GroupName = g.AccountName,
        ParentGroupNo = g.ParentAccountNo,
        RootType = g.RootType,
        NormalBalance = g.NormalBalance,
        DisplayOrder = g.DisplayOrder,
        IsControl = g.IsControl,
        IsActive = g.IsActive ?? 0,
        RowVersion = g.RowVersion,
        ParentGroupName = parentName
    };
}
