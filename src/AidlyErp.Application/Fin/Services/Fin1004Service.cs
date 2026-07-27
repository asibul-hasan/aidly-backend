using Microsoft.EntityFrameworkCore;
using AidlyErp.Application.Common.Exceptions;
using AidlyErp.Application.Common.Interfaces;
using AidlyErp.Application.Common.Security;
using AidlyErp.Application.Fin.Dto;
using AidlyErp.Domain.Fin;

namespace AidlyErp.Application.Fin.Services;

// ═══════════════════════════════════════════════════════════════════════════
// Fin1004Service — Opening Balances
// ═══════════════════════════════════════════════════════════════════════════

public interface IFin1004Service
{
    Task<List<Fin1004AccountDto>> GetAccountsWithOpeningBalanceAsync(CancellationToken ct = default);
    Task<Fin1004ResultDto> SaveOpeningBalancesAsync(Fin1004OpeningBalanceDto dto, CancellationToken ct = default);
}

public class Fin1004Service : IFin1004Service
{
    private readonly IApplicationDbContext _db;
    private readonly ICompanyBranchContext _ctx;

    public Fin1004Service(IApplicationDbContext db, ICompanyBranchContext ctx)
    {
        _db = db;
        _ctx = ctx;
    }

    public async Task<List<Fin1004AccountDto>> GetAccountsWithOpeningBalanceAsync(CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? 0;
        return await _db.FinAccounts
            .AsNoTracking()
            .Where(a => a.CompanyNo == companyNo && a.IsDeleted == 0 && a.IsPostable == 1)
            .OrderBy(a => a.AccountCode)
            .Select(a => new Fin1004AccountDto
            {
                AccountNo = a.AccountNo,
                AccountCode = a.AccountCode,
                AccountName = a.AccountName,
                NormalBalance = a.NormalBalance,
                OpeningBalance = a.OpeningBalance,
                OpeningDrCr = a.OpeningDrCr
            })
            .ToListAsync(ct);
    }

    public async Task<Fin1004ResultDto> SaveOpeningBalancesAsync(Fin1004OpeningBalanceDto dto, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
        long branchNo = _ctx.CurrentBranchNo() ?? throw new ValidationException("Branch context is required");

        if (dto.Lines == null || dto.Lines.Count == 0)
            throw new ValidationException("Opening balance lines are required");

        decimal totalDebit = 0m;
        decimal totalCredit = 0m;

        foreach (var line in dto.Lines)
        {
            var acc = await _db.FinAccounts.FirstOrDefaultAsync(a => a.AccountNo == line.AccountNo && a.CompanyNo == companyNo && a.IsDeleted == 0, ct)
                ?? throw new NotFoundException($"Account not found: {line.AccountNo}");

            acc.OpeningBalance = line.OpeningBalance;
            acc.OpeningDrCr = line.OpeningDrCr.ToLowerInvariant();
            acc.UpdatedBy = _ctx.CurrentUserNo(); acc.UpdatedAt = DateTime.UtcNow;

            if (acc.OpeningDrCr == "dr") totalDebit += acc.OpeningBalance;
            else totalCredit += acc.OpeningBalance;
        }

        await _db.SaveChangesAsync(ct);

        return new Fin1004ResultDto
        {
            VoucherNo = 0,
            VoucherId = "OPENING-BAL",
            TotalDebit = totalDebit,
            TotalCredit = totalCredit
        };
    }
}
