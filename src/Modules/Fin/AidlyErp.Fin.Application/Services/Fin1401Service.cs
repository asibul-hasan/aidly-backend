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
// Fin1401Service — Year-End & Period Closing Engine
// ═══════════════════════════════════════════════════════════════════════════

public interface IFin1401Service
{
    Task<List<Fin1401YearDto>> GetYearsAndPeriodsAsync(CancellationToken ct = default);
    Task<List<Fin1401PeriodDto>> GetPeriodsAsync(long finYearNo, CancellationToken ct = default);
    Task SetPeriodStatusAsync(long periodNo, short status, string? reason = null, CancellationToken ct = default);
    Task<Fin1401CloseResultDto> CloseFiscalYearAsync(Fin1401CloseRequestDto request, CancellationToken ct = default);
}

public class Fin1401Service : IFin1401Service
{
    private readonly IFinDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    private readonly IFinCalendar _calendar;
    private readonly IFinReportService _reportService;
    private readonly IFin1101Service _voucherService;
    private readonly IUnitOfWork<IFinDbContext> _uow;

    public Fin1401Service(IFinDbContext db, ICompanyBranchContext ctx, IFinReportService reportService, IFin1101Service voucherService,
                          IFinCalendar calendar, IUnitOfWork<IFinDbContext> uow)
    {
        _db = db;
        _ctx = ctx;
        _reportService = reportService;
        _voucherService = voucherService;
        _calendar = calendar;
        _uow = uow;
    }

    public async Task<List<Fin1401YearDto>> GetYearsAndPeriodsAsync(CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? 0;

        var years = await _calendar.ListYearsAsync(companyNo, ct);

        var yearNos = years.Select(y => y.FinYearNo).ToList();
        var periods = await _calendar.ListPeriodsAsync(yearNos, ct);

        var result = new List<Fin1401YearDto>();

        foreach (var y in years)
        {
            var pList = periods.Where(p => p.FinYearNo == y.FinYearNo)
                .Select(p => new Fin1401PeriodDto
                {
                    FinPeriodNo = p.FinPeriodNo,
                    FinYearNo = p.FinYearNo,
                    FinPeriodId = p.FinPeriodId,
                    PeriodName = p.FinPeriodName,
                    StartDate = p.StartDate.ToDateTime(TimeOnly.MinValue),
                    EndDate = p.EndDate.ToDateTime(TimeOnly.MinValue),
                    PeriodStatus = p.PeriodStatus,
                    IsClosed = p.PeriodStatus != 1 ? (short)1 : (short)0
                }).ToList();

            result.Add(new Fin1401YearDto
            {
                FinYearNo = y.FinYearNo,
                YearName = y.YearName,
                StartDate = y.StartDate.ToDateTime(TimeOnly.MinValue),
                EndDate = y.EndDate.ToDateTime(TimeOnly.MinValue),
                IsClosed = y.YearStatus == 2 ? (short)1 : (short)0,
                Periods = pList
            });
        }

        return result;
    }

    public async Task<List<Fin1401PeriodDto>> GetPeriodsAsync(long finYearNo, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");

        var year = await _calendar.FindYearAsync(finYearNo, ct)
            ?? throw new NotFoundException($"Financial year not found: {finYearNo}");

        if (year.CompanyNo != companyNo) throw new ValidationException("Financial year belongs to another company");

        var periods = await _calendar.ListPeriodsAsync(new[] { finYearNo }, ct);

        return periods.Select(p => new Fin1401PeriodDto
        {
            FinPeriodNo = p.FinPeriodNo,
            FinYearNo = p.FinYearNo,
            FinPeriodId = p.FinPeriodId,
            PeriodName = p.FinPeriodName,
            StartDate = p.StartDate.ToDateTime(TimeOnly.MinValue),
            EndDate = p.EndDate.ToDateTime(TimeOnly.MinValue),
            PeriodStatus = p.PeriodStatus,
            IsClosed = p.PeriodStatus != 1 ? (short)1 : (short)0
        }).ToList();
    }

    public async Task SetPeriodStatusAsync(long periodNo, short status, string? reason, CancellationToken ct = default)
    {
        await _uow.ExecuteAsync(async token =>
        {
            if (status < 1 || status > 3)
                throw new ValidationException("Status must be 1=Open, 2=Closed or 3=Locked");

            var period = await _calendar.FindPeriodAsync(periodNo, token)
                ?? throw new NotFoundException($"Period not found: {periodNo}");

            // Irreversibility guard: Locked(3) can NEVER be changed from the UI
            if (period.PeriodStatus == 3)
                throw new ValidationException("Locked periods cannot be re-opened. This is the post-year-end seal.");

            // Re-open guard: Closed(2) -> Open(1) requires a reason
            // The reason is captured in the API request body and logged by AuditLogMiddleware.
            if (period.PeriodStatus == 2 && status == 1)
            {
                if (string.IsNullOrWhiteSpace(reason))
                    throw new ValidationException("A reason is required to re-open a closed period");
            }

            // Check parent year is not closed
            var year = await _calendar.FindYearAsync(period.FinYearNo, token);
            if (year != null && year.YearStatus == 2)
                throw new ValidationException("The financial year is closed — periods are locked");

            await _calendar.SetPeriodStatusAsync(periodNo, status, _ctx.CurrentUserNo(), token);
        }, ct);
    }

