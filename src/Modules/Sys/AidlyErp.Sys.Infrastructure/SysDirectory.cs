using AidlyErp.Sys.Application.Interfaces;
using AidlyErp.Sys.Contracts;
using AidlyErp.Sys.Domain;
using Microsoft.EntityFrameworkCore;

namespace AidlyErp.Sys.Infrastructure;

/// <summary>SYS's side of <see cref="ISysBranchDirectory"/>.</summary>
internal sealed class SysBranchDirectory : ISysBranchDirectory
{
    private const short Deleted = 0;

    private readonly ISysDbContext _db;

    public SysBranchDirectory(ISysDbContext db) => _db = db;

    public Task<bool> ExistsAsync(long branchNo, CancellationToken cancellationToken = default) =>
        _db.Branches.AnyAsync(b => b.BranchNo == branchNo && b.IsDeleted == Deleted, cancellationToken);

    public async Task<BranchInfo?> FindAsync(long branchNo, CancellationToken cancellationToken = default) =>
        await _db.Branches
            .AsNoTracking()
            .Where(b => b.BranchNo == branchNo && b.IsDeleted == Deleted)
            .Select(b => new BranchInfo(b.BranchNo, b.BranchId, b.BranchName, b.CompanyNo))
            .FirstOrDefaultAsync(cancellationToken);
}

/// <summary>SYS's side of <see cref="ISysSettingsStore"/>.</summary>
internal sealed class SysSettingsStore : ISysSettingsStore
{
    private const short Deleted = 0;

    private readonly ISysDbContext _db;

    public SysSettingsStore(ISysDbContext db) => _db = db;

