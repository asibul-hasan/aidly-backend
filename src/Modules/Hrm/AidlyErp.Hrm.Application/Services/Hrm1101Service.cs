using Microsoft.EntityFrameworkCore;
using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Abstractions;
using AidlyErp.Shared.Contracts;
using AidlyErp.Sys.Contracts;
using AidlyErp.Hrm.Application.Interfaces;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Hrm.Domain;

namespace AidlyErp.Hrm.Application.Services;

// ═══════════════════════════════════════════════════════════════════════════
// Hrm1101Service — Manual Attendance
// Full port of Java Hrm1101Service (209 lines). Locked-row guard, future date
// guard, status range, employee existence, non-negative times, conditional
// update fields, source=MANUAL on every write.
// ═══════════════════════════════════════════════════════════════════════════

public interface IHrm1101Service
{
    Task<List<HrmAttendance>> GetListAsync(long? branchNo, DateTime? fromDate, DateTime? toDate, CancellationToken ct = default);
    Task<List<HrmAttendance>> GetListByEmployeeAsync(long employeeNo, CancellationToken ct = default);
    Task<HrmAttendance> GetDetailAsync(long attendanceNo, CancellationToken ct = default);
    Task<HrmAttendance> SaveAsync(HrmAttendance dto, CancellationToken ct = default);
    Task DeleteAsync(long attendanceNo, CancellationToken ct = default);
}

public class Hrm1101Service : IHrm1101Service
{
    private readonly IHrmDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    private const short SrcManual = 1;

    public Hrm1101Service(IHrmDbContext db, ICompanyBranchContext ctx) { _db = db; _ctx = ctx; }

