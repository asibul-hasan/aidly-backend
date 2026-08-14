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
// Fin1004Service — Opening Balances
// Port of Java Fin1004Service — posts opening balances as a system voucher.
// ═══════════════════════════════════════════════════════════════════════════

public interface IFin1004Service
{
    Task<List<Fin1004AccountDto>> GetAccountsWithOpeningBalanceAsync(CancellationToken ct = default);
    Task<Fin1004ResultDto> SaveOpeningBalancesAsync(Fin1004OpeningBalanceDto dto, CancellationToken ct = default);
}

public class Fin1004Service : IFin1004Service
{
    private readonly IFinDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    private readonly IFinCalendar _calendar;
    private readonly IFin1101Service _voucherService;
    private readonly IUnitOfWork<IFinDbContext> _uow;

    private const short OpeningKind = 7;  // baseKind for Opening voucher type
    private const short SrcFin = 5;
    private const string SrcDocType = "FIN_OPENING";
    private const decimal Cent = 0.0001m;

    public Fin1004Service(IFinDbContext db, ICompanyBranchContext ctx, IFinCalendar calendar, IFin1101Service voucherService, IUnitOfWork<IFinDbContext> uow)
    {
        _db = db;
        _ctx = ctx;
        _calendar = calendar;
        _voucherService = voucherService;
        _uow = uow;
    }

    public async Task<List<Fin1004AccountDto>> GetAccountsWithOpeningBalanceAsync(CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? 0;
        // Java: returns only active AND postable accounts
        return await _db.FinAccounts
            .AsNoTracking()
            .Where(a => a.CompanyNo == companyNo && a.IsDeleted == 0 && a.IsActive == 1 && a.IsPostable == 1)
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
        return await _uow.ExecuteAsync(async token =>
        {
            long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
            long branchNo = _ctx.CurrentBranchNo() ?? throw new ValidationException("Branch context is required");

            if (dto.Lines == null || dto.Lines.Count == 0)
                throw new ValidationException("Enter at least one opening line");

            // Batch-load all referenced accounts
            var accountNos = dto.Lines.Select(l => l.AccountNo).Distinct().ToList();
            var accounts = await _db.FinAccounts.AsNoTracking()
                .Where(a => accountNos.Contains(a.AccountNo) && a.IsDeleted == 0)
                .ToDictionaryAsync(a => a.AccountNo, token);

            // Validate each account
            foreach (var line in dto.Lines)
            {
                if (!accounts.TryGetValue(line.AccountNo, out var acc))
                    throw new NotFoundException($"Account not found: {line.AccountNo}");
                if (acc.CompanyNo != companyNo)
                    throw new ValidationException("Account belongs to another company");
                if (acc.IsActive != 1)
                    throw new ValidationException($"Account {acc.AccountCode} is inactive");
                if (acc.IsPostable != 1)
                    throw new ValidationException($"Account {acc.AccountCode} is not postable");
            }

            // Build balanced voucher lines
            var voucherLines = new List<Fin1101VoucherLineDto>();
            decimal totalDebit = 0m;
            decimal totalCredit = 0m;

            foreach (var line in dto.Lines)
            {
                decimal amount = line.OpeningBalance;
                if (amount < 0) throw new ValidationException("Opening amount cannot be negative");
                if (amount == 0) continue;

                string drCr = (line.OpeningDrCr ?? "dr").ToLowerInvariant();
                if (drCr != "dr" && drCr != "cr")
                    throw new ValidationException("Opening line type must be dr or cr");

                if (drCr == "dr")
                {
                    voucherLines.Add(new Fin1101VoucherLineDto
                    {
                        AccountNo = line.AccountNo,
                        Debit = amount,
                        Credit = 0m,
                        LineNarration = line.LineNarration
                    });
                    totalDebit += amount;
                }
                else
                {
                    voucherLines.Add(new Fin1101VoucherLineDto
                    {
                        AccountNo = line.AccountNo,
                        Debit = 0m,
                        Credit = amount,
                        LineNarration = line.LineNarration
                    });
                    totalCredit += amount;
                }
            }

            if (voucherLines.Count == 0)
                throw new ValidationException("All opening lines are zero — nothing to post");

            // Penny-tolerance balance check
            if (Math.Abs(totalDebit - totalCredit) >= Cent)
                throw new ValidationException("Opening balance must be balanced: total debit and total credit must be equal");

            if (voucherLines.Count < 2)
                throw new ValidationException("Opening batch needs at least two GL lines once balanced");

            // Resolve open financial year
            var voucherDate = dto.AsOfDate != default ? dto.AsOfDate : DateTime.Today;
            var voucherDay = DateOnly.FromDateTime(voucherDate);
            var finYear = await _calendar.FindYearForDateAsync(companyNo, voucherDay, token)
                ?? throw new ValidationException($"No open financial year covers {voucherDay} — configure it in Financial Year Setup (SYS_1003)");

            // Idempotency guard — prevent duplicate opening voucher per year per branch.
            // Check directly on fin_voucher since AlreadyPostedAsync is company-scoped.
            bool alreadyPosted = await _db.FinVouchers.AnyAsync(v =>
                v.CompanyNo == companyNo && v.BranchNo == branchNo &&
                v.SourceDocType == SrcDocType && v.SourceDocNo == finYear.FinYearNo &&
                v.Status == 2 && v.IsDeleted == 0, token);
            if (alreadyPosted)
                throw new ValidationException($"Opening balance already posted for {finYear.YearName} in this branch");

            // Resolve opening voucher type
            var vTypeNo = await _voucherService.ResolveSystemVoucherTypeAsync(companyNo, OpeningKind, token);

            // Post via the voucher engine
            string narration = dto.Narration ?? $"Opening balances as of {voucherDay:yyyy-MM-dd}";

            long voucherNo = await _voucherService.PostSystemVoucherAsync(
                companyNo, branchNo, vTypeNo,
                voucherDate,
                narration,
                SrcFin, SrcDocType, finYear.FinYearNo,
                voucherLines, token);

            // Zero the seed on every touched account so it can never be double-read.
            foreach (var line in dto.Lines)
            {
                var acc = await _db.FinAccounts.FirstOrDefaultAsync(a => a.AccountNo == line.AccountNo && a.CompanyNo == companyNo && a.IsDeleted == 0, token);
                if (acc != null)
                {
                    acc.OpeningBalance = 0m;
                    acc.OpeningDrCr = "dr";
                }
            }
            await _db.SaveChangesAsync(token);

            var v = await _db.FinVouchers.AsNoTracking().FirstOrDefaultAsync(x => x.VoucherNo == voucherNo, token);

            return new Fin1004ResultDto
            {
                VoucherNo = voucherNo,
                VoucherId = v?.VoucherId ?? "OPENING-BAL",
                TotalDebit = totalDebit,
                TotalCredit = totalCredit
            };
        }, ct);
    }
}
