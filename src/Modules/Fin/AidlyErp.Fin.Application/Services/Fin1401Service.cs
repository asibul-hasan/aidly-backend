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
    Task ClosePeriodAsync(long periodNo, CancellationToken ct = default);
    Task<Fin1401CloseResultDto> CloseFiscalYearAsync(Fin1401CloseRequestDto request, CancellationToken ct = default);
}

public class Fin1401Service : IFin1401Service
{
    private readonly IFinDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    private readonly IFinCalendar _calendar;
    private readonly IFinReportService _reportService;
    private readonly IFin1101Service _voucherService;

    public Fin1401Service(IFinDbContext db, ICompanyBranchContext ctx, IFinReportService reportService, IFin1101Service voucherService,
                          IFinCalendar calendar)
    {
        _db = db;
        _ctx = ctx;
        _reportService = reportService;
        _voucherService = voucherService;
        _calendar = calendar;
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
                    PeriodName = p.FinPeriodName,
                    StartDate = p.StartDate.ToDateTime(TimeOnly.MinValue),
                    EndDate = p.EndDate.ToDateTime(TimeOnly.MinValue),
                    IsClosed = p.PeriodStatus == 2 ? (short)1 : (short)0
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

    public async Task ClosePeriodAsync(long periodNo, CancellationToken ct = default)
    {
        _ = await _calendar.FindPeriodAsync(periodNo, ct)
            ?? throw new NotFoundException($"Period not found: {periodNo}");

        await _calendar.ClosePeriodAsync(periodNo, _ctx.CurrentUserNo(), ct);
    }

    public async Task<Fin1401CloseResultDto> CloseFiscalYearAsync(Fin1401CloseRequestDto request, CancellationToken ct = default)
    {
        var year = await _calendar.FindYearAsync(request.FinYearNo, ct)
            ?? throw new NotFoundException($"Fiscal Year not found: {request.FinYearNo}");

        if (year.YearStatus == 2) throw new ValidationException("Fiscal Year is already closed");

        var pnl = await _reportService.GetPnlAsync(year.StartDate.ToDateTime(TimeOnly.MinValue),
                                                   year.EndDate.ToDateTime(TimeOnly.MinValue), ct);
        decimal netIncome = pnl.NetProfit;

        var vTypeNo = await _voucherService.ResolveSystemJournalTypeAsync(year.CompanyNo, ct);

        var lines = new List<Fin1101VoucherLineDto>();

        if (netIncome != 0)
        {
            // Closing entry: transfer net income to retained earnings.
            // Requires two lines for balanced debit/credit.
            if (netIncome > 0)
            {
                // Profit: Credit retained earnings, Debit income summary
                lines.Add(new Fin1101VoucherLineDto { AccountNo = request.RetainedEarningsAccountNo, Debit = 0m, Credit = netIncome, LineNarration = "Net income transferred to retained earnings" });
                lines.Add(new Fin1101VoucherLineDto { AccountNo = request.IncomeSummaryAccountNo, Debit = netIncome, Credit = 0m, LineNarration = "Income summary closed" });
            }
            else
            {
                // Loss: Debit retained earnings, Credit income summary
                decimal absNet = Math.Abs(netIncome);
                lines.Add(new Fin1101VoucherLineDto { AccountNo = request.RetainedEarningsAccountNo, Debit = absNet, Credit = 0m, LineNarration = "Net loss transferred to retained earnings" });
                lines.Add(new Fin1101VoucherLineDto { AccountNo = request.IncomeSummaryAccountNo, Debit = 0m, Credit = absNet, LineNarration = "Income summary closed" });
            }
        }

        long voucherNo = 0;
        string voucherId = "YEAR-END-CLOSE";

        if (lines.Count > 0)
        {
            long branchNo = _ctx.CurrentBranchNo() ?? 1;
            voucherNo = await _voucherService.PostSystemVoucherAsync(
                year.CompanyNo,
                branchNo,
                vTypeNo,
                year.EndDate.ToDateTime(TimeOnly.MinValue),
                request.Narration ?? $"Year-End Closing for {year.YearName}",
                5,
                "YearEndClose",
                year.FinYearNo,
                lines,
                ct);

            var v = await _db.FinVouchers.FirstOrDefaultAsync(x => x.VoucherNo == voucherNo, ct);
            if (v != null) voucherId = v.VoucherId;
        }

        // The fiscal calendar is SYS-owned, so the close is applied through the contract.
        await _calendar.CloseYearAsync(year.FinYearNo, _ctx.CurrentUserNo(), ct);

        return new Fin1401CloseResultDto
        {
            FinYearNo = year.FinYearNo,
            ClosingVoucherNo = voucherNo,
            ClosingVoucherId = voucherId,
            NetIncomeTransferred = netIncome
        };
    }
}
