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
    Task<Fin1102WorksheetDto> GetWorksheetAsync(long accountNo, DateTime statementDate, CancellationToken ct = default);
    Task<Fin1102ReconDto> SaveReconciliationAsync(Fin1102SaveDto dto, CancellationToken ct = default);
    Task<List<Fin1102ReconDto>> GetReconHistoryAsync(long accountNo, CancellationToken ct = default);
}

public class Fin1102Service : IFin1102Service
{
    private readonly IFinDbContext _db;
    private readonly ICompanyBranchContext _ctx;

    public Fin1102Service(IFinDbContext db, ICompanyBranchContext ctx)
    {
        _db = db;
        _ctx = ctx;
    }

    public async Task<Fin1102WorksheetDto> GetWorksheetAsync(long accountNo, DateTime statementDate, CancellationToken ct = default)
    {
        var acc = await _db.FinAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.AccountNo == accountNo && a.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Account not found: {accountNo}");

        var clearedLedgerNos = await _db.FinBankReconLines.AsNoTracking()
            .Where(l => l.IsCleared == 1 && l.IsDeleted == 0)
            .Select(l => l.LedgerNo)
            .ToHashSetAsync(ct);

        var ledgerLines = await _db.FinLedgers.AsNoTracking()
            .Where(l => l.AccountNo == accountNo && l.VoucherDate <= statementDate && l.IsDeleted == 0)
            .OrderBy(l => l.VoucherDate)
            .ToListAsync(ct);

        var voucherNos = ledgerLines.Select(l => l.VoucherNo).Distinct().ToList();
        var voucherIds = await _db.FinVouchers.AsNoTracking().Where(v => voucherNos.Contains(v.VoucherNo)).ToDictionaryAsync(v => v.VoucherNo, v => v.VoucherId, ct);
        var narrations = await _db.FinVouchers.AsNoTracking().Where(v => voucherNos.Contains(v.VoucherNo)).ToDictionaryAsync(v => v.VoucherNo, v => v.Narration, ct);

        decimal bookBalance = acc.OpeningBalance;

        var lineDtos = new List<Fin1102LedgerLineDto>();
        foreach (var l in ledgerLines)
        {
            decimal deb = l.Debit;
            decimal cred = l.Credit;
            bookBalance += (deb - cred);

            if (!clearedLedgerNos.Contains(l.LedgerNo))
            {
                lineDtos.Add(new Fin1102LedgerLineDto
                {
                    LedgerNo = l.LedgerNo,
                    VoucherNo = l.VoucherNo,
                    VoucherId = voucherIds.ContainsKey(l.VoucherNo) ? voucherIds[l.VoucherNo] : null,
                    TxnDate = l.VoucherDate,
                    Narration = narrations.ContainsKey(l.VoucherNo) ? narrations[l.VoucherNo] : null,
                    Debit = deb,
                    Credit = cred,
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
        var worksheet = await GetWorksheetAsync(dto.AccountNo, dto.StatementDate, ct);

        var clearedLines = worksheet.Lines.Where(l => dto.ClearedLedgerNos.Contains(l.LedgerNo)).ToList();

        decimal clearedDebits = clearedLines.Sum(l => l.Debit);
        decimal clearedCredits = clearedLines.Sum(l => l.Credit);

        decimal reconciledBalance = worksheet.BookBalance + clearedCredits - clearedDebits;
        decimal diff = dto.StatementBalance - reconciledBalance;

        var recon = new FinBankRecon
        {
            AccountNo = dto.AccountNo,
            StatementDate = dto.StatementDate,
            StatementBalance = dto.StatementBalance,
            BookBalance = worksheet.BookBalance,
            ClearedDebits = clearedDebits,
            ClearedCredits = clearedCredits,
            ReconciledBalance = reconciledBalance,
            Difference = diff,
            Status = Math.Abs(diff) < 0.01m ? (short)2 : (short)1, // 2=Completed 1=Draft
            Remarks = dto.Remarks,
            IsActive = 1, IsDeleted = 0,
            CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow
        };

        _db.FinBankRecons.Add(recon);
        await _db.SaveChangesAsync(ct);

        foreach (var l in clearedLines)
        {
            var rLine = new FinBankReconLine
            {
                ReconNo = recon.ReconNo,
                LedgerNo = l.LedgerNo,
                VoucherDtlNo = 0,
                ClearedDate = dto.StatementDate,
                IsCleared = 1,
                IsActive = 1, IsDeleted = 0,
                CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow
            };
            _db.FinBankReconLines.Add(rLine);
        }

        await _db.SaveChangesAsync(ct);

        return ToDto(recon);
    }

    public async Task<List<Fin1102ReconDto>> GetReconHistoryAsync(long accountNo, CancellationToken ct = default)
    {
        return await _db.FinBankRecons
            .AsNoTracking()
            .Where(r => r.AccountNo == accountNo && r.IsDeleted == 0)
            .OrderByDescending(r => r.StatementDate)
            .Select(r => ToDto(r))
            .ToListAsync(ct);
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
