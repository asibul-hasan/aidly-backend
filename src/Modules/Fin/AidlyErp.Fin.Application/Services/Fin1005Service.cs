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
// Fin1005Service — Bank Accounts Setup
// ═══════════════════════════════════════════════════════════════════════════

public interface IFin1005Service
{
    Task<List<Fin1005BankAccountDto>> GetListAsync(CancellationToken ct = default);
    Task<Fin1005BankAccountDto> GetDetailAsync(long bankAccountNo, CancellationToken ct = default);
    Task<Fin1005BankAccountDto> SaveAsync(Fin1005BankAccountDto dto, CancellationToken ct = default);
    Task DeleteAsync(long bankAccountNo, CancellationToken ct = default);
}

public class Fin1005Service : IFin1005Service
{
    private readonly IFinDbContext _db;
    private readonly ICompanyBranchContext _ctx;

    public Fin1005Service(IFinDbContext db, ICompanyBranchContext ctx)
    {
        _db = db;
        _ctx = ctx;
    }

    public async Task<List<Fin1005BankAccountDto>> GetListAsync(CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? 0;
        var accounts = await _db.FinAccounts.AsNoTracking().Where(a => a.IsDeleted == 0).ToDictionaryAsync(a => a.AccountNo, ct);

        var rows = await _db.FinBankAccounts
            .AsNoTracking()
            .Where(b => b.CompanyNo == companyNo && b.IsDeleted == 0)
            .OrderBy(b => b.BankName)
            .ToListAsync(ct);

        return rows.Select(b => ToDto(b, accounts.GetValueOrDefault(b.AccountNo))).ToList();
    }

    public async Task<Fin1005BankAccountDto> GetDetailAsync(long bankAccountNo, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? 0;
        var bank = await _db.FinBankAccounts.AsNoTracking().FirstOrDefaultAsync(b => b.BankAccountNo == bankAccountNo && b.CompanyNo == companyNo && b.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Bank account not found: {bankAccountNo}");

        var acc = await _db.FinAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.AccountNo == bank.AccountNo, ct);
        return ToDto(bank, acc);
    }

