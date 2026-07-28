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
// Hrm1402Service — Candidate / Application
// Full port of Java Hrm1402Service (207 lines). Branch-scoped candidate-code
// uniqueness, application-status validation (1–8), email trimToNull,
// markOffered/markHired hooks for the offer flow, delete guard for hired.
// ═══════════════════════════════════════════════════════════════════════════

public interface IHrm1402Service
{
    Task<List<HrmCandidate>> GetListAsync(CancellationToken ct = default);
    Task<List<HrmCandidate>> GetListByBranchAsync(long branchNo, CancellationToken ct = default);
    Task<List<HrmCandidate>> GetOfferableAsync(CancellationToken ct = default);
    Task<HrmCandidate> GetDetailAsync(long candidateNo, CancellationToken ct = default);
    Task<HrmCandidate> SaveAsync(HrmCandidate dto, CancellationToken ct = default);
    Task<HrmCandidate> MarkOfferedAsync(long candidateNo, CancellationToken ct = default);
    Task<HrmCandidate> MarkHiredAsync(long candidateNo, CancellationToken ct = default);
    Task DeleteAsync(long candidateNo, CancellationToken ct = default);
}

public class Hrm1402Service : IHrm1402Service
{
    private readonly IHrmDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    private const short StSelected = 4, StOffered = 5, StHired = 6;

    public Hrm1402Service(IHrmDbContext db, ICompanyBranchContext ctx) { _db = db; _ctx = ctx; }

    // ── Reads ──────────────────────────────────────────────────────────────

    public async Task<List<HrmCandidate>> GetListAsync(CancellationToken ct = default) =>
        await _db.HrmCandidates.AsNoTracking()
            .Where(x => x.IsDeleted == 0 && x.BranchNo == Branch())
            .OrderByDescending(x => x.CandidateNo).ToListAsync(ct);

    public async Task<List<HrmCandidate>> GetListByBranchAsync(long branchNo, CancellationToken ct = default) =>
        await _db.HrmCandidates.AsNoTracking()
            .Where(x => x.BranchNo == branchNo && x.IsDeleted == 0)
            .OrderByDescending(x => x.CandidateNo).ToListAsync(ct);

    public async Task<List<HrmCandidate>> GetOfferableAsync(CancellationToken ct = default) =>
        await _db.HrmCandidates.AsNoTracking()
            .Where(x => x.IsDeleted == 0 && (x.ApplicationStatus == StSelected || x.ApplicationStatus == StOffered))
            .OrderByDescending(x => x.CandidateNo).ToListAsync(ct);

    public async Task<HrmCandidate> GetDetailAsync(long candidateNo, CancellationToken ct = default) =>
        await LoadLiveAsync(candidateNo, ct);

    // ── Save ───────────────────────────────────────────────────────────────

    public async Task<HrmCandidate> SaveAsync(HrmCandidate dto, CancellationToken ct = default)
    {
        if (dto.CandidateNo > 0)
            return await UpdateAsync(dto.CandidateNo, dto, ct);
        return await InsertAsync(dto, ct);
    }

    private async Task<HrmCandidate> InsertAsync(HrmCandidate dto, CancellationToken ct)
    {
        long branchNo = ResolveBranch(dto.BranchNo);
        string code = TrimRequired(dto.CandidateId, "Candidate ID").ToUpperInvariant();
        string name = TrimRequired(dto.FullName, "Full name");
        string mobile = TrimRequired(dto.MobileNumber, "Mobile number");

        if (await _db.HrmCandidates.AnyAsync(x => x.CandidateId == code && x.BranchNo == branchNo && x.IsDeleted == 0, ct))
            throw new ValidationException($"Candidate ID already exists in this branch: {code}");

        var entity = new HrmCandidate
        {
            BranchNo = branchNo,
            CandidateId = code,
            FullName = name,
            MobileNumber = mobile,
            Email = TrimToNull(dto.Email),
            Source = dto.Source,
            RequisitionNo = dto.RequisitionNo,
            ApplicationStatus = ValidateStatus(dto.ApplicationStatus, 1),
            ResumeFileNo = dto.ResumeFileNo,
            Remarks = dto.Remarks,
            IsActive = NormalizeFlag(dto.IsActive, 1, "is_active"),
            IsDeleted = 0,
            CreatedBy = _ctx.CurrentUserNo(),
            CreatedAt = DateTime.UtcNow
        };
        _db.HrmCandidates.Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity;
    }

