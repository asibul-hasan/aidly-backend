using Microsoft.EntityFrameworkCore;
using AidlyErp.Application.Common.Exceptions;
using AidlyErp.Application.Common.Interfaces;
using AidlyErp.Application.Common.Security;
using AidlyErp.Application.Fin.Dto;
using AidlyErp.Domain.Fin;

namespace AidlyErp.Application.Fin.Services;

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
    private readonly IApplicationDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    private readonly IFinReportService _reportService;
    private readonly IFin1101Service _voucherService;

    public Fin1401Service(IApplicationDbContext db, ICompanyBranchContext ctx, IFinReportService reportService, IFin1101Service voucherService)
    {
        _db = db;
        _ctx = ctx;
        _reportService = reportService;
        _voucherService = voucherService;
    }

    public async Task<List<Fin1401YearDto>> GetYearsAndPeriodsAsync(CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? 0;

        var years = await _db.FinYears.AsNoTracking()
            .Where(y => y.CompanyNo == companyNo && y.IsDeleted == 0)
            .OrderByDescending(y => y.StartDate)
            .ToListAsync(ct);

        var yearNos = years.Select(y => y.FinYearNo).ToList();
        var periods = await _db.FinYearDtls.AsNoTracking()
            .Where(p => yearNos.Contains(p.FinYearNo) && p.IsDeleted == 0)
            .OrderBy(p => p.StartDate)
            .ToListAsync(ct);

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
        var period = await _db.FinYearDtls.FirstOrDefaultAsync(p => p.FinPeriodNo == periodNo && p.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Period not found: {periodNo}");

        period.PeriodStatus = 2; // Closed
        period.UpdatedBy = _ctx.CurrentUserNo(); period.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<Fin1401CloseResultDto> CloseFiscalYearAsync(Fin1401CloseRequestDto request, CancellationToken ct = default)
    {
        var year = await _db.FinYears.FirstOrDefaultAsync(y => y.FinYearNo == request.FinYearNo && y.IsDeleted == 0, ct)
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

        year.YearStatus = 2; // Closed
        year.UpdatedBy = _ctx.CurrentUserNo(); year.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return new Fin1401CloseResultDto
        {
            FinYearNo = year.FinYearNo,
            ClosingVoucherNo = voucherNo,
            ClosingVoucherId = voucherId,
            NetIncomeTransferred = netIncome
        };
    }
}