    public async Task<Fin1005BankAccountDto> SaveAsync(Fin1005BankAccountDto dto, CancellationToken ct = default)
    {
        if (dto.BankAccountNo.HasValue && dto.BankAccountNo.Value > 0)
        {
            long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
            var bank = await _db.FinBankAccounts.FirstOrDefaultAsync(b => b.BankAccountNo == dto.BankAccountNo.Value && b.CompanyNo == companyNo && b.IsDeleted == 0, ct)
                ?? throw new NotFoundException($"Bank account not found: {dto.BankAccountNo}");

            // If GL account link changed, validate the new one
            if (dto.AccountNo != bank.AccountNo && dto.AccountNo > 0)
            {
                var newAcc = await _db.FinAccounts.FirstOrDefaultAsync(a => a.AccountNo == dto.AccountNo && a.CompanyNo == companyNo && a.IsDeleted == 0, ct)
                    ?? throw new NotFoundException($"Account not found: {dto.AccountNo}");
                if (newAcc.IsActive != 1) throw new ValidationException("The linked GL account is inactive");
                if (newAcc.ControlType is not (3 or 4))
                    throw new ValidationException("The linked GL account must be a Bank (3) or Cash (4) control account");
                // 1:1 link enforcement — only check if account actually changed
                if (await _db.FinBankAccounts.AnyAsync(b => b.AccountNo == dto.AccountNo && b.CompanyNo == companyNo && b.BankAccountNo != bank.BankAccountNo && b.IsDeleted == 0, ct))
                    throw new ValidationException("This GL Bank/Cash account is already linked to a bank account setup");
                bank.AccountNo = dto.AccountNo;
            }

            if (dto.BankName != null) bank.BankName = dto.BankName.Trim();
            if (dto.BranchName != null) bank.BranchName = dto.BranchName.Trim();
            if (dto.AccountNumber != null) bank.AccountNumber = dto.AccountNumber.Trim();
            bank.AccountTitle = dto.AccountTitle?.Trim();
            bank.RoutingNumber = dto.RoutingNumber?.Trim();
            bank.SwiftCode = dto.SwiftCode;
            bank.CurrencyNo = dto.CurrencyNo;
            bank.IsActive = dto.IsActive;
            bank.UpdatedBy = _ctx.CurrentUserNo(); bank.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            var acc = await _db.FinAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.AccountNo == bank.AccountNo, ct);
            return ToDto(bank, acc);
        }
        else
        {
            long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
            long branchNo = dto.BranchNo ?? _ctx.CurrentBranchNo() ?? throw new ValidationException("Branch context is required");
            if (string.IsNullOrWhiteSpace(dto.BankAccountId?.Trim()))
                throw new ValidationException("Bank account ID is required");
            if (string.IsNullOrWhiteSpace(dto.BankName)) throw new ValidationException("Bank name is required");
            if (string.IsNullOrWhiteSpace(dto.AccountNumber)) throw new ValidationException("Account number is required");

            string bankAccountId = dto.BankAccountId.Trim().ToUpperInvariant();

            if (await _db.FinBankAccounts.AnyAsync(b => b.BankAccountId == bankAccountId && b.CompanyNo == companyNo && b.IsDeleted == 0, ct))
                throw new ValidationException($"Bank account ID already exists: {bankAccountId}");

            var acc = await _db.FinAccounts.FirstOrDefaultAsync(a => a.AccountNo == dto.AccountNo && a.CompanyNo == companyNo && a.IsDeleted == 0, ct)
                ?? throw new NotFoundException($"Account not found: {dto.AccountNo}");
            if (acc.IsActive != 1) throw new ValidationException("The linked GL account is inactive");
            if (acc.ControlType is not (3 or 4))
                throw new ValidationException("The linked GL account must be a Bank (3) or Cash (4) control account");

            // 1:1 link enforcement
            if (await _db.FinBankAccounts.AnyAsync(b => b.AccountNo == dto.AccountNo && b.CompanyNo == companyNo && b.IsDeleted == 0, ct))
                throw new ValidationException("This GL Bank/Cash account is already linked to a bank account setup");

            var bank = new FinBankAccount
            {
                CompanyNo = companyNo,
                BranchNo = branchNo,
                BankAccountId = bankAccountId,
                AccountNo = dto.AccountNo,
                BankName = dto.BankName.Trim(),
                BranchName = dto.BranchName?.Trim(),
                AccountTitle = dto.AccountTitle?.Trim(),
                AccountNumber = dto.AccountNumber.Trim(),
                RoutingNumber = dto.RoutingNumber?.Trim(),
                SwiftCode = dto.SwiftCode,
                CurrencyNo = dto.CurrencyNo,
                OpeningBalance = dto.OpeningBalance,
                IsActive = dto.IsActive,
                IsDeleted = 0,
                CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow
            };

            _db.FinBankAccounts.Add(bank);
            await _db.SaveChangesAsync(ct);
            return ToDto(bank, acc);
        }
    }

    public async Task DeleteAsync(long bankAccountNo, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
        var bank = await _db.FinBankAccounts.FirstOrDefaultAsync(b => b.BankAccountNo == bankAccountNo && b.CompanyNo == companyNo && b.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Bank account not found: {bankAccountNo}");

        // Check reconciliation history on the GL account
        if (await _db.FinBankRecons.AnyAsync(r => r.AccountNo == bank.AccountNo && r.CompanyNo == companyNo && r.IsDeleted == 0, ct))
            throw new ValidationException("Cannot delete: this bank account has reconciliation history");

        bank.IsDeleted = 1; bank.IsActive = 0;
        bank.DeletedBy = _ctx.CurrentUserNo(); bank.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private static Fin1005BankAccountDto ToDto(FinBankAccount b, FinAccount? a) => new()
    {
        BankAccountNo = b.BankAccountNo,
        BankAccountId = b.BankAccountId,
        AccountNo = b.AccountNo,
        BankName = b.BankName,
        BranchName = b.BranchName,
        AccountTitle = b.AccountTitle,
        AccountNumber = b.AccountNumber,
        RoutingNumber = b.RoutingNumber,
        SwiftCode = b.SwiftCode,
        CurrencyNo = b.CurrencyNo,
        OpeningBalance = b.OpeningBalance,
        BranchNo = b.BranchNo,
        IsActive = b.IsActive ?? 0,
        RowVersion = b.RowVersion,
        GlAccountCode = a?.AccountCode,
        GlAccountName = a?.AccountName
    };
}
