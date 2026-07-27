using Microsoft.EntityFrameworkCore;
using AidlyErp.Application.Common.Exceptions;
using AidlyErp.Application.Common.Interfaces;
using AidlyErp.Application.Common.Security;
using AidlyErp.Application.Fin.Dto;
using AidlyErp.Domain.Fin;

namespace AidlyErp.Application.Fin.Services;

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
    private readonly IApplicationDbContext _db;
    private readonly ICompanyBranchContext _ctx;

    public Fin1005Service(IApplicationDbContext db, ICompanyBranchContext ctx)
    {
        _db = db;
        _ctx = ctx;
    }

    public async Task<List<Fin1005BankAccountDto>> GetListAsync(CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? 0;
        var accounts = await _db.FinAccounts.AsNoTracking().Where(a => a.IsDeleted == 0).ToDictionaryAsync(a => a.AccountNo, ct);

        return await _db.FinBankAccounts
            .AsNoTracking()
            .Where(b => b.CompanyNo == companyNo && b.IsDeleted == 0)
            .OrderBy(b => b.BankName)
            .Select(b => ToDto(b, accounts.ContainsKey(b.AccountNo) ? accounts[b.AccountNo] : null))
            .ToListAsync(ct);
    }

    public async Task<Fin1005BankAccountDto> GetDetailAsync(long bankAccountNo, CancellationToken ct = default)
    {
        var bank = await _db.FinBankAccounts.AsNoTracking().FirstOrDefaultAsync(b => b.BankAccountNo == bankAccountNo && b.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Bank account not found: {bankAccountNo}");

        var acc = await _db.FinAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.AccountNo == bank.AccountNo, ct);
        return ToDto(bank, acc);
    }

    public async Task<Fin1005BankAccountDto> SaveAsync(Fin1005BankAccountDto dto, CancellationToken ct = default)
    {
        if (dto.BankAccountNo.HasValue && dto.BankAccountNo.Value > 0)
        {
            var bank = await _db.FinBankAccounts.FirstOrDefaultAsync(b => b.BankAccountNo == dto.BankAccountNo.Value && b.IsDeleted == 0, ct)
                ?? throw new NotFoundException($"Bank account not found: {dto.BankAccountNo}");
            if (dto.BankName != null) bank.BankName = dto.BankName.Trim();
            if (dto.BranchName != null) bank.BranchName = dto.BranchName.Trim();
            if (dto.AccountNumber != null) bank.AccountNumber = dto.AccountNumber.Trim();
            bank.SwiftCode = dto.SwiftCode;
            bank.Iban = dto.Iban;
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
            if (string.IsNullOrWhiteSpace(dto.BankName)) throw new ValidationException("Bank name is required");
            if (string.IsNullOrWhiteSpace(dto.AccountNumber)) throw new ValidationException("Account number is required");

            var acc = await _db.FinAccounts.FirstOrDefaultAsync(a => a.AccountNo == dto.AccountNo && a.CompanyNo == companyNo && a.IsDeleted == 0, ct)
                ?? throw new NotFoundException($"GL Account not found: {dto.AccountNo}");

            var bank = new FinBankAccount
            {
                CompanyNo = companyNo,
                BranchNo = branchNo,
                AccountNo = dto.AccountNo,
                BankName = dto.BankName.Trim(),
                BranchName = dto.BranchName?.Trim(),
                AccountNumber = dto.AccountNumber.Trim(),
                SwiftCode = dto.SwiftCode,
                Iban = dto.Iban,
                CurrencyNo = dto.CurrencyNo,
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
        var bank = await _db.FinBankAccounts.FirstOrDefaultAsync(b => b.BankAccountNo == bankAccountNo && b.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Bank account not found: {bankAccountNo}");

        bank.IsDeleted = 1; bank.IsActive = 0;
        bank.DeletedBy = _ctx.CurrentUserNo(); bank.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private static Fin1005BankAccountDto ToDto(FinBankAccount b, FinAccount? a) => new()
    {
        BankAccountNo = b.BankAccountNo,
        AccountNo = b.AccountNo,
        BankName = b.BankName,
        BranchName = b.BranchName,
        AccountNumber = b.AccountNumber,
        SwiftCode = b.SwiftCode,
        Iban = b.Iban,
        CurrencyNo = b.CurrencyNo,
        BranchNo = b.BranchNo,
        IsActive = b.IsActive,
        RowVersion = b.RowVersion,
        GlAccountCode = a?.AccountCode,
        GlAccountName = a?.AccountName
    };
}
