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
    private readonly IDocSequenceGenerator _docSeq;

    public Fin1001Service(IFinDbContext db, ICompanyBranchContext ctx, IDocSequenceGenerator docSeq)
    {
        _db = db;
        _ctx = ctx;
        _docSeq = docSeq;
    }

    public async Task<List<Fin1001AccountDto>> GetListAsync(CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? 0;
        var groups = await _db.FinAccountGroups.AsNoTracking().Where(g => g.CompanyNo == companyNo && g.IsDeleted == 0).ToDictionaryAsync(g => g.AccountGroupNo, g => g.GroupName, ct);

        var rows = await _db.FinAccounts
            .AsNoTracking()
            .Where(a => a.CompanyNo == companyNo && a.IsDeleted == 0)
            .OrderBy(a => a.AccountCode)
            .ToListAsync(ct);

        return rows.Select(a => ToDto(a, groups.GetValueOrDefault(a.AccountGroupNo))).ToList();
    }

    public async Task<Fin1001AccountDto> GetDetailAsync(long accountNo, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
        var acc = await _db.FinAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.AccountNo == accountNo && a.CompanyNo == companyNo && a.IsDeleted == 0, ct)
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
        if (string.IsNullOrWhiteSpace(dto.AccountCode) && dto.AccountGroupNo <= 0)
            throw new ValidationException("Account code is required");
        if (string.IsNullOrWhiteSpace(dto.AccountName)) throw new ValidationException("Account name is required");

        var group = await _db.FinAccountGroups
            .FirstOrDefaultAsync(g => g.AccountGroupNo == dto.AccountGroupNo && g.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Account group not found: {dto.AccountGroupNo}");

        if (group.CompanyNo != companyNo)
            throw new ValidationException("Account group belongs to another company");

        if (dto.ControlType.HasValue && (dto.ControlType.Value < 1 || dto.ControlType.Value > 7))
            throw new ValidationException("control_type must be 1..7 (AR/AP/Bank/Cash/Inventory/Tax/Retained-Earnings)");

        // normal_balance derives from root_type: Asset/Expense => dr; Liability/Equity/Income => cr
        string derivedNb = group.RootType is 1 or 5 ? "dr" : "cr";
        string? nb = dto.NormalBalance?.ToLowerInvariant();
        if (nb != null && nb != "dr" && nb != "cr")
            throw new ValidationException("normal_balance must be dr or cr");
        if (nb != null && nb != derivedNb)
            throw new ValidationException($"normal_balance '{nb}' contradicts root_type {group.RootType} (expected '{derivedNb}')");

        string code = string.IsNullOrWhiteSpace(dto.AccountCode) ? await NextAccountCodeAsync(companyNo, group.GroupCode, ct) : dto.AccountCode.Trim().ToUpperInvariant();

        if (await _db.FinAccounts.AnyAsync(a => a.AccountCode == code && a.CompanyNo == companyNo && a.IsDeleted == 0, ct))
            throw new ValidationException($"Account code already exists: {code}");

        // Auto-force RequiresParty for AR (1) and AP (2) accounts
        short requiresParty = dto.RequiresParty;
        if (dto.ControlType is 1 or 2) requiresParty = 1;

        var acc = new FinAccount
        {
            CompanyNo = companyNo,
            BranchNo = dto.BranchNo ?? _ctx.CurrentBranchNo(),
            AccountCode = code,
            AccountName = dto.AccountName.Trim(),
            AccountGroupNo = dto.AccountGroupNo,
            RootType = group.RootType,
            NormalBalance = derivedNb,
            IsPostable = dto.IsPostable,
            ControlType = dto.ControlType,
            RequiresCostCenter = dto.RequiresCostCenter,
            RequiresParty = requiresParty,
            CurrencyNo = dto.CurrencyNo,
            OpeningBalance = dto.OpeningBalance,
            OpeningDrCr = dto.OpeningDrCr?.ToLowerInvariant() ?? "dr",
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
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
        var acc = await _db.FinAccounts.FirstOrDefaultAsync(a => a.AccountNo == accountNo && a.CompanyNo == companyNo && a.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Account not found: {accountNo}");

        // Opening balance lock: cannot change once account has posted ledger entries
        bool hasPostedEntries = await _db.FinLedgers.AnyAsync(l => l.AccountNo == accountNo && l.IsDeleted == 0, ct);
        if (hasPostedEntries)
        {
            if (dto.OpeningBalance != acc.OpeningBalance ||
                (dto.OpeningDrCr != null && dto.OpeningDrCr.ToLowerInvariant() != (acc.OpeningDrCr ?? "dr")))
                throw new ValidationException("Opening balance cannot change — openings are established in FIN_1004 Opening Balances");
        }

        if (dto.ControlType.HasValue && (dto.ControlType.Value < 1 || dto.ControlType.Value > 7))
            throw new ValidationException("control_type must be 1..7 (AR/AP/Bank/Cash/Inventory/Tax/Retained-Earnings)");

        if (dto.AccountName != null) acc.AccountName = dto.AccountName.Trim();
        if (dto.AccountGroupNo > 0 && dto.AccountGroupNo != acc.AccountGroupNo)
        {
            var grp = await _db.FinAccountGroups.FirstOrDefaultAsync(g => g.AccountGroupNo == dto.AccountGroupNo && g.IsDeleted == 0, ct)
                ?? throw new NotFoundException($"Account group not found: {dto.AccountGroupNo}");
            if (grp.CompanyNo != companyNo)
                throw new ValidationException("Account group belongs to another company");
            acc.AccountGroupNo = dto.AccountGroupNo;
            acc.RootType = grp.RootType;
            if (dto.NormalBalance == null) acc.NormalBalance = grp.NormalBalance ?? "dr";
        }
        if (dto.NormalBalance != null)
        {
            string nb = dto.NormalBalance.ToLowerInvariant();
            if (nb != "dr" && nb != "cr") throw new ValidationException("normal_balance must be dr or cr");
            acc.NormalBalance = nb;
        }
        acc.IsPostable = dto.IsPostable;
        acc.ControlType = dto.ControlType;
        acc.RequiresCostCenter = dto.RequiresCostCenter;

        // Auto-force RequiresParty for AR (1) and AP (2) accounts
        acc.RequiresParty = dto.ControlType is 1 or 2 ? (short)1 : dto.RequiresParty;

        acc.CurrencyNo = dto.CurrencyNo;
        if (!hasPostedEntries)
        {
            acc.OpeningBalance = dto.OpeningBalance;
            if (dto.OpeningDrCr != null) acc.OpeningDrCr = dto.OpeningDrCr.ToLowerInvariant();
        }
        acc.IsActive = dto.IsActive;
        acc.UpdatedBy = _ctx.CurrentUserNo();
        acc.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        var groupName = await _db.FinAccountGroups.Where(g => g.AccountGroupNo == acc.AccountGroupNo).Select(g => g.GroupName).FirstOrDefaultAsync(ct);
        return ToDto(acc, groupName);
    }

    public async Task DeleteAsync(long accountNo, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
        var acc = await _db.FinAccounts.FirstOrDefaultAsync(a => a.AccountNo == accountNo && a.CompanyNo == companyNo && a.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Account not found: {accountNo}");

        if (await _db.FinLedgers.AnyAsync(l => l.AccountNo == accountNo && l.IsDeleted == 0, ct) ||
            await _db.FinVoucherDtls.AnyAsync(d => d.AccountNo == accountNo && d.IsDeleted == 0, ct))
            throw new ValidationException("Cannot delete: this account is used in vouchers/ledger — deactivate it instead");

        acc.IsDeleted = 1;
        acc.IsActive = 0;
        acc.DeletedBy = _ctx.CurrentUserNo();
        acc.DeletedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
    }

    private async Task<string> NextAccountCodeAsync(long companyNo, string prefix, CancellationToken ct)
    {
        // Use IDocSequenceGenerator for gap-free, race-free account codes
        return await _docSeq.NextAsync(companyNo, null, "FIN_ACCOUNT", prefix, 4, ct);
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
        IsActive = a.IsActive ?? 0,
        RowVersion = a.RowVersion,
        AccountGroupName = groupName
    };
}
