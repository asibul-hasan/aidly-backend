using System.Globalization;
using AidlyErp.Application.Common.Exceptions;
using AidlyErp.Application.Common.Interfaces;
using AidlyErp.Application.Common.Security;
using AidlyErp.Application.Sys.Dto;
using AidlyErp.Domain.Sys;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AidlyErp.Application.Sys.Services;

public interface ISys1003Service
{
    Task<List<Sys1003FinYearDto>> GetListAsync(CancellationToken cancellationToken = default);

    Task<List<Sys1003FinYearDto>> GetListByBranchAsync(long branchNo, CancellationToken cancellationToken = default);

    Task<Sys1003FinYearDto> GetDetailAsync(long finYearNo, CancellationToken cancellationToken = default);

    Task<List<Sys1003FinYearDtlDto>> GetPeriodsAsync(long finYearNo, CancellationToken cancellationToken = default);

    Task<Sys1003FinYearDto?> SaveAsync(Sys1003FinYearDto dto, CancellationToken cancellationToken = default);

    Task DeleteAsync(long finYearNo, CancellationToken cancellationToken = default);
}

/// <summary>
/// Dedicated service for the SYS_1003 Financial Year Setup form.
///
/// <para>Owns every business rule for the master <c>sys_fin_year</c> plus its child periods in
/// <c>sys_fin_year_dtl</c>: company/branch resolution, duplicate-code and duplicate-name checks,
/// start/end date validation, non-overlapping year ranges, active/closed status handling, and the
/// atomic master-detail save with cascade soft-delete.</para>
///
/// <para><b>All Branches pattern:</b> <c>branch_no = NULL</c> means company-wide (applies to all
/// branches), matching the SYS_1004 currency setup pattern.</para>
/// </summary>
public class Sys1003Service : ISys1003Service
{
    private const short Active = 1;
    private const short Deleted = 0;
    private const short Closed = 1;
    private const short Locked = 3; // period_status = 3 = Locked

    private readonly IApplicationDbContext _db;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICompanyBranchContext _ctx;
    private readonly ILogger<Sys1003Service> _logger;

    public Sys1003Service(IApplicationDbContext db, IUnitOfWork unitOfWork, ICompanyBranchContext ctx,
                          ILogger<Sys1003Service> logger)
    {
        _db = db;
        _unitOfWork = unitOfWork;
        _ctx = ctx;
        _logger = logger;
    }

    // ─── Reads ───────────────────────────────────────────────────────────────

    public async Task<List<Sys1003FinYearDto>> GetListAsync(CancellationToken cancellationToken = default)
    {
        // Branch-specific rows for the current branch + company-wide rows (branch_no IS NULL).
        // Company + is_deleted always hold; a null branch (company context) safely returns only
        // the company-wide rows.
        var companyNo = Company();
        var branchNo = _ctx.BranchNo;

        var rows = await _db.FinYears
            .AsNoTracking()
            .Where(y => y.CompanyNo == companyNo && y.IsDeleted == Deleted
                        && (y.BranchNo == null || (branchNo != null && y.BranchNo == branchNo)))
            .OrderBy(y => y.FinYearNo)
            .ToListAsync(cancellationToken);

        return rows.Select(ToDto).ToList();
    }

    public Task<List<Sys1003FinYearDto>> GetListByBranchAsync(long branchNo,
                                                              CancellationToken cancellationToken = default) =>
        GetListAsync(cancellationToken);

    public async Task<Sys1003FinYearDto> GetDetailAsync(long finYearNo, CancellationToken cancellationToken = default)
    {
        var master = await LoadLiveAsync(finYearNo, cancellationToken);
        var dto = ToDto(master);
        dto.Periods = await LoadPeriodsAsync(finYearNo, cancellationToken);
        return dto;
    }

    public async Task<List<Sys1003FinYearDtlDto>> GetPeriodsAsync(long finYearNo,
                                                                  CancellationToken cancellationToken = default)
    {
        // Ensure the parent year is live before listing periods.
        await LoadLiveAsync(finYearNo, cancellationToken);
        return await LoadPeriodsAsync(finYearNo, cancellationToken);
    }

    // ─── Save (insert or update) ─────────────────────────────────────────────

