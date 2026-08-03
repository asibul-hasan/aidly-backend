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
// Fin1001Service — Chart of Accounts Master
// ═══════════════════════════════════════════════════════════════════════════

public interface IFin1001Service
{
    Task<List<Fin1001AccountDto>> GetListAsync(CancellationToken ct = default);
    Task<Fin1001AccountDto> GetDetailAsync(long accountNo, CancellationToken ct = default);
    Task<Fin1001AccountDto> SaveAsync(Fin1001AccountDto dto, CancellationToken ct = default);
    Task DeleteAsync(long accountNo, CancellationToken ct = default);
}

public class Fin1001Service : IFin1001Service
{
    private readonly IFinDbContext _db;
    private readonly ICompanyBranchContext _ctx;

    public Fin1001Service(IFinDbContext db, ICompanyBranchContext ctx)
    {
        _db = db;
        _ctx = ctx;
    }

    public async Task<List<Fin1001AccountDto>> GetListAsync(CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? 0;
        var groups = await _db.FinAccountGroups.AsNoTracking().Where(g => g.IsDeleted == 0).ToDictionaryAsync(g => g.AccountGroupNo, g => g.GroupName, ct);

        return await _db.FinAccounts
            .AsNoTracking()
            .Where(a => a.CompanyNo == companyNo && a.IsDeleted == 0)
            .OrderBy(a => a.AccountCode)
            .Select(a => ToDto(a, groups.ContainsKey(a.AccountGroupNo) ? groups[a.AccountGroupNo] : null))
            .ToListAsync(ct);
    }

    public async Task<Fin1001AccountDto> GetDetailAsync(long accountNo, CancellationToken ct = default)
    {
        var acc = await _db.FinAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.AccountNo == accountNo && a.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Account not found: {accountNo}");

        var groupName = await _db.FinAccountGroups
            .Where(g => g.AccountGroupNo == acc.AccountGroupNo && g.IsDeleted == 0)
            .Select(g => g.GroupName)
            .FirstOrDefaultAsync(ct);

        return ToDto(acc, groupName);
    }

    public async Task<Fin1001AccountDto> SaveAsync(Fin1001AccountDto dto, CancellationToken ct = default)
    {
        return dto.AccountNo.HasValue && dto.AccountNo.Value > 0
            ? await UpdateAsync(dto.AccountNo.Value, dto, ct)
            : await InsertAsync(dto, ct);
    }

    private async Task<Fin1001AccountDto> InsertAsync(Fin1001AccountDto dto, CancellationToken ct)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
        if (string.IsNullOrWhiteSpace(dto.AccountName)) throw new ValidationException("Account name is required");

        var group = await _db.FinAccountGroups
            .FirstOrDefaultAsync(g => g.AccountGroupNo == dto.AccountGroupNo && g.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Account group not found: {dto.AccountGroupNo}");

        string code = string.IsNullOrWhiteSpace(dto.AccountCode) ? await NextAccountCodeAsync(companyNo, group.GroupCode, ct) : dto.AccountCode.Trim().ToUpperInvariant();

        if (await _db.FinAccounts.AnyAsync(a => a.AccountCode == code && a.CompanyNo == companyNo && a.IsDeleted == 0, ct))
            throw new ValidationException($"Account code already exists: {code}");

        var acc = new FinAccount
        {
            CompanyNo = companyNo,
            BranchNo = dto.BranchNo ?? _ctx.CurrentBranchNo(),
            AccountCode = code,
            AccountName = dto.AccountName.Trim(),
            AccountGroupNo = dto.AccountGroupNo,
            RootType = group.RootType,
            NormalBalance = !string.IsNullOrWhiteSpace(dto.NormalBalance) ? dto.NormalBalance.ToLowerInvariant() : (group.NormalBalance ?? "dr"),
            IsPostable = dto.IsPostable,
            ControlType = dto.ControlType,
            RequiresCostCenter = dto.RequiresCostCenter,
            RequiresParty = dto.RequiresParty,
            CurrencyNo = dto.CurrencyNo,
            OpeningBalance = dto.OpeningBalance,
            OpeningDrCr = dto.OpeningDrCr ?? "dr",
            IsActive = dto.IsActive,
            IsDeleted = 0,
            CreatedBy = _ctx.CurrentUserNo(),
            CreatedAt = DateTime.UtcNow
        };

        _db.FinAccounts.Add(acc);
        await _db.SaveChangesAsync(ct);

        return ToDto(acc, group.GroupName);
    }

