namespace AidlyErp.Sys.Contracts;

// ─────────────────────────────────────────────────────────────────────────────
// Public read models. Deliberately not the SYS entities — those stay internal to
// the module so consumers never bind to SYS's schema.
// ─────────────────────────────────────────────────────────────────────────────

public sealed record BranchInfo(long BranchNo, string? BranchId, string? BranchName, long? CompanyNo);

public sealed record FinYearInfo(
    long FinYearNo,
    long CompanyNo,
    string FinYearId,
    string FinYearName,
    string YearName,
    DateOnly StartDate,
    DateOnly EndDate,
    short YearStatus,
    short IsClosed,
    long? BranchNo);

public sealed record FinPeriodInfo(
    long FinPeriodNo,
    long FinYearNo,
    string FinPeriodId,
    string FinPeriodName,
    DateOnly StartDate,
    DateOnly EndDate,
    short PeriodType,
    short PeriodStatus,
    short IsClosed);

// ─────────────────────────────────────────────────────────────────────────────
// Ports
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Branch reference data. Branches are SYS master data that every module validates against.</summary>
public interface ISysBranchDirectory
{
    Task<bool> ExistsAsync(long branchNo, CancellationToken cancellationToken = default);

    Task<BranchInfo?> FindAsync(long branchNo, CancellationToken cancellationToken = default);
}

/// <summary>Company-scoped key/value settings held in <c>sys_setting</c>.</summary>
public interface ISysSettingsStore
{
    Task<string?> GetAsync(long companyNo, string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts or updates one setting. <paramref name="group"/> and <paramref name="description"/>
    /// are only applied when the stored row has none, matching the original upsert.
    /// </summary>
    Task UpsertAsync(long companyNo, string key, string value, short valueType,
                     string? group, string? description, CancellationToken cancellationToken = default);
}

/// <summary>
/// Allocates the next number for a document type from <c>sys_doc_sequence</c>, creating the
/// sequence row on first use. Returns the formatted document id (prefix + zero-padded counter).
/// </summary>
public interface IDocSequenceGenerator
{
    Task<string> NextAsync(long companyNo, long? branchNo, string docType, string prefix,
                           int width = 4, CancellationToken cancellationToken = default);
}

/// <summary>
/// The fiscal calendar (<c>sys_fin_year</c> / <c>sys_fin_year_dtl</c>). Owned by SYS; FIN and INV
/// read it to resolve the period a transaction falls into, and FIN drives year/period closing.
/// </summary>
public interface IFinCalendar
{
    Task<FinYearInfo?> FindYearAsync(long finYearNo, CancellationToken cancellationToken = default);

    /// <summary>The company's fiscal year covering <paramref name="date"/>.</summary>
    Task<FinYearInfo?> FindYearForDateAsync(long companyNo, DateOnly date,
                                            CancellationToken cancellationToken = default);

    /// <summary>The active fiscal year covering <paramref name="date"/>, ignoring company scope.</summary>
    Task<FinYearInfo?> FindActiveYearForDateAsync(DateOnly date, CancellationToken cancellationToken = default);

    /// <summary>Any active fiscal year — the fallback when no year covers the requested date.</summary>
    Task<FinYearInfo?> FindAnyActiveYearAsync(CancellationToken cancellationToken = default);

    /// <summary>All of the company's fiscal years, newest start date first.</summary>
    Task<IReadOnlyList<FinYearInfo>> ListYearsAsync(long companyNo, CancellationToken cancellationToken = default);

    Task<FinPeriodInfo?> FindPeriodAsync(long finPeriodNo, CancellationToken cancellationToken = default);

    /// <summary>The period within <paramref name="finYearNo"/> covering <paramref name="date"/>.</summary>
    Task<FinPeriodInfo?> FindPeriodForDateAsync(long finYearNo, DateOnly date,
                                                CancellationToken cancellationToken = default);

    /// <summary>Every period belonging to the given years, ordered by start date.</summary>
    Task<IReadOnlyList<FinPeriodInfo>> ListPeriodsAsync(IReadOnlyCollection<long> finYearNos,
                                                        CancellationToken cancellationToken = default);

    /// <summary>Marks a period closed (<c>period_status = 2</c>).</summary>
    Task ClosePeriodAsync(long finPeriodNo, long actingUserNo, CancellationToken cancellationToken = default);

    /// <summary>Marks a fiscal year closed (<c>year_status = 2</c>).</summary>
    Task CloseYearAsync(long finYearNo, long actingUserNo, CancellationToken cancellationToken = default);
}

/// <summary>
/// Read side of the approval workflow. Modules that raise approvals need to know whether a request
/// is still untouched before letting the underlying document be edited.
/// </summary>
public interface IApprovalRequestReader
{
    /// <summary>Current status of a request, or <c>null</c> when it no longer exists.</summary>
    Task<short?> GetStatusAsync(long approvalRequestNo, CancellationToken cancellationToken = default);
}
