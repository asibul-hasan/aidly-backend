using Microsoft.EntityFrameworkCore;
using AidlyErp.Application.Common.Exceptions;
using AidlyErp.Application.Common.Interfaces;
using AidlyErp.Application.Common.Security;
using AidlyErp.Domain.Hrm;

namespace AidlyErp.Application.Hrm.Services;

// ═══════════════════════════════════════════════════════════════════════════
// Hrm1006Service — Holiday Calendar
// Full port of Java Hrm1006Service (172 lines). Branch-scoped date uniqueness,
// holiday type validation (1–4), conditional field updates, leave year default.
// ═══════════════════════════════════════════════════════════════════════════

public interface IHrm1006Service
{
    Task<List<HrmHoliday>> GetListAsync(CancellationToken ct = default);
    Task<List<HrmHoliday>> GetListByBranchAsync(long branchNo, CancellationToken ct = default);
    Task<HrmHoliday> GetDetailAsync(long holidayNo, CancellationToken ct = default);
    Task<HrmHoliday> SaveAsync(HrmHoliday dto, CancellationToken ct = default);
    Task DeleteAsync(long holidayNo, CancellationToken ct = default);
}

public class Hrm1006Service : IHrm1006Service
{
    private readonly IApplicationDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    public Hrm1006Service(IApplicationDbContext db, ICompanyBranchContext ctx) { _db = db; _ctx = ctx; }

    // ── Reads ──────────────────────────────────────────────────────────────

    public async Task<List<HrmHoliday>> GetListAsync(CancellationToken ct = default) =>
        await _db.HrmHolidays.AsNoTracking()
            .Where(x => x.IsDeleted == 0 && x.BranchNo == Branch())
            .OrderBy(x => x.HolidayDate).ToListAsync(ct);

    public async Task<List<HrmHoliday>> GetListByBranchAsync(long branchNo, CancellationToken ct = default) =>
        await _db.HrmHolidays.AsNoTracking().Where(x => x.BranchNo == branchNo && x.IsDeleted == 0).OrderBy(x => x.HolidayDate).ToListAsync(ct);

    public async Task<HrmHoliday> GetDetailAsync(long holidayNo, CancellationToken ct = default) =>
        await LoadLiveAsync(holidayNo, ct);

    // ── Save ───────────────────────────────────────────────────────────────

    public async Task<HrmHoliday> SaveAsync(HrmHoliday dto, CancellationToken ct = default)
    {
        if (dto.HolidayNo > 0)
            return await UpdateAsync(dto.HolidayNo, dto, ct);
        return await InsertAsync(dto, ct);
    }

    private async Task<HrmHoliday> InsertAsync(HrmHoliday dto, CancellationToken ct)
    {
        long branchNo = ResolveBranch(dto.BranchNo);
        var date = RequireDate(dto.HolidayDate);
        string name = TrimRequired(dto.HolidayName, "Holiday name");

        if (await _db.HrmHolidays.AnyAsync(x => x.HolidayDate == date && x.BranchNo == branchNo && x.IsDeleted == 0, ct))
            throw new ValidationException($"A holiday already exists on this date in this branch: {date}");

        var entity = new HrmHoliday
        {
            BranchNo = branchNo,
            CompanyNo = _ctx.CompanyNo,
            HolidayDate = date,
            HolidayName = name,
            HolidayType = ValidateHolidayType(dto.HolidayType),
            AlternateType = dto.AlternateType,
            IsRecurring = NormalizeFlagNullable(dto.IsRecurring, 0, "is_recurring"),
            LeaveYear = dto.LeaveYear > 0 ? dto.LeaveYear : date.Year,
            Remarks = dto.Remarks,
            IsActive = NormalizeFlag(dto.IsActive, 1, "is_active"),
            IsDeleted = 0,
            CreatedBy = _ctx.CurrentUserNo(),
            CreatedAt = DateTime.UtcNow
        };
        _db.HrmHolidays.Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity;
    }

    private async Task<HrmHoliday> UpdateAsync(long holidayNo, HrmHoliday dto, CancellationToken ct)
    {
        var entity = await LoadLiveAsync(holidayNo, ct);

        if (dto.HolidayDate != default && dto.HolidayDate != entity.HolidayDate)
        {
            if (await _db.HrmHolidays.AnyAsync(x => x.HolidayDate == dto.HolidayDate && x.BranchNo == entity.BranchNo && x.HolidayNo != holidayNo && x.IsDeleted == 0, ct))
                throw new ValidationException($"A holiday already exists on this date in this branch: {dto.HolidayDate}");
            entity.HolidayDate = dto.HolidayDate;
        }
        if (dto.HolidayName != null) entity.HolidayName = TrimRequired(dto.HolidayName, "Holiday name");
        if (dto.HolidayType != null) entity.HolidayType = ValidateHolidayType(dto.HolidayType);
        if (dto.AlternateType != null) entity.AlternateType = dto.AlternateType;
        if (dto.IsRecurring != null) entity.IsRecurring = NormalizeFlagNullable(dto.IsRecurring, 0, "is_recurring");
        if (dto.LeaveYear > 0) entity.LeaveYear = dto.LeaveYear;
        if (dto.Remarks != null) entity.Remarks = dto.Remarks;
        if (dto.IsActive != 0) entity.IsActive = NormalizeFlag(dto.IsActive, 1, "is_active");

        entity.UpdatedBy = _ctx.CurrentUserNo(); entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task DeleteAsync(long holidayNo, CancellationToken ct = default)
    {
        var entity = await LoadLiveAsync(holidayNo, ct);
        entity.PerformSoftDelete(_ctx.CurrentUserNo());
        await _db.SaveChangesAsync(ct);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private async Task<HrmHoliday> LoadLiveAsync(long holidayNo, CancellationToken ct) =>
        await _db.HrmHolidays.FirstOrDefaultAsync(x => x.HolidayNo == holidayNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Holiday not found: holidayNo={holidayNo}");

    private long Branch()
    {
        var b = _ctx.BranchNo;
        if (b == null) throw new ValidationException("No active branch in context");
        return b.Value;
    }

    private long ResolveBranch(long? branchNoFromDto)
    {
        var branchNo = branchNoFromDto ?? _ctx.BranchNo;
        if (branchNo == null) throw new ValidationException("Branch is required");
        return branchNo.Value;
    }

    private static DateOnly RequireDate(DateOnly date)
    {
        if (date == default) throw new ValidationException("Holiday date is required");
        return date;
    }

    private static short? ValidateHolidayType(short? type)
    {
        short value = type ?? 1;
        if (value < 1 || value > 4) throw new ValidationException("Holiday type must be 1–4 (Public/Religious/Company/Optional)");
        return value;
    }

    private static short NormalizeFlag(short value, short defaultValue, string fieldName)
    {
        if (value == defaultValue) return value;
        if (value is 0 or 1) return value;
        throw new ValidationException($"{fieldName} must be 0 or 1");
    }

    private static short? NormalizeFlagNullable(short? value, short? defaultValue, string fieldName)
    {
        if (!value.HasValue) return defaultValue;
        if (value.Value is 0 or 1) return value;
        throw new ValidationException($"{fieldName} must be 0 or 1");
    }

    private static string TrimRequired(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ValidationException($"{fieldName} is required");
        return value.Trim();
    }
}
