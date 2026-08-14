using AidlyErp.Shared.Core.Exceptions;
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

        // Refuse rather than fabricate. The migration used to synthesise a year here so posting
        // never hard-failed on an unconfigured calendar, but that stamped every ledger row with
        // fin_year_no = 1 — a year that may belong to another company or not exist at all, and
        // which nothing downstream can tell apart from a real one.
        return year ?? throw new ValidationException(
            $"No financial year covers the date {targetDate:yyyy-MM-dd} — configure the fiscal calendar first");
    }
}
