using Microsoft.EntityFrameworkCore;
using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Abstractions;
using AidlyErp.Shared.Contracts;
using AidlyErp.Sys.Contracts;
using AidlyErp.Hrm.Application.Interfaces;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Hrm.Application.Dto;
using AidlyErp.Hrm.Domain;

namespace AidlyErp.Hrm.Application.Services;

public interface IHrm1102Service
{
    Task<Hrm1102SyncResultDto> SyncAsync(Hrm1102SyncRequestDto request, CancellationToken ct = default);
    Task<List<Hrm1102AttendanceRowDto>> GetRecentAsync(CancellationToken ct = default);
}

public class Hrm1102Service : IHrm1102Service
{
    private readonly IHrmDbContext _db;
    private readonly ICompanyBranchContext _ctx;

    private const short StPresent = 1, StAbsent = 2, StLate = 3;
    private const short SrcDevice = 2;

    public Hrm1102Service(IHrmDbContext db, ICompanyBranchContext ctx)
    {
        _db = db;
        _ctx = ctx;
    }

    public async Task<Hrm1102SyncResultDto> SyncAsync(Hrm1102SyncRequestDto request, CancellationToken ct = default)
    {
        if (request == null || request.Punches == null || request.Punches.Count == 0)
        {
            throw new ValidationException("No punches to sync");
        }

        long? branchNo = request.BranchNo ?? _ctx.CurrentBranchNo();
        if (!branchNo.HasValue) throw new ValidationException("Branch is required");

        var shiftCache = new Dictionary<long, HrmShift?>();
        var result = new Hrm1102SyncResultDto();

        foreach (var p in request.Punches)
        {
            result.Total++;
            if (!p.EmployeeNo.HasValue || !p.AttDate.HasValue)
            {
                result.Skipped++;
                result.Messages.Add("Skipped a punch missing employee or date");
                continue;
            }

            var existing = await _db.HrmAttendances.FirstOrDefaultAsync(
                a => a.EmployeeNo == p.EmployeeNo.Value && a.AttDate == p.AttDate.Value && a.IsDeleted == 0, ct);

            if (existing != null && existing.IsLocked == 1)
            {
                result.Skipped++;
                result.Messages.Add($"Locked (payroll-processed): emp {p.EmployeeNo.Value} on {p.AttDate.Value:yyyy-MM-dd}");
                continue;
            }

            HrmShift? shift = await ResolveShiftAsync(p.ShiftNo, shiftCache, ct);
            HrmAttendance a = existing ?? new HrmAttendance();
            a.EmployeeNo = p.EmployeeNo.Value;
            a.AttDate = p.AttDate.Value;
            a.BranchNo = branchNo.Value;
            a.ShiftNo = p.ShiftNo;
            a.InTime = p.InTime;
            a.OutTime = p.OutTime;
            a.Source = SrcDevice;

            ApplyDerived(a, shift);

            if (existing != null)
            {
                a.UpdatedBy = _ctx.CurrentUserNo();
                a.UpdatedAt = DateTime.UtcNow;
                result.Updated++;
            }
            else
            {
                a.IsActive = 1; a.IsDeleted = 0;
                a.CreatedBy = _ctx.CurrentUserNo();
                a.CreatedAt = DateTime.UtcNow;
                _db.HrmAttendances.Add(a);
                result.Created++;
            }
        }

        await _db.SaveChangesAsync(ct);
        return result;
    }

    public async Task<List<Hrm1102AttendanceRowDto>> GetRecentAsync(CancellationToken ct = default)
    {
        long? branchNo = _ctx.CurrentBranchNo();

        var query = _db.HrmAttendances.AsNoTracking().Where(a => a.IsDeleted == 0);
        if (branchNo.HasValue)
        {
            query = query.Where(a => a.BranchNo == branchNo.Value);
        }

        var rows = await query
            .OrderByDescending(a => a.AttDate)
            .ThenByDescending(a => a.AttendanceNo)
            .Take(200)
            .ToListAsync(ct);

        return rows.Select(a => ToRow(a)).ToList();
    }

    private void ApplyDerived(HrmAttendance a, HrmShift? shift)
    {
        if (!a.InTime.HasValue)
        {
            a.Status = StAbsent;
            a.LateMinutes = 0;
            a.WorkedHours = 0m;
            return;
        }

        short status = StPresent;
        int late = 0;

        if (shift != null)
        {
            int grace = shift.GraceMinutes ?? 0;
            TimeOnly allowed = shift.StartTime.AddMinutes(grace);
            if (a.InTime.Value > allowed)
            {
                late = (int)(a.InTime.Value - shift.StartTime).TotalMinutes;
                status = StLate;
            }
        }

        a.Status = status;
        a.LateMinutes = late;

        if (a.OutTime.HasValue && a.OutTime.Value > a.InTime.Value)
        {
            long minutes = (long)(a.OutTime.Value - a.InTime.Value).TotalMinutes;
            int breakMin = shift?.BreakMinutes ?? 0;
            long net = Math.Max(0, minutes - breakMin);
            a.WorkedHours = Math.Round((decimal)net / 60m, 4);
        }
        else
        {
            a.WorkedHours = 0m;
        }
    }

    private async Task<HrmShift?> ResolveShiftAsync(long? shiftNo, Dictionary<long, HrmShift?> cache, CancellationToken ct)
    {
        if (!shiftNo.HasValue) return null;
        if (cache.TryGetValue(shiftNo.Value, out var s)) return s;

        var shift = await _db.HrmShifts.AsNoTracking().FirstOrDefaultAsync(x => x.ShiftNo == shiftNo.Value && x.IsDeleted == 0, ct);
        cache[shiftNo.Value] = shift;
        return shift;
    }

    private static Hrm1102AttendanceRowDto ToRow(HrmAttendance a) => new()
    {
        AttendanceNo = a.AttendanceNo,
        EmployeeNo = a.EmployeeNo,
        AttDate = a.AttDate,
        InTime = a.InTime,
        OutTime = a.OutTime,
        Status = a.Status,
        LateMinutes = a.LateMinutes ?? 0,
        WorkedHours = a.WorkedHours ?? 0m,
        Source = a.Source ?? 0,
        IsLocked = a.IsLocked
    };
}