    public async Task<string?> GetAsync(long companyNo, string key, CancellationToken cancellationToken = default) =>
        await _db.Settings
            .AsNoTracking()
            .Where(s => s.CompanyNo == companyNo && s.SettingKey == key && s.IsDeleted == Deleted)
            .Select(s => s.SettingValue)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task UpsertAsync(long companyNo, string key, string value, short valueType,
                                  string? group, string? description,
                                  CancellationToken cancellationToken = default)
    {
        var entity = await _db.Settings
            .FirstOrDefaultAsync(s => s.CompanyNo == companyNo && s.SettingKey == key && s.IsDeleted == Deleted,
                cancellationToken);

        if (entity == null)
        {
            entity = new Setting
            {
                CompanyNo = companyNo,
                SettingKey = key,
                SettingGroup = group,
                IsActive = 1,
                IsDeleted = Deleted,
                CreatedAt = DateTime.UtcNow
            };
            _db.Settings.Add(entity);
        }

        entity.SettingValue = value;
        entity.ValueType = valueType;
        // Group and description are only filled in when absent — never overwritten.
        entity.SettingGroup ??= group;
        entity.Description ??= description;
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>SYS's side of <see cref="IDocSequenceGenerator"/>.</summary>
internal sealed class DocSequenceGenerator : IDocSequenceGenerator
{
    private readonly ISysDbContext _db;

    public DocSequenceGenerator(ISysDbContext db) => _db = db;

    public async Task<string> NextAsync(long companyNo, long? branchNo, string docType, string prefix,
                                        int width = 4, CancellationToken cancellationToken = default)
    {
        var seq = await _db.DocSequences.FirstOrDefaultAsync(
            s => s.CompanyNo == companyNo && s.BranchNo == branchNo && s.DocType == docType, cancellationToken);

        if (seq == null)
        {
            // First document of this type: create the sequence already pointing at 2, and hand out 1.
            _db.DocSequences.Add(new DocSequence
            {
                CompanyNo = companyNo,
                BranchNo = branchNo,
                DocType = docType,
                Prefix = prefix,
                NextVal = 2,
                IsDeleted = 0,
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync(cancellationToken);
            return prefix + 1L.ToString(new string('0', width));
        }

        var current = seq.NextVal;
        seq.NextVal = current + 1;
        await _db.SaveChangesAsync(cancellationToken);

        return prefix + current.ToString(new string('0', width));
    }
}

/// <summary>SYS's side of <see cref="IFinCalendar"/>.</summary>
internal sealed class FinCalendar : IFinCalendar
{
    private const short Deleted = 0;

    private readonly ISysDbContext _db;

    public FinCalendar(ISysDbContext db) => _db = db;

    private static readonly System.Linq.Expressions.Expression<Func<FinYear, FinYearInfo>> ToYear =
        y => new FinYearInfo(y.FinYearNo, y.CompanyNo, y.FinYearId, y.FinYearName, y.YearName,
            y.StartDate, y.EndDate, y.YearStatus, y.IsClosed, y.BranchNo);

    private static readonly System.Linq.Expressions.Expression<Func<FinYearDtl, FinPeriodInfo>> ToPeriod =
        p => new FinPeriodInfo(p.FinPeriodNo, p.FinYearNo, p.FinPeriodId, p.FinPeriodName,
            p.StartDate, p.EndDate, p.PeriodType, p.PeriodStatus, p.IsClosed);

    public async Task<FinYearInfo?> FindYearAsync(long finYearNo, CancellationToken cancellationToken = default) =>
        await _db.FinYears.AsNoTracking()
            .Where(y => y.FinYearNo == finYearNo && y.IsDeleted == Deleted)
            .Select(ToYear)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<FinYearInfo?> FindYearForDateAsync(long companyNo, DateOnly date,
                                                         CancellationToken cancellationToken = default) =>
        await _db.FinYears.AsNoTracking()
            .Where(y => y.CompanyNo == companyNo && y.StartDate <= date && y.EndDate >= date
                        && y.IsDeleted == Deleted)
            .Select(ToYear)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<FinYearInfo?> FindActiveYearForDateAsync(DateOnly date,
                                                               CancellationToken cancellationToken = default) =>
        await _db.FinYears.AsNoTracking()
            .Where(y => y.IsActive == 1 && y.IsDeleted == Deleted && y.StartDate <= date && y.EndDate >= date)
            .Select(ToYear)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<FinYearInfo?> FindAnyActiveYearAsync(CancellationToken cancellationToken = default) =>
        await _db.FinYears.AsNoTracking()
            .Where(y => y.IsActive == 1 && y.IsDeleted == Deleted)
            .Select(ToYear)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<FinYearInfo>> ListYearsAsync(long companyNo,
                                                                 CancellationToken cancellationToken = default) =>
        await _db.FinYears.AsNoTracking()
            .Where(y => y.CompanyNo == companyNo && y.IsDeleted == Deleted)
            .OrderByDescending(y => y.StartDate)
            .Select(ToYear)
            .ToListAsync(cancellationToken);

    public async Task<FinPeriodInfo?> FindPeriodAsync(long finPeriodNo,
                                                      CancellationToken cancellationToken = default) =>
        await _db.FinYearDtls.AsNoTracking()
            .Where(p => p.FinPeriodNo == finPeriodNo && p.IsDeleted == Deleted)
            .Select(ToPeriod)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<FinPeriodInfo?> FindPeriodForDateAsync(long finYearNo, DateOnly date,
                                                             CancellationToken cancellationToken = default) =>
        await _db.FinYearDtls.AsNoTracking()
            .Where(p => p.FinYearNo == finYearNo && p.StartDate <= date && p.EndDate >= date
                        && p.IsDeleted == Deleted)
            .Select(ToPeriod)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<FinPeriodInfo>> ListPeriodsAsync(IReadOnlyCollection<long> finYearNos,
                                                                     CancellationToken cancellationToken = default)
    {
        if (finYearNos.Count == 0) return Array.Empty<FinPeriodInfo>();

        return await _db.FinYearDtls.AsNoTracking()
            .Where(p => finYearNos.Contains(p.FinYearNo) && p.IsDeleted == Deleted)
            .OrderBy(p => p.StartDate)
            .Select(ToPeriod)
            .ToListAsync(cancellationToken);
    }

    public async Task ClosePeriodAsync(long finPeriodNo, long actingUserNo,
                                       CancellationToken cancellationToken = default)
    {
        var period = await _db.FinYearDtls
            .FirstOrDefaultAsync(p => p.FinPeriodNo == finPeriodNo && p.IsDeleted == Deleted, cancellationToken);

        if (period == null) return;

        period.PeriodStatus = 2; // Closed
        period.UpdatedBy = actingUserNo;
        period.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task CloseYearAsync(long finYearNo, long actingUserNo,
                                     CancellationToken cancellationToken = default)
    {
        var year = await _db.FinYears
            .FirstOrDefaultAsync(y => y.FinYearNo == finYearNo && y.IsDeleted == Deleted, cancellationToken);

        if (year == null) return;

        year.YearStatus = 2; // Closed
        year.UpdatedBy = actingUserNo;
        year.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>SYS's side of <see cref="IApprovalRequestReader"/>.</summary>
internal sealed class ApprovalRequestReader : IApprovalRequestReader
{
    private readonly ISysDbContext _db;

    public ApprovalRequestReader(ISysDbContext db) => _db = db;

    public async Task<short?> GetStatusAsync(long approvalRequestNo, CancellationToken cancellationToken = default) =>
        await _db.ApprovalRequests
            .AsNoTracking()
            .Where(r => r.ApprovalRequestNo == approvalRequestNo)
            .Select(r => (short?)r.Status)
            .FirstOrDefaultAsync(cancellationToken);
}
