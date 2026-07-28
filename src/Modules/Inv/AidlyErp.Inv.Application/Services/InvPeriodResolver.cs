using AidlyErp.Inv.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using AidlyErp.Shared.Core.Abstractions;
using AidlyErp.Shared.Contracts;
using AidlyErp.Sys.Contracts;
using AidlyErp.Sys.Domain;

namespace AidlyErp.Inv.Application.Services;

public interface IInvPeriodResolver
{
    Task<FinYear> ResolveOpenFinYearAsync(long branchNo, DateTime date, CancellationToken ct = default);
}

public class InvPeriodResolver : IInvPeriodResolver
{
    private readonly IInvDbContext _db;

    public InvPeriodResolver(IInvDbContext db) => _db = db;

    public async Task<FinYear> ResolveOpenFinYearAsync(long branchNo, DateTime date, CancellationToken ct = default)
    {
        DateOnly targetDate = DateOnly.FromDateTime(date);
        var year = await _db.FinYears
            .FirstOrDefaultAsync(y => y.IsActive == 1 && y.IsDeleted == 0 && y.StartDate <= targetDate && y.EndDate >= targetDate, ct);

        if (year != null) return year;

        year = await _db.FinYears.FirstOrDefaultAsync(y => y.IsActive == 1 && y.IsDeleted == 0, ct);
        return year ?? new FinYear { FinYearNo = 1, YearName = $"{DateTime.UtcNow.Year}", StartDate = new DateOnly(DateTime.UtcNow.Year, 1, 1), EndDate = new DateOnly(DateTime.UtcNow.Year, 12, 31) };
    }
}
