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
// Fin1102Service — Bank Reconciliation
// ═══════════════════════════════════════════════════════════════════════════

public interface IFin1102Service
{
    Task<List<Fin1102BankAccountDto>> GetBankAccountsAsync(CancellationToken ct = default);
    Task<Fin1102WorksheetDto> GetWorksheetAsync(long accountNo, DateTime statementDate, CancellationToken ct = default);
    Task<List<Fin1102ReconDto>> GetListAsync(CancellationToken ct = default);
    Task<Fin1102ReconDto> GetDetailAsync(long bankReconNo, CancellationToken ct = default);
    Task<Fin1102ReconDto> SaveReconciliationAsync(Fin1102SaveDto dto, CancellationToken ct = default);
    Task<List<Fin1102ReconDto>> GetReconHistoryAsync(long accountNo, CancellationToken ct = default);
    Task DeleteAsync(long bankReconNo, CancellationToken ct = default);
}

public class Fin1102Service : IFin1102Service
{
    private readonly IFinDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    private readonly IUnitOfWork<IFinDbContext> _uow;

    public Fin1102Service(IFinDbContext db, ICompanyBranchContext ctx, IUnitOfWork<IFinDbContext> uow)
    {
        _db = db;
        _ctx = ctx;
        _uow = uow;
    }

    public async Task<List<Fin1102BankAccountDto>> GetBankAccountsAsync(CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? 0;
        // Java: returns only accounts where controlType == 3 (Bank) AND isPostable == 1
        return await _db.FinBankAccounts.AsNoTracking()
            .Where(b => b.CompanyNo == companyNo && b.IsDeleted == 0 &&
                        _db.FinAccounts.Any(a => a.AccountNo == b.AccountNo && a.ControlType == 3 && a.IsPostable == 1 && a.IsDeleted == 0))
            .OrderByDescending(b => b.BankAccountNo)
            .Select(b => new Fin1102BankAccountDto
            {
                BankAccountNo = b.BankAccountNo,
                AccountNo = b.AccountNo,
                BankName = b.BankName,
                AccountNumber = b.AccountNumber
            })
            .ToListAsync(ct);
    }

    public async Task<Fin1102WorksheetDto> GetWorksheetAsync(long accountNo, DateTime statementDate, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");

        var acc = await _db.FinAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.AccountNo == accountNo && a.CompanyNo == companyNo && a.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Account not found: {accountNo}");

        if (acc.ControlType != 3)
            throw new ValidationException($"Account {acc.AccountCode} is not a bank account");

        // Opening balance from ledger: sum(debit) - sum(credit) before statementDate
        var opening = await _db.FinLedgers.AsNoTracking()
            .Where(l => l.AccountNo == accountNo && l.CompanyNo == companyNo && l.VoucherDate < statementDate && l.IsDeleted == 0)
            .GroupBy(_ => 1)
            .Select(g => new { Debit = g.Sum(x => x.Debit), Credit = g.Sum(x => x.Credit) })
            .FirstOrDefaultAsync(ct);
        // Opening balance is ledger-derived only — fin_account.opening_balance is a data-entry seed.
        decimal openingBalance = (opening?.Debit ?? 0m) - (opening?.Credit ?? 0m);

        // Cleared ledger nos — scoped to this company's reconciliations
        var clearedLedgerNos = await _db.FinBankReconLines.AsNoTracking()
            .Where(l => l.IsCleared == 1 && l.IsDeleted == 0 &&
                        _db.FinBankRecons.Any(r => r.ReconNo == l.ReconNo && r.CompanyNo == companyNo && r.IsDeleted == 0))
            .Select(l => l.LedgerNo)
            .ToHashSetAsync(ct);

        var ledgerLines = await _db.FinLedgers.AsNoTracking()
            .Where(l => l.AccountNo == accountNo && l.CompanyNo == companyNo && l.VoucherDate <= statementDate && l.IsDeleted == 0)
            .OrderBy(l => l.VoucherDate).ThenBy(l => l.LedgerNo)
            .ToListAsync(ct);

        var voucherNos = ledgerLines.Select(l => l.VoucherNo).Distinct().ToList();
        var vouchers = await _db.FinVouchers.AsNoTracking().Where(v => voucherNos.Contains(v.VoucherNo)).ToDictionaryAsync(v => v.VoucherNo, ct);

        decimal bookBalance = openingBalance;

        var lineDtos = new List<Fin1102LedgerLineDto>();
        foreach (var l in ledgerLines)
        {
            bookBalance += (l.Debit - l.Credit);

            if (!clearedLedgerNos.Contains(l.LedgerNo))
            {
                vouchers.TryGetValue(l.VoucherNo, out var v);
                lineDtos.Add(new Fin1102LedgerLineDto
                {
                    LedgerNo = l.LedgerNo,
                    VoucherNo = l.VoucherNo,
                    VoucherId = v?.VoucherId,
                    TxnDate = l.VoucherDate,
                    Narration = v?.Narration,
                    Debit = l.Debit,
                    Credit = l.Credit,
                    IsCleared = 0
                });
            }
        }

        return new Fin1102WorksheetDto
        {
            AccountNo = accountNo,
            AccountCode = acc.AccountCode,
            AccountName = acc.AccountName,
            StatementDate = statementDate,
            BookBalance = bookBalance,
            Lines = lineDtos
        };
    }

