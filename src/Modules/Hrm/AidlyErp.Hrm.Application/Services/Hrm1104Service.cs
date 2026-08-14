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
// Hrm1104Service — Shift Roster
// Full port of Java Hrm1104Service (218 lines). Master + lines model,
// roster ID uniqueness, date validation, replaceLines on save,
// publish requires lines, delete cascades to lines.
// ═══════════════════════════════════════════════════════════════════════════

public interface IHrm1104Service
{
    Task<List<HrmShiftRoster>> GetListAsync(CancellationToken ct = default);
    Task<HrmShiftRoster> GetDetailAsync(long rosterNo, CancellationToken ct = default);
    Task<HrmShiftRoster> SaveAsync(HrmShiftRoster dto, CancellationToken ct = default);
    Task<HrmShiftRoster> PublishAsync(long rosterNo, CancellationToken ct = default);
    Task<HrmShiftRoster> CancelAsync(long rosterNo, CancellationToken ct = default);
    Task DeleteAsync(long rosterNo, CancellationToken ct = default);
}

public class Hrm1104Service : IHrm1104Service
{
    private readonly IHrmDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    private const short StDraft = 1, StPublished = 2, StCancelled = 3;

    public Hrm1104Service(IHrmDbContext db, ICompanyBranchContext ctx) { _db = db; _ctx = ctx; }

    // ── Reads ──────────────────────────────────────────────────────────────

    public async Task<List<HrmShiftRoster>> GetListAsync(CancellationToken ct = default) =>
        await _db.HrmShiftRosters.AsNoTracking()
            .Where(x => x.IsDeleted == 0 && x.BranchNo == Branch())
            .OrderByDescending(x => x.RosterNo).ToListAsync(ct);

    public async Task<HrmShiftRoster> GetDetailAsync(long rosterNo, CancellationToken ct = default)
    {
        var entity = await LoadLiveAsync(rosterNo, ct);
        entity.Lines = await _db.HrmShiftRosterLines.AsNoTracking()
            .Where(l => l.RosterNo == rosterNo && l.IsDeleted == 0)
            .OrderBy(l => l.RosterLineNo).ToListAsync(ct);
        return entity;
    }

    // ── Save ───────────────────────────────────────────────────────────────

    public async Task<HrmShiftRoster> SaveAsync(HrmShiftRoster dto, CancellationToken ct = default)
    {
        if (dto.RosterNo > 0)
            return await UpdateAsync(dto.RosterNo, dto, ct);
        return await InsertAsync(dto, ct);
    }

    private async Task<HrmShiftRoster> InsertAsync(HrmShiftRoster dto, CancellationToken ct)
    {
        long branchNo = ResolveBranch(dto.BranchNo);
        string code = TrimRequired(dto.RosterId, "Roster ID").ToUpperInvariant();
        if (await _db.HrmShiftRosters.AnyAsync(x => x.RosterId == code && x.BranchNo == branchNo && x.IsDeleted == 0, ct))
            throw new ValidationException($"Roster ID already exists in this branch: {code}");

        var from = RequireDate(dto.FromDate, "From date");
        var to = RequireDate(dto.ToDate, "To date");
        if (to < from) throw new ValidationException("To date cannot be before from date");

        var entity = new HrmShiftRoster
        {
            BranchNo = branchNo,
            RosterId = code,
            RosterName = TrimRequired(dto.RosterName, "Roster name"),
            FromDate = from,
            ToDate = to,
            Status = StDraft,
            IsActive = NormalizeFlag((short)(dto.IsActive ?? 1), 1, "is_active"),
            IsDeleted = 0,
            CreatedBy = _ctx.CurrentUserNo(),
            CreatedAt = DateTime.UtcNow
        };
        _db.HrmShiftRosters.Add(entity);
        await _db.SaveChangesAsync(ct);

        if (dto.Lines is { Count: > 0 })
            await ReplaceLinesAsync(entity, dto.Lines, ct);

        return entity;
    }