    public Task<Sys1003FinYearDto?> SaveAsync(Sys1003FinYearDto dto, CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(async ct => dto.FinYearNo != null
            ? await UpdateAsync(dto.FinYearNo.Value, dto, ct)
            : await InsertAsync(dto, ct), cancellationToken);

    public Task DeleteAsync(long finYearNo, CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(async ct =>
        {
            var entity = await LoadLiveAsync(finYearNo, ct);

            if (entity.IsClosed == Closed)
            {
                throw new ValidationException(
                    "A closed financial year cannot be deleted: " + entity.FinYearName);
            }

            // Cascade soft-delete to every live child period. Locked periods block the action.
            var periods = await _db.FinYearDtls
                .Where(p => p.FinYearNo == finYearNo && p.IsDeleted == Deleted)
                .OrderBy(p => p.StartDate)
                .ToListAsync(ct);

            var userNo = _ctx.CurrentUserNo();

            foreach (var p in periods)
            {
                if (p.PeriodStatus == Locked)
                {
                    throw new ValidationException(
                        "Cannot delete: a locked period exists (" + p.FinPeriodId + ")");
                }

                p.PerformSoftDelete(userNo);
            }

            entity.PerformSoftDelete(userNo);
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("FinYear soft-deleted with {Count} child period(s): finYearNo={FinYearNo}",
                periods.Count, finYearNo);
        }, cancellationToken);

    // ─── Private: master write paths ─────────────────────────────────────────

    private async Task<Sys1003FinYearDto?> InsertAsync(Sys1003FinYearDto dto, CancellationToken ct)
    {
        var companyNo = Company();
        var startDate = dto.StartDate;
        var endDate = dto.EndDate;

        ValidateDates(startDate, endDate);

        var finYearId = AutoFillFinYearId(dto.FinYearId, startDate!.Value, endDate!.Value);
        var finYearName = TrimRequired(dto.FinYearName, "Financial year name");
        var branchNos = ResolveBranches(dto.BranchNos);

        Sys1003FinYearDto? lastSaved = null;
        foreach (var branchNo in branchNos)
        {
            lastSaved = await InsertSingleAsync(companyNo, branchNo, finYearId, finYearName,
                                                startDate.Value, endDate.Value, dto, ct);
        }

        return lastSaved;
    }

    private async Task<Sys1003FinYearDto> InsertSingleAsync(long companyNo, long? branchNo, string finYearId,
                                                            string finYearName, DateOnly startDate, DateOnly endDate,
                                                            Sys1003FinYearDto dto, CancellationToken ct)
    {
        // Per-(company, branch) uniqueness — the same FY id may exist in another branch.
        var duplicate = await _db.FinYears
            .AnyAsync(y => y.FinYearId == finYearId && y.CompanyNo == companyNo
                           && y.BranchNo == branchNo && y.IsDeleted == Deleted, ct);

        if (duplicate)
        {
            throw new ValidationException("Financial year ID already exists for this branch: " + finYearId);
        }

        await ValidateNoOverlapAsync(companyNo, branchNo, startDate, endDate, null, ct);

        var entity = new FinYear
        {
            CompanyNo = companyNo,
            BranchNo = branchNo,
            FinYearId = finYearId,
            FinYearName = finYearName,
            StartDate = startDate,
            EndDate = endDate,
            IsClosed = NormalizeFlag(dto.IsClosed, 0, "is_closed"),
            Remarks = dto.Remarks,
            IsActive = NormalizeFlag(dto.IsActive, Active, "is_active")
        };

        _db.FinYears.Add(entity);
        await _db.SaveChangesAsync(ct);

        await SyncPeriodsAsync(entity, dto.Periods, ct);

        _logger.LogInformation("FinYear inserted: finYearNo={FinYearNo}, finYearId={FinYearId}, branchNo={BranchNo}",
            entity.FinYearNo, entity.FinYearId, branchNo);

        return await BuildResponseAsync(entity, ct);
    }

    private async Task<Sys1003FinYearDto> UpdateAsync(long finYearNo, Sys1003FinYearDto dto, CancellationToken ct)
    {
        var entity = await LoadLiveAsync(finYearNo, ct);

        var companyNo = entity.CompanyNo;
        var branchNo = entity.BranchNo; // branch is immutable once a year is opened

        var startDate = dto.StartDate ?? entity.StartDate;
        var endDate = dto.EndDate ?? entity.EndDate;

        ValidateDates(startDate, endDate);
        await ValidateNoOverlapAsync(companyNo, branchNo, startDate, endDate, finYearNo, ct);

        if (dto.FinYearId != null)
        {
            var finYearId = TrimRequired(dto.FinYearId, "Financial year ID");

            var other = await _db.FinYears
                .FirstOrDefaultAsync(y => y.FinYearId == finYearId && y.CompanyNo == companyNo
                                          && y.BranchNo == branchNo && y.IsDeleted == Deleted, ct);

            if (other != null && other.FinYearNo != finYearNo)
            {
                throw new ValidationException("Financial year ID already in use: " + finYearId);
            }

            entity.FinYearId = finYearId;
        }

        if (dto.FinYearName != null)
        {
            var finYearName = TrimRequired(dto.FinYearName, "Financial year name");

            var other = await _db.FinYears
                .FirstOrDefaultAsync(y => y.FinYearName == finYearName && y.CompanyNo == companyNo
                                          && y.BranchNo == branchNo && y.IsDeleted == Deleted, ct);

            if (other != null && other.FinYearNo != finYearNo)
            {
                throw new ValidationException("Financial year name already in use: " + finYearName);
            }

            entity.FinYearName = finYearName;
        }

        entity.StartDate = startDate;
        entity.EndDate = endDate;

        if (dto.IsClosed != null) entity.IsClosed = NormalizeFlag(dto.IsClosed, 0, "is_closed");
        if (dto.Remarks != null) entity.Remarks = dto.Remarks;
        if (dto.IsActive != null) entity.IsActive = NormalizeFlag(dto.IsActive, Active, "is_active");

        await _db.SaveChangesAsync(ct);
        await SyncPeriodsAsync(entity, dto.Periods, ct);

        _logger.LogInformation("FinYear updated: finYearNo={FinYearNo}", entity.FinYearNo);

        return await BuildResponseAsync(entity, ct);
    }

    private async Task<Sys1003FinYearDto> BuildResponseAsync(FinYear saved, CancellationToken ct)
    {
        var outDto = ToDto(saved);
        outDto.Periods = await LoadPeriodsAsync(saved.FinYearNo, ct);
        return outDto;
    }

    // ─── Private: detail (periods) write path ────────────────────────────────

    /// <summary>
    /// Reconciles the requested periods with the rows already in the database for this master.
    ///
    /// <list type="bullet">
    ///   <item><c>null</c> → leave existing periods untouched (master-only save).</item>
    ///   <item>Periods carrying <c>fin_period_no</c> → update that row.</item>
    ///   <item>Periods without <c>fin_period_no</c> → insert as new rows.</item>
    ///   <item>Existing rows absent from the request → soft-delete.</item>
    /// </list>
    ///
    /// <para>Locked periods can be neither modified nor removed — the whole save aborts in that
    /// case so the transaction rolls back cleanly.</para>
    /// </summary>
    private async Task SyncPeriodsAsync(FinYear master, List<Sys1003FinYearDtlDto>? requested, CancellationToken ct)
    {
        if (requested == null)
        {
            return;
        }

        // Pre-validate the whole batch before touching the DB so the transaction rolls back
        // without writing partial rows on failure.
        foreach (var p in requested) ValidatePeriod(master, p);
        ValidatePeriodsNoOverlap(requested);

        var current = await _db.FinYearDtls
            .Where(p => p.FinYearNo == master.FinYearNo && p.IsDeleted == Deleted)
            .OrderBy(p => p.StartDate)
            .ToListAsync(ct);

        var currentByNo = current.ToDictionary(p => p.FinPeriodNo);
        var retainedPeriodNos = new HashSet<long>();

        foreach (var p in requested)
        {
            var periodId = TrimRequired(p.FinPeriodId, "Period ID");
            var periodName = TrimRequired(p.FinPeriodName, "Period name");

            FinYearDtl entity;

            if (p.FinPeriodNo != null)
            {
                if (!currentByNo.TryGetValue(p.FinPeriodNo.Value, out entity!))
                {
                    throw new NotFoundException("Period not found in this year: finPeriodNo=" + p.FinPeriodNo);
                }

                if (entity.PeriodStatus == Locked)
                {
                    throw new ValidationException("A locked period cannot be modified: " + entity.FinPeriodId);
                }
            }
            else
            {
                entity = new FinYearDtl { FinYearNo = master.FinYearNo };
                _db.FinYearDtls.Add(entity);
            }

            // Per-year uniqueness on fin_period_id, excluding the row being updated.
            var currentPeriodNo = entity.FinPeriodNo;

            var other = await _db.FinYearDtls
                .FirstOrDefaultAsync(x => x.FinYearNo == master.FinYearNo && x.FinPeriodId == periodId
                                          && x.IsDeleted == Deleted, ct);

            if (other != null && (currentPeriodNo == 0 || other.FinPeriodNo != currentPeriodNo))
            {
                throw new ValidationException("Period ID already exists in this year: " + periodId);
            }

            entity.FinPeriodId = periodId;
            entity.FinPeriodName = periodName;
            entity.StartDate = p.StartDate!.Value;
            entity.EndDate = p.EndDate!.Value;
            entity.PeriodType = NormalizePeriodType(p.PeriodType);
            entity.PeriodStatus = NormalizePeriodStatus(p.PeriodStatus);
            entity.Remarks = p.Remarks;
            entity.IsActive = NormalizeFlag(p.IsActive, Active, "is_active");

            await _db.SaveChangesAsync(ct);
            retainedPeriodNos.Add(entity.FinPeriodNo);
        }

        // Soft-delete periods the request no longer carries.
        var userNo = _ctx.CurrentUserNo();

        foreach (var existing in current)
        {
            if (retainedPeriodNos.Contains(existing.FinPeriodNo))
            {
                continue;
            }

            if (existing.PeriodStatus == Locked)
            {
                throw new ValidationException("A locked period cannot be removed: " + existing.FinPeriodId);
            }

            existing.PerformSoftDelete(userNo);
        }

        await _db.SaveChangesAsync(ct);
    }

    // ─── Private: master validation helpers ──────────────────────────────────

    private async Task<FinYear> LoadLiveAsync(long finYearNo, CancellationToken ct)
    {
        var entity = await _db.FinYears.FirstOrDefaultAsync(y => y.FinYearNo == finYearNo && y.IsDeleted == Deleted, ct)
                     ?? throw new NotFoundException("Financial year not found: finYearNo=" + finYearNo);

        // Verify company access
        var companyNo = _ctx.CompanyNo;
        if (companyNo != null && companyNo != entity.CompanyNo)
        {
            throw new NotFoundException("Financial year not found: finYearNo=" + finYearNo);
        }

        return entity;
    }

    private long Company() =>
        _ctx.CompanyNo ?? throw new ValidationException("No active company in context");

    /// <summary>Empty/absent branch list means one company-wide row (<c>branch_no = NULL</c>).</summary>
    private static List<long?> ResolveBranches(List<long>? requestedBranchNos) =>
        requestedBranchNos == null || requestedBranchNos.Count == 0
            ? new List<long?> { null }
            : requestedBranchNos.Distinct().Select(b => (long?)b).ToList();

    private static void ValidateDates(DateOnly? startDate, DateOnly? endDate)
    {
        if (startDate == null || endDate == null)
        {
            throw new ValidationException("Start date and end date are required");
        }

        if (startDate >= endDate)
        {
            throw new ValidationException("Start date must be before end date");
        }
    }

    /// <summary>
    /// Rejects a date span that overlaps any other live financial year in the same
    /// company + branch scope.
    /// </summary>
    private async Task ValidateNoOverlapAsync(long companyNo, long? branchNo, DateOnly startDate, DateOnly endDate,
                                              long? excludeFinYearNo, CancellationToken ct)
    {
        var candidates = await _db.FinYears
            .AsNoTracking()
            .Where(y => y.CompanyNo == companyNo && y.IsDeleted == Deleted
                        && y.StartDate <= endDate && y.EndDate >= startDate)
            .ToListAsync(ct);

        var overlaps = candidates
            // Same branch scope: match if both have the same branch_no or both are null.
            .Where(other => branchNo == null ? other.BranchNo == null : other.BranchNo == branchNo)
            .Any(other => excludeFinYearNo == null || other.FinYearNo != excludeFinYearNo);

        if (overlaps)
        {
            throw new ValidationException("The date range overlaps an existing financial year in this scope");
        }
    }

    // ─── Private: detail validation helpers ──────────────────────────────────

    private static void ValidatePeriod(FinYear master, Sys1003FinYearDtlDto? p)
    {
        if (p == null)
        {
            throw new ValidationException("Period is required");
        }

        if (p.StartDate == null || p.EndDate == null)
        {
            throw new ValidationException("Period start/end dates are required");
        }

        if (p.StartDate >= p.EndDate)
        {
            throw new ValidationException("Period start date must be before its end date");
        }

        if (p.StartDate < master.StartDate || p.EndDate > master.EndDate)
        {
            throw new ValidationException(
                $"Period dates must fall within the financial year ({master.StartDate} to {master.EndDate})");
        }

        if (string.IsNullOrWhiteSpace(p.FinPeriodId))
        {
            throw new ValidationException("Period ID is required");
        }

        if (string.IsNullOrWhiteSpace(p.FinPeriodName))
        {
            throw new ValidationException("Period name is required");
        }
    }

    /// <summary>Rejects a request that contains two periods with overlapping date ranges.</summary>
    private static void ValidatePeriodsNoOverlap(List<Sys1003FinYearDtlDto> periods)
    {
        var sorted = periods.OrderBy(p => p.StartDate).ToList();

        for (var i = 1; i < sorted.Count; i++)
        {
            var prev = sorted[i - 1];
            var curr = sorted[i];

            // Overlap iff the next period starts on or before the previous one's end.
            if (curr.StartDate <= prev.EndDate)
            {
                throw new ValidationException(
                    $"Periods overlap: '{prev.FinPeriodId}' and '{curr.FinPeriodId}'");
            }
        }
    }

    private static short NormalizePeriodType(short? type)
    {
        var value = type ?? 1;
        if (value < 1 || value > 5)
        {
            throw new ValidationException(
                "Period type must be 1=Monthly, 2=Quarterly, 3=Half-Yearly, 4=Yearly, or 5=Adjustment");
        }
        return value;
    }

    private static short NormalizePeriodStatus(short? status)
    {
        var value = status ?? 1;
        if (value < 1 || value > 3)
        {
            throw new ValidationException("Period status must be 1=Open, 2=Closed, or 3=Locked");
        }
        return value;
    }

    // ─── Private: shared helpers ─────────────────────────────────────────────

    private static short NormalizeFlag(short? value, short defaultValue, string fieldName)
    {
        if (value == null)
        {
            return defaultValue;
        }

        if (value != 0 && value != 1)
        {
            throw new ValidationException(fieldName + " must be 0 or 1");
        }

        return value.Value;
    }

    private static string TrimRequired(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException(fieldName + " is required");
        }

        return value.Trim();
    }

    /// <summary>
    /// Builds the master <c>fin_year_id</c> when the client didn't supply one. Format:
    /// <c>FY-MMMYY-MMMYY</c> where each segment is the upper-case English month abbreviation plus
    /// the 2-digit year of the start/end date — e.g. start <c>2025-07-01</c> + end
    /// <c>2026-06-30</c> → <c>FY-JUL25-JUN26</c>. The detail table's <c>fin_period_id</c> is
    /// intentionally NOT auto-generated.
    /// </summary>
    private static string AutoFillFinYearId(string? provided, DateOnly startDate, DateOnly endDate)
    {
        if (!string.IsNullOrWhiteSpace(provided))
        {
            return provided.Trim();
        }

        var english = CultureInfo.GetCultureInfo("en-US");
        var startPart = startDate.ToString("MMMyy", english).ToUpperInvariant();
        var endPart = endDate.ToString("MMMyy", english).ToUpperInvariant();

        return $"FY-{startPart}-{endPart}";
    }

    // ─── Private: mapping ────────────────────────────────────────────────────

    private static Sys1003FinYearDto ToDto(FinYear e) => new()
    {
        FinYearNo = e.FinYearNo,
        FinYearId = e.FinYearId,
        FinYearName = e.FinYearName,
        StartDate = e.StartDate,
        EndDate = e.EndDate,
        IsClosed = e.IsClosed,
        BranchNo = e.BranchNo,
        Remarks = e.Remarks,
        IsActive = e.IsActive,
        RowVersion = e.RowVersion
    };

    private async Task<List<Sys1003FinYearDtlDto>> LoadPeriodsAsync(long finYearNo, CancellationToken ct) =>
        await _db.FinYearDtls
            .AsNoTracking()
            .Where(p => p.FinYearNo == finYearNo && p.IsDeleted == Deleted)
            .OrderBy(p => p.StartDate)
            .Select(e => new Sys1003FinYearDtlDto
            {
                FinPeriodNo = e.FinPeriodNo,
                FinYearNo = e.FinYearNo,
                FinPeriodId = e.FinPeriodId,
                FinPeriodName = e.FinPeriodName,
                StartDate = e.StartDate,
                EndDate = e.EndDate,
                PeriodType = e.PeriodType,
                PeriodStatus = e.PeriodStatus,
                Remarks = e.Remarks,
                IsActive = e.IsActive,
                RowVersion = e.RowVersion
            })
            .ToListAsync(ct);
}