    private async Task<Fin1001AccountDto> UpdateAsync(long accountNo, Fin1001AccountDto dto, CancellationToken ct)
    {
        var acc = await _db.FinAccounts.FirstOrDefaultAsync(a => a.AccountNo == accountNo && a.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Account not found: {accountNo}");

        if (dto.AccountName != null) acc.AccountName = dto.AccountName.Trim();
        if (dto.AccountGroupNo > 0 && dto.AccountGroupNo != acc.AccountGroupNo)
        {
            var grp = await _db.FinAccountGroups.FirstOrDefaultAsync(g => g.AccountGroupNo == dto.AccountGroupNo && g.IsDeleted == 0, ct)
                ?? throw new NotFoundException($"Account group not found: {dto.AccountGroupNo}");
            acc.AccountGroupNo = dto.AccountGroupNo;
            acc.RootType = grp.RootType;
        }
        if (dto.NormalBalance != null) acc.NormalBalance = dto.NormalBalance.ToLowerInvariant();
        acc.IsPostable = dto.IsPostable;
        acc.ControlType = dto.ControlType;
        acc.RequiresCostCenter = dto.RequiresCostCenter;
        acc.RequiresParty = dto.RequiresParty;
        acc.CurrencyNo = dto.CurrencyNo;
        acc.OpeningBalance = dto.OpeningBalance;
        if (dto.OpeningDrCr != null) acc.OpeningDrCr = dto.OpeningDrCr.ToLowerInvariant();
        acc.IsActive = dto.IsActive;
        acc.UpdatedBy = _ctx.CurrentUserNo();
        acc.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        var groupName = await _db.FinAccountGroups.Where(g => g.AccountGroupNo == acc.AccountGroupNo).Select(g => g.GroupName).FirstOrDefaultAsync(ct);
        return ToDto(acc, groupName);
    }

    public async Task DeleteAsync(long accountNo, CancellationToken ct = default)
    {
        var acc = await _db.FinAccounts.FirstOrDefaultAsync(a => a.AccountNo == accountNo && a.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Account not found: {accountNo}");

        if (await _db.FinVoucherDtls.AnyAsync(d => d.AccountNo == accountNo && d.IsDeleted == 0, ct))
            throw new ValidationException("Cannot delete account with existing transactions");

        acc.IsDeleted = 1;
        acc.IsActive = 0;
        acc.DeletedBy = _ctx.CurrentUserNo();
        acc.DeletedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
    }

    private async Task<string> NextAccountCodeAsync(long companyNo, string prefix, CancellationToken ct)
    {
        long count = await _db.FinAccounts.CountAsync(a => a.CompanyNo == companyNo, ct) + 1;
        string code;
        do { code = $"{prefix}{count++:D4}"; }
        while (await _db.FinAccounts.AnyAsync(a => a.AccountCode == code && a.CompanyNo == companyNo && a.IsDeleted == 0, ct));
        return code;
    }

    private static Fin1001AccountDto ToDto(FinAccount a, string? groupName) => new()
    {
        AccountNo = a.AccountNo,
        AccountCode = a.AccountCode,
        AccountName = a.AccountName,
        AccountGroupNo = a.AccountGroupNo,
        RootType = a.RootType,
        NormalBalance = a.NormalBalance,
        IsPostable = a.IsPostable,
        ControlType = a.ControlType,
        RequiresCostCenter = a.RequiresCostCenter,
        RequiresParty = a.RequiresParty,
        CurrencyNo = a.CurrencyNo,
        OpeningBalance = a.OpeningBalance,
        OpeningDrCr = a.OpeningDrCr,
        BranchNo = a.BranchNo,
        IsActive = a.IsActive,
        RowVersion = a.RowVersion,
        AccountGroupName = groupName
    };
}
