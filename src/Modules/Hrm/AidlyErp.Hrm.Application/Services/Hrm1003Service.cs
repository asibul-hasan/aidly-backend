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

// ═══════════════════════════════════════════════════════════════════════════
// Hrm1003Service — Shift Setup (HRM_shift)
// Full port of Java Hrm1003Service (209 lines). DTO mapping, branch resolution
// with DB validation, per-branch shift-code uniqueness, time-window and minute
// validations, null/default normalization, PerformSoftDelete.
// ═══════════════════════════════════════════════════════════════════════════

public interface IHrm1003Service
{
    Task<List<Hrm1003ShiftDto>> GetListAsync(CancellationToken ct = default);
    Task<List<Hrm1003ShiftDto>> GetListByBranchAsync(long branchNo, CancellationToken ct = default);
    Task<Hrm1003ShiftDto> GetDetailAsync(long shiftNo, CancellationToken ct = default);
    Task<Hrm1003ShiftDto> SaveAsync(Hrm1003ShiftDto dto, CancellationToken ct = default);
    Task DeleteAsync(long shiftNo, CancellationToken ct = default);
}

public class Hrm1003Service : IHrm1003Service
{
    private readonly IHrmDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    public Hrm1003Service(IHrmDbContext db, ICompanyBranchContext ctx) { _db = db; _ctx = ctx; }

    public async Task<List<Hrm1003ShiftDto>> GetListAsync(CancellationToken ct = default)
    {
        long? branchNo = _ctx.BranchNo;
        return await _db.HrmShifts.AsNoTracking()
            .Where(x => x.IsDeleted == 0 && (branchNo == null || x.BranchNo == branchNo))
            .OrderBy(x => x.ShiftNo)
            .Select(x => ToDto(x))
            .ToListAsync(ct);
    }

    public async Task<List<Hrm1003ShiftDto>> GetListByBranchAsync(long branchNo, CancellationToken ct = default) =>
        await _db.HrmShifts.AsNoTracking()
            .Where(x => x.BranchNo == branchNo && x.IsDeleted == 0)
            .OrderBy(x => x.ShiftNo)
            .Select(x => ToDto(x))
            .ToListAsync(ct);

    public async Task<Hrm1003ShiftDto> GetDetailAsync(long shiftNo, CancellationToken ct = default) =>
        ToDto(await LoadLiveAsync(shiftNo, ct));

    public async Task<Hrm1003ShiftDto> SaveAsync(Hrm1003ShiftDto dto, CancellationToken ct = default)
    {
        return dto.ShiftNo.HasValue
            ? await UpdateAsync(dto.ShiftNo.Value, dto, ct)
            : await InsertAsync(dto, ct);
    }

    public async Task DeleteAsync(long shiftNo, CancellationToken ct = default)
    {
        var entity = await LoadLiveAsync(shiftNo, ct);
        entity.PerformSoftDelete(_ctx.CurrentUserNo());
        await _db.SaveChangesAsync(ct);
    }

    // ── Write paths ────────────────────────────────────────────────────────

