using AidlyErp.Sys.Contracts;

namespace AidlyErp.Inv.Application.Services;

public interface IInvPeriodResolver
{
    Task<FinYearInfo> ResolveOpenFinYearAsync(long branchNo, DateTime date, CancellationToken ct = default);
}

/// <summary>
/// Resolves the fiscal year a stock movement falls into. The calendar is SYS master data, so it is
/// read through <see cref="IFinCalendar"/> rather than by querying <c>sys_fin_year</c> from INV.
/// </summary>
public class InvPeriodResolver : IInvPeriodResolver
{
    private readonly IFinCalendar _calendar;

    public InvPeriodResolver(IFinCalendar calendar) => _calendar = calendar;

    public async Task<FinYearInfo> ResolveOpenFinYearAsync(long branchNo, DateTime date,
                                                           CancellationToken ct = default)
    {
        var targetDate = DateOnly.FromDateTime(date);

        var year = await _calendar.FindActiveYearForDateAsync(targetDate, ct)
                   ?? await _calendar.FindAnyActiveYearAsync(ct);

        // Same last-resort fallback as before: a synthetic calendar year, so posting never hard-fails
        // purely because no fiscal year has been configured yet.
        return year ?? new FinYearInfo(
            FinYearNo: 1,
            CompanyNo: 0,
            FinYearId: string.Empty,
            FinYearName: $"{DateTime.UtcNow.Year}",
            YearName: $"{DateTime.UtcNow.Year}",
            StartDate: new DateOnly(DateTime.UtcNow.Year, 1, 1),
            EndDate: new DateOnly(DateTime.UtcNow.Year, 12, 31),
            YearStatus: 1,
            IsClosed: 0,
            BranchNo: null);
    }
}