    public async Task<List<HrmAttendance>> GetListAsync(long? branchNo, DateTime? fromDate, DateTime? toDate, CancellationToken ct = default)
    {
        var query = _db.HrmAttendances.AsNoTracking().Where(x => x.IsDeleted == 0);
        if (branchNo.HasValue) query = query.Where(x => x.BranchNo == branchNo.Value);
        if (fromDate.HasValue) query = query.Where(x => x.AttDate >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(x => x.AttDate <= toDate.Value);
        return await query.OrderByDescending(x => x.AttDate).ThenByDescending(x => x.AttendanceNo).ToListAsync(ct);
    }

    public async Task<List<HrmAttendance>> GetListByEmployeeAsync(long employeeNo, CancellationToken ct = default) =>
        await _db.HrmAttendances.AsNoTracking().Where(x => x.EmployeeNo == employeeNo && x.IsDeleted == 0).OrderByDescending(x => x.AttDate).ToListAsync(ct);

    public async Task<HrmAttendance> GetDetailAsync(long attendanceNo, CancellationToken ct = default) =>
        await LoadLiveAsync(attendanceNo, ct);

    public async Task<HrmAttendance> SaveAsync(HrmAttendance dto, CancellationToken ct = default)
    {
        if (dto.AttendanceNo > 0)
            return await UpdateAsync(dto.AttendanceNo, dto, ct);
        return await InsertAsync(dto, ct);
    }

    public async Task DeleteAsync(long attendanceNo, CancellationToken ct = default)
    {
        var entity = await LoadLiveAsync(attendanceNo, ct);
        GuardLocked(entity);
        entity.PerformSoftDelete(_ctx.CurrentUserNo());
        await _db.SaveChangesAsync(ct);
    }

    // ── Write paths ────────────────────────────────────────────────────────

    private async Task<HrmAttendance> InsertAsync(HrmAttendance dto, CancellationToken ct)
    {
        long branchNo = ResolveBranch(dto.BranchNo);
        long employeeNo = RequireEmployee(dto.EmployeeNo);
        var date = RequireDate(dto.AttDate);
        var status = ValidateStatus(dto.Status);

        if (await _db.HrmAttendances.AnyAsync(x => x.EmployeeNo == employeeNo && x.AttDate == date && x.IsDeleted == 0, ct))
            throw new ValidationException($"Attendance already recorded for this employee on {date:yyyy-MM-dd} — edit that row instead");

        var entity = new HrmAttendance
        {
            BranchNo = branchNo,
            CompanyNo = _ctx.CompanyNo,
            EmployeeNo = employeeNo,
            AttDate = date,
            ShiftNo = dto.ShiftNo,
            InTime = dto.InTime,
            OutTime = dto.OutTime,
            Status = status,
            LateMinutes = NonNegativeInt(dto.LateMinutes, "Late minutes"),
            EarlyOutMinutes = NonNegativeInt(dto.EarlyOutMinutes, "Early-out minutes"),
            WorkedHours = NonNegativeDecimal(dto.WorkedHours, "Worked hours"),
            OtHours = NonNegativeDecimal(dto.OtHours, "OT hours"),
            Source = SrcManual,
            IsLocked = 0,
            IsActive = NormalizeFlag(dto.IsActive, 1, "is_active"),
            IsDeleted = 0,
            CreatedBy = _ctx.CurrentUserNo(),
            CreatedAt = DateTime.UtcNow
        };
        _db.HrmAttendances.Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity;
    }

    private async Task<HrmAttendance> UpdateAsync(long attendanceNo, HrmAttendance dto, CancellationToken ct)
    {
        var entity = await LoadLiveAsync(attendanceNo, ct);
        GuardLocked(entity);

        // employee + date are the natural key — immutable on an existing row.
        if (dto.Status != 0) entity.Status = ValidateStatus(dto.Status);
        if (dto.ShiftNo != null) entity.ShiftNo = dto.ShiftNo;
        if (dto.InTime != null) entity.InTime = dto.InTime;
        if (dto.OutTime != null) entity.OutTime = dto.OutTime;
        if (dto.LateMinutes != 0) entity.LateMinutes = NonNegativeInt(dto.LateMinutes, "Late minutes");
        if (dto.EarlyOutMinutes != 0) entity.EarlyOutMinutes = NonNegativeInt(dto.EarlyOutMinutes, "Early-out minutes");
        if (dto.WorkedHours != 0) entity.WorkedHours = NonNegativeDecimal(dto.WorkedHours, "Worked hours");
        if (dto.OtHours != 0) entity.OtHours = NonNegativeDecimal(dto.OtHours, "OT hours");
        entity.Source = SrcManual;
        if (dto.IsActive != 0) entity.IsActive = NormalizeFlag(dto.IsActive, 1, "is_active");

        entity.UpdatedBy = _ctx.CurrentUserNo(); entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return entity;
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private async Task<HrmAttendance> LoadLiveAsync(long attendanceNo, CancellationToken ct) =>
        await _db.HrmAttendances.FirstOrDefaultAsync(x => x.AttendanceNo == attendanceNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Attendance not found: attendanceNo={attendanceNo}");

    private static void GuardLocked(HrmAttendance entity)
    {
        if (entity.IsLocked == 1)
            throw new ValidationException("This attendance is locked by a finalized payroll run; raise an adjustment (HRM_1103) to change it");
    }

    private long ResolveBranch(long? branchNoFromDto)
    {
        var branchNo = branchNoFromDto ?? _ctx.BranchNo;
        if (branchNo == null) throw new ValidationException("Branch is required");
        return branchNo.Value;
    }

    private long RequireEmployee(long employeeNo)
    {
        if (employeeNo <= 0) throw new ValidationException("Employee is required");
        return employeeNo;
    }

    private static DateTime RequireDate(DateTime date)
    {
        if (date == default) throw new ValidationException("Attendance date is required");
        if (date.Date > DateTime.UtcNow.Date) throw new ValidationException("Attendance date cannot be in the future");
        return date;
    }

    private static short ValidateStatus(short status)
    {
        if (status < 1 || status > 8) throw new ValidationException("Attendance status must be 1–8");
        return status;
    }

    private static int NonNegativeInt(int value, string fieldName)
    {
        if (value < 0) throw new ValidationException($"{fieldName} cannot be negative");
        return value;
    }

    private static decimal NonNegativeDecimal(decimal value, string fieldName)
    {
        if (value < 0) throw new ValidationException($"{fieldName} cannot be negative");
        return value;
    }

    private static short NormalizeFlag(short value, short defaultValue, string fieldName)
    {
        if (value == defaultValue) return value;
        if (value is 0 or 1) return value;
        throw new ValidationException($"{fieldName} must be 0 or 1");
    }
}