    public async Task<Fin1102ReconDto> SaveReconciliationAsync(Fin1102SaveDto dto, CancellationToken ct = default)
    {
        return await _uow.ExecuteAsync(async token =>
        {
            long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
            long branchNo = _ctx.CurrentBranchNo() ?? throw new ValidationException("Branch context is required");

            if (dto.AccountNo <= 0) throw new ValidationException("Bank account is required");
            if (dto.StatementDate == default) throw new ValidationException("Statement date is required");

            // Resolve effective values — support both old (ClearedLedgerNos) and new (ClearedLines) shapes
            var remarks = dto.Remarks ?? dto.Narration;
            var clearedNos = dto.ClearedLines?.Select(l => l.LedgerNo).ToList()
                             ?? dto.ClearedLedgerNos ?? new List<long>();

            // Build per-line bank date lookup
            var bankDates = dto.ClearedLines?.Where(l => l.BankDate.HasValue)
                            .ToDictionary(l => l.LedgerNo, l => l.BankDate!.Value)
                            ?? new Dictionary<long, DateTime>();

            var worksheet = await GetWorksheetAsync(dto.AccountNo, dto.StatementDate, token);

            // Cleared-once protection: load ALL already-cleared ledger nos across ALL reconciliations
            var alreadyCleared = await _db.FinBankReconLines.AsNoTracking()
                .Where(l => l.IsCleared == 1 && l.IsDeleted == 0 &&
                            _db.FinBankRecons.Any(r => r.ReconNo == l.ReconNo && r.CompanyNo == companyNo && r.IsDeleted == 0))
                .Select(l => l.LedgerNo)
                .ToHashSetAsync(token);

            // Validate submitted ledger nos against already-cleared set (defence against replayed requests)
            foreach (var ledgerNo in clearedNos)
            {
                if (alreadyCleared.Contains(ledgerNo))
                    throw new ValidationException($"Ledger line {ledgerNo} is already reconciled");
            }

            var clearedLines = worksheet.Lines.Where(l => clearedNos.Contains(l.LedgerNo)).ToList();
            var unclearedLines = worksheet.Lines.Where(l => !clearedNos.Contains(l.LedgerNo)).ToList();

            decimal clearedDebits = clearedLines.Sum(l => l.Debit);
            decimal clearedCredits = clearedLines.Sum(l => l.Credit);

            // Reconciled = BookBalance minus the items that have NOT cleared the bank
            decimal unclearedDebits = unclearedLines.Sum(l => l.Debit);
            decimal unclearedCredits = unclearedLines.Sum(l => l.Credit);
            decimal reconciledBalance = worksheet.BookBalance - unclearedDebits + unclearedCredits;
            decimal diff = dto.StatementBalance - reconciledBalance;

            var recon = new FinBankRecon
            {
                CompanyNo = companyNo,
                BranchNo = branchNo,
                AccountNo = dto.AccountNo,
                StatementDate = dto.StatementDate,
                StatementBalance = dto.StatementBalance,
                BookBalance = worksheet.BookBalance,
                ClearedDebits = clearedDebits,
                ClearedCredits = clearedCredits,
                ReconciledBalance = reconciledBalance,
                Difference = diff,
                Status = Math.Abs(diff) < 0.01m ? (short)2 : (short)1, // 2=Completed 1=Draft
                Remarks = remarks,
                IsActive = 1, IsDeleted = 0,
                CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow
            };

            _db.FinBankRecons.Add(recon);
            await _db.SaveChangesAsync(token);

            // Finalize recon ID
            recon.BankReconId = $"BR-{recon.ReconNo:D6}";

            foreach (var l in clearedLines)
            {
                var rLine = new FinBankReconLine
                {
                    ReconNo = recon.ReconNo,
                    LedgerNo = l.LedgerNo,
                    Debit = l.Debit,
                    Credit = l.Credit,
                    ClearedDate = bankDates.TryGetValue(l.LedgerNo, out var bd) ? bd : dto.StatementDate,
                    IsActive = 1, IsDeleted = 0,
                    CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow
                };
                _db.FinBankReconLines.Add(rLine);
            }

            await _db.SaveChangesAsync(token);

            return ToDto(recon);
        }, ct);
    }