    private async Task<Hrm1003ShiftDto> InsertAsync(Hrm1003ShiftDto dto, CancellationToken ct)
    {
        long branchNo = await ResolveBranchAsync(dto.BranchNo, ct);
        string shiftId = string.IsNullOrWhiteSpace(dto.ShiftId)
            ? await NextShiftIdAsync(branchNo, ct)
            : dto.ShiftId.Trim().ToUpperInvariant();
        string shiftName = HrmValidation.TrimRequired(dto.ShiftName, "Shift name");
        short nightShift = 0;
        ValidateTimes(dto.StartTime ?? default, dto.EndTime ?? default, nightShift);

        if (await _db.HrmShifts.AnyAsync(x => x.ShiftId == shiftId && x.BranchNo == branchNo && x.IsDeleted == 0, ct))
            throw new ValidationException($"Shift code already exists in this branch: {shiftId}");

        var entity = new HrmShift
        {
            BranchNo = branchNo,
            ShiftId = shiftId,
            ShiftName = shiftName,
            EffectiveDate = dto.EffectiveDate,
            StartTime = dto.StartTime ?? default,
            EndTime = dto.EndTime ?? default,
            GraceMinutes = dto.GraceMinutes.HasValue ? NonNegative(dto.GraceMinutes.Value, "Grace minutes") : null,
            HalfDayMinutes = dto.HalfDayMinutes.HasValue ? NonNegative(dto.HalfDayMinutes.Value, "Half-day minutes") : null,
            BreakMinutes = dto.BreakMinutes.HasValue ? NonNegative(dto.BreakMinutes.Value, "Break minutes") : null,
            WeeklyOffMask = dto.WeeklyOffMask ?? 0,
            IsNightShift = nightShift,
            Remarks = dto.Remarks,
            IsActive = NormalizeFlag(dto.IsActive, 1, "is_active"),
            IsDeleted = 0,
            CreatedBy = _ctx.CurrentUserNo(),
            CreatedAt = DateTime.UtcNow
        };
        _db.HrmShifts.Add(entity);
        await _db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    private async Task<Hrm1003ShiftDto> UpdateAsync(long shiftNo, Hrm1003ShiftDto dto, CancellationToken ct)
    {
        var entity = await LoadLiveAsync(shiftNo, ct);
        long branchNo = entity.BranchNo ?? 0;

        if (dto.ShiftId != null)
        {
            string shiftId = string.IsNullOrWhiteSpace(dto.ShiftId) ? entity.ShiftId : dto.ShiftId.Trim().ToUpperInvariant();
            var other = await _db.HrmShifts.FirstOrDefaultAsync(x =>
                x.ShiftId == shiftId && x.BranchNo == branchNo && x.IsDeleted == 0 && x.ShiftNo != shiftNo, ct);
            if (other != null) throw new ValidationException($"Shift code already in use in this branch: {shiftId}");
            entity.ShiftId = shiftId;
        }

        if (dto.ShiftName != null) entity.ShiftName = HrmValidation.TrimRequired(dto.ShiftName, "Shift name");
        if (dto.EffectiveDate.HasValue) entity.EffectiveDate = dto.EffectiveDate;

        // Re-validate times using existing values as fallback.
        entity.IsNightShift = 0; // Reset night shift for validation
        var start = dto.StartTime ?? entity.StartTime;
        var end = dto.EndTime ?? entity.EndTime;
        ValidateTimes(start, end, (short)(entity.IsNightShift ?? 0));
        entity.StartTime = start;
        entity.EndTime = end;

        if (dto.GraceMinutes.HasValue) entity.GraceMinutes = NonNegative(dto.GraceMinutes.Value, "Grace minutes");
        if (dto.HalfDayMinutes.HasValue) entity.HalfDayMinutes = NonNegative(dto.HalfDayMinutes.Value, "Half-day minutes");
        if (dto.BreakMinutes.HasValue) entity.BreakMinutes = NonNegative(dto.BreakMinutes.Value, "Break minutes");
        if (dto.WeeklyOffMask.HasValue) entity.WeeklyOffMask = dto.WeeklyOffMask.Value;
        if (dto.Remarks != null) entity.Remarks = dto.Remarks;
        if (dto.IsActive.HasValue) entity.IsActive = NormalizeFlag(dto.IsActive.Value, 1, "is_active");

        entity.UpdatedBy = _ctx.CurrentUserNo();
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private async Task<HrmShift> LoadLiveAsync(long shiftNo, CancellationToken ct) =>
        await _db.HrmShifts.FirstOrDefaultAsync(x => x.ShiftNo == shiftNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Shift not found: shiftNo={shiftNo}");

    private async Task<long> ResolveBranchAsync(long? branchNoFromDto, CancellationToken ct)
    {
        long? branchNo = branchNoFromDto ?? _ctx.BranchNo;
        if (!branchNo.HasValue) throw new ValidationException("Branch is required");
        var exists = await _db.Branches.AnyAsync(b => b.BranchNo == branchNo.Value && b.IsDeleted == 0, ct);
        if (!exists) throw new NotFoundException($"Branch not found: branchNo={branchNo}");
        return branchNo.Value;
    }

    private static void ValidateTimes(TimeOnly start, TimeOnly end, short isNightShift)
    {
        if (start == default) throw new ValidationException("Start time is required");
        if (end == default) throw new ValidationException("End time is required");
        bool night = isNightShift == 1;
        if (!night && end <= start)
            throw new ValidationException("End time must be after start time (enable Night Shift if it spans midnight)");
    }

    private static int NonNegative(int value, string fieldName)
    {
        if (value < 0) throw new ValidationException($"{fieldName} cannot be negative");
        return value;
    }

    private static short NormalizeFlag(short? value, short defaultValue, string fieldName)
    {
        if (!value.HasValue) return defaultValue;
        if (value.Value is 0 or 1) return value.Value;
        throw new ValidationException($"{fieldName} must be 0 or 1");
    }

    private async Task<string> NextShiftIdAsync(long branchNo, CancellationToken ct)
    {
        long next = await _db.HrmShifts.CountAsync(x => x.BranchNo == branchNo && x.IsDeleted == 0, ct) + 1;
        string id;
        do { id = $"SHF{next++:D4}"; }
        while (await _db.HrmShifts.AnyAsync(x => x.ShiftId == id && x.BranchNo == branchNo && x.IsDeleted == 0, ct));
        return id;
    }

    private static Hrm1003ShiftDto ToDto(HrmShift e) => new()
    {
        ShiftNo = e.ShiftNo,
        ShiftId = e.ShiftId,
        ShiftName = e.ShiftName,
        EffectiveDate = e.EffectiveDate,
        StartTime = e.StartTime,
        EndTime = e.EndTime,
        GraceMinutes = e.GraceMinutes,
        HalfDayMinutes = e.HalfDayMinutes,
        BreakMinutes = e.BreakMinutes,
        WeeklyOffMask = e.WeeklyOffMask,
        IsNightShift = e.IsNightShift,
        BranchNo = e.BranchNo,
        Remarks = e.Remarks,
        IsActive = e.IsActive,
        RowVersion = e.RowVersion
    };
}