    public async Task<Fin1401CloseResultDto> CloseFiscalYearAsync(Fin1401CloseRequestDto request, CancellationToken ct = default)
    {
        return await _uow.ExecuteAsync(async token =>
        {
            long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
            long branchNo = _ctx.CurrentBranchNo() ?? throw new ValidationException("Branch context is required");

            if (request.FinYearNo <= 0) throw new ValidationException("Financial year is required");
            if (request.RetainedEarningsAccountNo <= 0) throw new ValidationException("A retained-earnings account is required");
            if (request.IncomeSummaryAccountNo <= 0) throw new ValidationException("An income-summary account is required");

            var year = await _calendar.FindYearAsync(request.FinYearNo, token)
                ?? throw new NotFoundException($"Financial year not found: {request.FinYearNo}");

            if (year.CompanyNo != companyNo) throw new ValidationException("Financial year belongs to another company");
            if (year.YearStatus == 2) throw new ValidationException("This financial year is already closed");

            // Guard: reject if any period in the year is still Open
            var periods = await _calendar.ListPeriodsAsync(new[] { request.FinYearNo }, token);
            var openPeriods = periods.Where(p => p.PeriodStatus == 1).ToList();
            if (openPeriods.Count > 0)
            {
                var names = string.Join(", ", openPeriods.Select(p => p.FinPeriodName));
                throw new ValidationException($"Cannot close year: the following periods are still Open: {names}");
            }

            // Idempotency guard — prevent double-close
            if (await _voucherService.AlreadyPostedAsync("FIN_YEAR_CLOSE", request.FinYearNo, companyNo, token))
                throw new ValidationException("A year-end close voucher already exists for this year");

            // Validate retained earnings account
            var reAccount = await _db.FinAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.AccountNo == request.RetainedEarningsAccountNo && a.CompanyNo == companyNo && a.IsDeleted == 0, token)
                ?? throw new NotFoundException($"Account not found: {request.RetainedEarningsAccountNo}");
            if (reAccount.IsPostable != 1) throw new ValidationException("Retained-earnings account must be postable");

            // Validate income summary account
            var isAccount = await _db.FinAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.AccountNo == request.IncomeSummaryAccountNo && a.CompanyNo == companyNo && a.IsDeleted == 0, token)
                ?? throw new NotFoundException($"Account not found: {request.IncomeSummaryAccountNo}");
            if (isAccount.IsPostable != 1) throw new ValidationException("Income summary account must be postable");

            // Use closing voucher type (baseKind=8) if available, fallback to journal
            var vTypeNo = await _voucherService.ResolveSystemVoucherTypeAsync(companyNo, 8, token);

            // Get windowed P&L for the year
            var pnl = await _reportService.GetPnlAsync(year.StartDate.ToDateTime(TimeOnly.MinValue),
                                                       year.EndDate.ToDateTime(TimeOnly.MinValue), null, token);

            var lines = new List<Fin1101VoucherLineDto>();
            decimal netIncome = pnl.NetProfit;

            // Close each revenue account: Dr revenue for its balance (zero it out)
            foreach (var row in pnl.RevenueRows)
            {
                if (row.Amount == 0) continue;
                lines.Add(new Fin1101VoucherLineDto
                {
                    AccountNo = row.AccountNo,
                    Debit = row.Amount,
                    Credit = 0m,
                    LineNarration = $"Closing {row.AccountCode} {row.AccountName}"
                });
            }

            // Close each expense account: Cr expense for its balance (zero it out)
            foreach (var row in pnl.ExpenseRows)
            {
                if (row.Amount == 0) continue;
                lines.Add(new Fin1101VoucherLineDto
                {
                    AccountNo = row.AccountNo,
                    Debit = 0m,
                    Credit = row.Amount,
                    LineNarration = $"Closing {row.AccountCode} {row.AccountName}"
                });
            }

            // Balancing line to Income Summary or Retained Earnings
            if (netIncome != 0)
            {
                if (netIncome > 0)
                {
                    // Profit: Cr retained earnings (or Cr income summary if supplied)
                    lines.Add(new Fin1101VoucherLineDto
                    {
                        AccountNo = request.IncomeSummaryAccountNo > 0 ? request.IncomeSummaryAccountNo : request.RetainedEarningsAccountNo,
                        Debit = 0m,
                        Credit = netIncome,
                        LineNarration = "Net income for the year"
                    });
                }
                else
                {
                    // Loss: Dr retained earnings (or Dr income summary if supplied)
                    decimal absNet = Math.Abs(netIncome);
                    lines.Add(new Fin1101VoucherLineDto
                    {
                        AccountNo = request.IncomeSummaryAccountNo > 0 ? request.IncomeSummaryAccountNo : request.RetainedEarningsAccountNo,
                        Debit = absNet,
                        Credit = 0m,
                        LineNarration = "Net loss for the year"
                    });
                }
            }

            long voucherNo = 0;
            string voucherId = "YEAR-END-CLOSE";

            if (lines.Count >= 2)
            {
                voucherNo = await _voucherService.PostSystemVoucherAsync(
                    year.CompanyNo,
                    branchNo,
                    vTypeNo,
                    year.EndDate.ToDateTime(TimeOnly.MinValue),
                    request.Narration ?? $"Year-End Closing for {year.YearName}",
                    5, // SRC_FIN
                    "FIN_YEAR_CLOSE",
                    year.FinYearNo,
                    lines,
                    token);

                var v = await _db.FinVouchers.FirstOrDefaultAsync(x => x.VoucherNo == voucherNo, token);
                if (v != null) voucherId = v.VoucherId;
            }

            // Lock every period in the year, then close the year
            foreach (var p in periods)
            {
                await _calendar.SetPeriodStatusAsync(p.FinPeriodNo, 3, _ctx.CurrentUserNo(), token);
            }
            await _calendar.CloseYearAsync(year.FinYearNo, _ctx.CurrentUserNo(), token);

            return new Fin1401CloseResultDto
            {
                FinYearNo = year.FinYearNo,
                ClosingVoucherNo = voucherNo,
                ClosingVoucherId = voucherId,
                NetIncomeTransferred = netIncome
            };
        }, ct);
    }
}