    public async Task<List<Fin1102ReconDto>> GetListAsync(CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? 0;
        var rows = await _db.FinBankRecons.AsNoTracking()
            .Where(r => r.CompanyNo == companyNo && r.IsDeleted == 0)
            .OrderByDescending(r => r.ReconNo)
            .ToListAsync(ct);
        return rows.Select(r => ToDto(r)).ToList();
    }

    public async Task<Fin1102ReconDto> GetDetailAsync(long bankReconNo, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? 0;
        var recon = await _db.FinBankRecons.AsNoTracking().FirstOrDefaultAsync(r => r.ReconNo == bankReconNo && r.CompanyNo == companyNo && r.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Reconciliation not found: {bankReconNo}");
        return ToDto(recon);
    }

    public async Task<List<Fin1102ReconDto>> GetReconHistoryAsync(long accountNo, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? 0;
        var rows = await _db.FinBankRecons
            .AsNoTracking()
            .Where(r => r.AccountNo == accountNo && r.CompanyNo == companyNo && r.IsDeleted == 0)
            .OrderByDescending(r => r.StatementDate)
            .ToListAsync(ct);
        return rows.Select(r => ToDto(r)).ToList();
    }

    public async Task DeleteAsync(long bankReconNo, CancellationToken ct = default)
    {
        await _uow.ExecuteAsync(async token =>
        {
            long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
            var recon = await _db.FinBankRecons.FirstOrDefaultAsync(r => r.ReconNo == bankReconNo && r.CompanyNo == companyNo && r.IsDeleted == 0, token)
                ?? throw new NotFoundException($"Reconciliation not found: {bankReconNo}");
            recon.IsDeleted = 1; recon.IsActive = 0;
            recon.DeletedBy = _ctx.CurrentUserNo(); recon.DeletedAt = DateTime.UtcNow;

            var lines = await _db.FinBankReconLines.Where(l => l.ReconNo == bankReconNo && l.IsDeleted == 0).ToListAsync(token);
            foreach (var l in lines) { l.IsDeleted = 1; l.IsActive = 0; l.DeletedBy = _ctx.CurrentUserNo(); l.DeletedAt = DateTime.UtcNow; }

            await _db.SaveChangesAsync(token);
        }, ct);
    }

    private static Fin1102ReconDto ToDto(FinBankRecon r) => new()
    {
        ReconNo = r.ReconNo,
        AccountNo = r.AccountNo,
        StatementDate = r.StatementDate,
        StatementBalance = r.StatementBalance,
        BookBalance = r.BookBalance,
        ClearedDebits = r.ClearedDebits,
        ClearedCredits = r.ClearedCredits,
        ReconciledBalance = r.ReconciledBalance,
        Difference = r.Difference,
        Status = r.Status,
        Remarks = r.Remarks
    };
}
