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
// Fin1001Service — Unified Chart of Accounts (COA) Master Service
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

        var allRows = await _db.FinAccounts
            .AsNoTracking()
            .Where(a => a.CompanyNo == companyNo && a.IsDeleted == 0)
            .OrderBy(a => a.RootType)
            .ThenBy(a => a.DisplayOrder)
            .ThenBy(a => a.AccountCode)
            .ToListAsync(ct);

        var rowLookup = allRows.ToDictionary(a => a.AccountNo);

        return allRows.Select(a =>
        {
            FinAccount? parent = null;
            if (a.ParentAccountNo.HasValue && rowLookup.TryGetValue(a.ParentAccountNo.Value, out var p))
            {
                parent = p;
            }
            return ToDto(a, parent);
        }).ToList();
    }

    public async Task<Fin1001AccountDto> GetDetailAsync(long accountNo, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
        var acc = await _db.FinAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.AccountNo == accountNo && a.CompanyNo == companyNo && a.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Account not found: {accountNo}");

        FinAccount? parent = null;
        if (acc.ParentAccountNo.HasValue)
        {
            parent = await _db.FinAccounts
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.AccountNo == acc.ParentAccountNo.Value && p.CompanyNo == companyNo && p.IsDeleted == 0, ct);
        }

        return ToDto(acc, parent);
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
        if (string.IsNullOrWhiteSpace(dto.AccountName))
            throw new ValidationException("Account name is required");

        short isGroup = (short)(dto.IsGroup == 1 || dto.IsGroupAccount == 1 ? 1 : 0);
        short isPostable = (short)(isGroup == 1 ? 0 : (dto.IsPostable ?? 1));
        long? parentNo = dto.ParentAccountNo ?? (dto.AccountGroupNo > 0 ? dto.AccountGroupNo : null);

        FinAccount? parent = null;
        if (parentNo.HasValue && parentNo.Value > 0)
        {
            parent = await _db.FinAccounts
                .FirstOrDefaultAsync(p => p.AccountNo == parentNo.Value && p.CompanyNo == companyNo && p.IsDeleted == 0, ct)
                ?? throw new NotFoundException($"Parent account not found: {parentNo}");
        }

        short rootType = dto.RootType ?? parent?.RootType ?? 1;
        if (rootType < 1 || rootType > 5)
            throw new ValidationException("Root type must be 1=Asset, 2=Liability, 3=Equity, 4=Income, 5=Expense");

        // Normal balance: Asset/Expense => dr; Liability/Equity/Income => cr
        string expectedNb = rootType is 1 or 5 ? "dr" : "cr";
        string normalBalance = !string.IsNullOrWhiteSpace(dto.NormalBalance)
            ? dto.NormalBalance.Trim().ToLowerInvariant()
            : expectedNb;

        if (normalBalance != "dr" && normalBalance != "cr")
            throw new ValidationException("normal_balance must be 'dr' or 'cr'");

        if (dto.ControlType.HasValue && (dto.ControlType.Value < 1 || dto.ControlType.Value > 7))
            throw new ValidationException("control_type must be 1..7 (AR/AP/Bank/Cash/Inventory/Tax/Retained-Earnings)");

        string code = string.IsNullOrWhiteSpace(dto.AccountCode)
            ? await NextAccountCodeAsync(companyNo, parent?.AccountCode ?? $"RT{rootType}", ct)
            : dto.AccountCode.Trim().ToUpperInvariant();

        if (await _db.FinAccounts.AnyAsync(a => a.AccountCode == code && a.CompanyNo == companyNo && a.IsDeleted == 0, ct))
            throw new ValidationException($"Account code already exists: {code}");

        // Auto-force RequiresParty for AR (1) and AP (2)
        short requiresParty = dto.RequiresParty ?? 0;
        if (dto.ControlType is 1 or 2) requiresParty = 1;

        var acc = new FinAccount
        {
            CompanyNo = companyNo,
            BranchNo = dto.BranchNo ?? _ctx.CurrentBranchNo(),
            AccountCode = code,
            AccountName = (dto.AccountName ?? "").Trim(),
            ParentAccountNo = parentNo,
            AccountGroupNo = parentNo,
            RootType = rootType,
            NormalBalance = normalBalance,
            IsGroup = isGroup,
            IsPostable = isPostable,
            IsControl = dto.IsControl ?? 0,
            ControlType = dto.ControlType,
            AccountCategory = dto.AccountCategory,
            SubCategory = dto.SubCategory,
            AccountClass = dto.AccountClass,
            RequiresCostCenter = dto.RequiresCostCenter ?? 0,
            RequiresParty = requiresParty,
            RequiresReconciliation = dto.RequiresReconciliation ?? 0,
            RequiresBranch = dto.RequiresBranch ?? 0,
            RequiresProject = dto.RequiresProject ?? 0,
            RequiresDepartment = dto.RequiresDepartment ?? 0,
            CurrencyNo = dto.CurrencyNo,
            OpeningBalance = isGroup == 1 ? 0m : (dto.OpeningBalance ?? 0m),
            OpeningDrCr = dto.OpeningDrCr?.ToLowerInvariant() ?? "dr",
            DisplayOrder = dto.OrderSl ?? 0,
            Description = dto.Description,
            IsActive = dto.IsActive ?? 1,
            IsDeleted = 0,
            CreatedBy = _ctx.CurrentUserNo(),
            CreatedAt = DateTime.UtcNow
        };

        _db.FinAccounts.Add(acc);
        await _db.SaveChangesAsync(ct);

        return ToDto(acc, parent);
    }

    private async Task<Fin1001AccountDto> UpdateAsync(long accountNo, Fin1001AccountDto dto, CancellationToken ct)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
        var acc = await _db.FinAccounts
            .FirstOrDefaultAsync(a => a.AccountNo == accountNo && a.CompanyNo == companyNo && a.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Account not found: {accountNo}");

        long? parentNo = dto.ParentAccountNo ?? (dto.AccountGroupNo.HasValue && dto.AccountGroupNo.Value > 0 ? dto.AccountGroupNo : null);

        if (parentNo.HasValue && parentNo.Value == accountNo)
            throw new ValidationException("An account cannot be its own parent");

        FinAccount? parent = null;
        if (parentNo.HasValue && parentNo.Value > 0)
        {
            parent = await _db.FinAccounts
                .FirstOrDefaultAsync(p => p.AccountNo == parentNo.Value && p.CompanyNo == companyNo && p.IsDeleted == 0, ct)
                ?? throw new NotFoundException($"Parent account not found: {parentNo}");

            if (parent.CompanyNo != companyNo)
                throw new ValidationException("Parent account belongs to another company");
        }

        // Opening balance lock: cannot change once account has posted ledger entries
        bool hasPostedEntries = await _db.FinLedgers.AnyAsync(l => l.AccountNo == accountNo && l.IsDeleted == 0, ct);
        if (hasPostedEntries && acc.IsGroup == 0)
        {
            if ((dto.OpeningBalance.HasValue && dto.OpeningBalance.Value != acc.OpeningBalance) ||
                (dto.OpeningDrCr != null && dto.OpeningDrCr.ToLowerInvariant() != (acc.OpeningDrCr ?? "dr")))
            {
                throw new ValidationException("Opening balance cannot change — openings are established in FIN_1004 Opening Balances");
            }
        }

        if (dto.ControlType.HasValue && (dto.ControlType.Value < 1 || dto.ControlType.Value > 7))
            throw new ValidationException("control_type must be 1..7 (AR/AP/Bank/Cash/Inventory/Tax/Retained-Earnings)");

        if (!string.IsNullOrWhiteSpace(dto.AccountCode))
        {
            string code = dto.AccountCode.Trim().ToUpperInvariant();
            if (code != acc.AccountCode &&
                await _db.FinAccounts.AnyAsync(a => a.AccountCode == code && a.CompanyNo == companyNo && a.IsDeleted == 0, ct))
            {
                throw new ValidationException($"Account code already exists: {code}");
            }
            acc.AccountCode = code;
        }

        if (dto.AccountName != null && !string.IsNullOrWhiteSpace(dto.AccountName))
            acc.AccountName = dto.AccountName.Trim();

        if (dto.ParentAccountNo != null || dto.AccountGroupNo.HasValue)
        {
            acc.ParentAccountNo = parentNo;
            acc.AccountGroupNo = parentNo;
        }

        if (dto.RootType.HasValue && dto.RootType.Value >= 1 && dto.RootType.Value <= 5)
        {
            // If changing root_type, ensure children allow it
            if (dto.RootType.Value != acc.RootType &&
                await _db.FinAccounts.AnyAsync(c => c.ParentAccountNo == accountNo && c.CompanyNo == companyNo && c.IsDeleted == 0, ct))
            {
                throw new ValidationException("Root type cannot change while child accounts are attached");
            }
            acc.RootType = dto.RootType.Value;
        }

        if (!string.IsNullOrWhiteSpace(dto.NormalBalance))
        {
            string nb = dto.NormalBalance.Trim().ToLowerInvariant();
            if (nb != "dr" && nb != "cr") throw new ValidationException("normal_balance must be dr or cr");
            acc.NormalBalance = nb;
        }

        if (dto.IsGroup.HasValue || dto.IsGroupAccount.HasValue)
        {
            short isGroup = (short)(dto.IsGroup == 1 || dto.IsGroupAccount == 1 ? 1 : 0);
            acc.IsGroup = isGroup;
            acc.IsPostable = isGroup == 1 ? (short)0 : (dto.IsPostable ?? acc.IsPostable);
        }
        else if (dto.IsPostable.HasValue)
        {
            acc.IsPostable = dto.IsPostable.Value;
        }

        if (dto.IsControl.HasValue) acc.IsControl = dto.IsControl.Value;
        if (dto.ControlType.HasValue) acc.ControlType = dto.ControlType;
        if (dto.AccountCategory.HasValue) acc.AccountCategory = dto.AccountCategory;
        if (dto.SubCategory.HasValue) acc.SubCategory = dto.SubCategory;
        if (dto.AccountClass.HasValue) acc.AccountClass = dto.AccountClass;
        if (dto.RequiresCostCenter.HasValue) acc.RequiresCostCenter = dto.RequiresCostCenter.Value;
        if (dto.RequiresParty.HasValue) acc.RequiresParty = dto.ControlType is 1 or 2 ? (short)1 : dto.RequiresParty.Value;
        if (dto.RequiresReconciliation.HasValue) acc.RequiresReconciliation = dto.RequiresReconciliation.Value;
        if (dto.RequiresBranch.HasValue) acc.RequiresBranch = dto.RequiresBranch.Value;
        if (dto.RequiresProject.HasValue) acc.RequiresProject = dto.RequiresProject.Value;
        if (dto.RequiresDepartment.HasValue) acc.RequiresDepartment = dto.RequiresDepartment.Value;
        if (dto.CurrencyNo.HasValue) acc.CurrencyNo = dto.CurrencyNo;
        if (dto.OrderSl.HasValue) acc.DisplayOrder = dto.OrderSl.Value;
        if (dto.Description != null) acc.Description = dto.Description;

        if (!hasPostedEntries && acc.IsGroup == 0)
        {
            if (dto.OpeningBalance.HasValue) acc.OpeningBalance = dto.OpeningBalance.Value;
            if (dto.OpeningDrCr != null) acc.OpeningDrCr = dto.OpeningDrCr.ToLowerInvariant();
        }

        if (dto.IsActive.HasValue) acc.IsActive = dto.IsActive.Value;
        acc.UpdatedBy = _ctx.CurrentUserNo();
        acc.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return ToDto(acc, parent);
    }

    public async Task DeleteAsync(long accountNo, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
        var acc = await _db.FinAccounts
            .FirstOrDefaultAsync(a => a.AccountNo == accountNo && a.CompanyNo == companyNo && a.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Account not found: {accountNo}");

        // Check if has children
        if (await _db.FinAccounts.AnyAsync(c => c.ParentAccountNo == accountNo && c.CompanyNo == companyNo && c.IsDeleted == 0, ct))
            throw new ValidationException("Cannot delete: this node has child accounts or groups. Please delete or reassign child nodes first.");

        if (await _db.FinLedgers.AnyAsync(l => l.AccountNo == accountNo && l.IsDeleted == 0, ct) ||
            await _db.FinVoucherDtls.AnyAsync(d => d.AccountNo == accountNo && d.IsDeleted == 0, ct))
            throw new ValidationException("Cannot delete: this account is used in vouchers/ledger — deactivate it instead.");

        acc.IsDeleted = 1;
        acc.IsActive = 0;
        acc.DeletedBy = _ctx.CurrentUserNo();
        acc.DeletedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
    }

    private async Task<string> NextAccountCodeAsync(long companyNo, string prefix, CancellationToken ct)
    {
        return await _docSeq.NextAsync(companyNo, null, "FIN_ACCOUNT", prefix, 4, ct);
    }

    private static Fin1001AccountDto ToDto(FinAccount a, FinAccount? parent) => new()
    {
        AccountNo = a.AccountNo,
        AccountCode = a.AccountCode,
        AccountName = a.AccountName,
        ParentAccountNo = a.ParentAccountNo,
        ParentAccountCode = parent?.AccountCode,
        ParentAccountName = parent?.AccountName,
        RootType = a.RootType,
        NormalBalance = a.NormalBalance,
        IsGroup = a.IsGroup,
        IsPostable = a.IsPostable,
        IsControl = a.IsControl,
        ControlType = a.ControlType,
        AccountCategory = a.AccountCategory,
        SubCategory = a.SubCategory,
        AccountClass = a.AccountClass,
        RequiresCostCenter = a.RequiresCostCenter,
        RequiresParty = a.RequiresParty,
        RequiresReconciliation = a.RequiresReconciliation,
        RequiresBranch = a.RequiresBranch,
        RequiresProject = a.RequiresProject,
        RequiresDepartment = a.RequiresDepartment,
        CurrencyNo = a.CurrencyNo,
        OpeningBalance = a.OpeningBalance,
        OpeningDrCr = a.OpeningDrCr,
        OrderSl = a.DisplayOrder,
        Description = a.Description,
        BranchNo = a.BranchNo,
        IsActive = a.IsActive ?? 0,
        RowVersion = a.RowVersion
    };
}