    private async Task<HrmCandidate> UpdateAsync(long no, HrmCandidate dto, CancellationToken ct)
    {
        var entity = await LoadLiveAsync(no, ct);

        if (dto.CandidateId != null)
        {
            string code = TrimRequired(dto.CandidateId, "Candidate ID").ToUpperInvariant();
            if (await _db.HrmCandidates.AnyAsync(x => x.CandidateId == code && x.BranchNo == entity.BranchNo && x.CandidateNo != no && x.IsDeleted == 0, ct))
                throw new ValidationException($"Candidate ID already in use: {code}");
            entity.CandidateId = code;
        }
        if (dto.FullName != null) entity.FullName = TrimRequired(dto.FullName, "Full name");
        if (dto.MobileNumber != null) entity.MobileNumber = TrimRequired(dto.MobileNumber, "Mobile number");
        if (dto.Email != null) entity.Email = TrimToNull(dto.Email);
        if (dto.Source != null) entity.Source = dto.Source;
        if (dto.RequisitionNo != null) entity.RequisitionNo = dto.RequisitionNo;
        if (dto.ApplicationStatus != 0) entity.ApplicationStatus = ValidateStatus(dto.ApplicationStatus, entity.ApplicationStatus);
        if (dto.ResumeFileNo != null) entity.ResumeFileNo = dto.ResumeFileNo;
        if (dto.Remarks != null) entity.Remarks = dto.Remarks;
        if (dto.IsActive != 0) entity.IsActive = NormalizeFlag(dto.IsActive, 1, "is_active");

        entity.UpdatedBy = _ctx.CurrentUserNo(); entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return entity;
    }

    // ── Hooks used by the offer flow (HRM_1404) ───────────────────────────

    public async Task<HrmCandidate> MarkOfferedAsync(long candidateNo, CancellationToken ct = default)
    {
        var entity = await LoadLiveAsync(candidateNo, ct);
        if (entity.ApplicationStatus < StOffered)
        {
            entity.ApplicationStatus = StOffered;
            entity.UpdatedBy = _ctx.CurrentUserNo(); entity.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        return entity;
    }

    public async Task<HrmCandidate> MarkHiredAsync(long candidateNo, CancellationToken ct = default)
    {
        var entity = await LoadLiveAsync(candidateNo, ct);
        entity.ApplicationStatus = StHired;
        entity.UpdatedBy = _ctx.CurrentUserNo(); entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task DeleteAsync(long candidateNo, CancellationToken ct = default)
    {
        var entity = await LoadLiveAsync(candidateNo, ct);
        if (entity.ApplicationStatus == StHired)
            throw new ValidationException("A hired candidate cannot be deleted");
        entity.PerformSoftDelete(_ctx.CurrentUserNo());
        await _db.SaveChangesAsync(ct);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private async Task<HrmCandidate> LoadLiveAsync(long no, CancellationToken ct) =>
        await _db.HrmCandidates.FirstOrDefaultAsync(x => x.CandidateNo == no && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Candidate not found: no={no}");

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

    private static short ValidateStatus(short status, short fallback)
    {
        short value = status != 0 ? status : fallback;
        if (value < 1 || value > 8) throw new ValidationException("Application status must be 1–8");
        return value;
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

    private static string? TrimToNull(string? value)
    {
        if (value == null) return null;
        var t = value.Trim();
        return t.Length == 0 ? null : t;
    }
}