    private async Task<HrmShiftRoster> UpdateAsync(long no, HrmShiftRoster dto, CancellationToken ct)
    {
        var entity = await LoadLiveAsync(no, ct);
        if (entity.Status != StDraft) throw new ValidationException("Only a Draft roster can be edited");

        if (dto.RosterName != null) entity.RosterName = TrimRequired(dto.RosterName, "Roster name");
        if (dto.FromDate != default) entity.FromDate = dto.FromDate;
        if (dto.ToDate != default) entity.ToDate = dto.ToDate;
        if (entity.ToDate < entity.FromDate) throw new ValidationException("To date cannot be before from date");
        if (dto.IsActive != null) entity.IsActive = NormalizeFlag(dto.IsActive.Value, 1, "is_active");

        entity.UpdatedBy = _ctx.CurrentUserNo(); entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        if (dto.Lines != null)
            await ReplaceLinesAsync(entity, dto.Lines, ct);

        return entity;
    }

    // ── Line management ───────────────────────────────────────────────────

    private async Task ReplaceLinesAsync(HrmShiftRoster roster, List<HrmShiftRosterLine> lines, CancellationToken ct)
    {
        var userNo = _ctx.CurrentUserNo();
        var existing = await _db.HrmShiftRosterLines
            .Where(l => l.RosterNo == roster.RosterNo && l.IsDeleted == 0).ToListAsync(ct);
        foreach (var old in existing) old.PerformSoftDelete(userNo);

        foreach (var ld in lines)
        {
            if (ld.EmployeeNo <= 0 || ld.ShiftNo <= 0)
                throw new ValidationException("Each roster line needs an employee and a shift");
            _db.HrmShiftRosterLines.Add(new HrmShiftRosterLine
            {
                RosterNo = roster.RosterNo,
                EmployeeNo = ld.EmployeeNo,
                ShiftNo = ld.ShiftNo,
                WeeklyOff = ld.WeeklyOff,
                BranchNo = roster.BranchNo,
                IsActive = 1,
                IsDeleted = 0,
                CreatedBy = userNo,
                CreatedAt = DateTime.UtcNow
            });
        }
        await _db.SaveChangesAsync(ct);
    }

    // ── Workflow ───────────────────────────────────────────────────────────

    public async Task<HrmShiftRoster> PublishAsync(long rosterNo, CancellationToken ct = default)
    {
        var entity = await LoadLiveAsync(rosterNo, ct);
        if (entity.Status != StDraft) throw new ValidationException("Only a Draft roster can be published");

        var hasLines = await _db.HrmShiftRosterLines.AnyAsync(l => l.RosterNo == rosterNo && l.IsDeleted == 0, ct);
        if (!hasLines) throw new ValidationException("Add at least one employee before publishing");

        entity.Status = StPublished;
        entity.UpdatedBy = _ctx.CurrentUserNo(); entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<HrmShiftRoster> CancelAsync(long rosterNo, CancellationToken ct = default)
    {
        var entity = await LoadLiveAsync(rosterNo, ct);
        if (entity.Status == StCancelled) throw new ValidationException("Roster is already cancelled");
        entity.Status = StCancelled;
        entity.UpdatedBy = _ctx.CurrentUserNo(); entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task DeleteAsync(long rosterNo, CancellationToken ct = default)
    {
        var entity = await LoadLiveAsync(rosterNo, ct);
        if (entity.Status is not (StDraft or StCancelled))
            throw new ValidationException("Only a Draft or Cancelled roster can be deleted");

        var userNo = _ctx.CurrentUserNo();

        var lines = await _db.HrmShiftRosterLines.Where(l => l.RosterNo == rosterNo && l.IsDeleted == 0).ToListAsync(ct);
        foreach (var line in lines) line.PerformSoftDelete(userNo);

        entity.PerformSoftDelete(userNo);
        await _db.SaveChangesAsync(ct);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private async Task<HrmShiftRoster> LoadLiveAsync(long no, CancellationToken ct) =>
        await _db.HrmShiftRosters.FirstOrDefaultAsync(x => x.RosterNo == no && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Roster not found: no={no}");

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

    private static DateOnly RequireDate(DateOnly date, string field)
    {
        if (date == default) throw new ValidationException($"{field} is required");
        return date;
    }

    private static short NormalizeFlag(short value, short defaultValue, string fieldName)
    {
        if (value == defaultValue) return value;
        if (value is 0 or 1) return value;
        throw new ValidationException($"{fieldName} must be 0 or 1");
    }

    private static string TrimRequired(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ValidationException($"{fieldName} is required");
        return value.Trim();
    }
}
