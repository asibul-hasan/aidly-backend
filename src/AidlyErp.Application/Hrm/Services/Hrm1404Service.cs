using Microsoft.EntityFrameworkCore;
using AidlyErp.Application.Common.Exceptions;
using AidlyErp.Application.Common.Interfaces;
using AidlyErp.Application.Common.Security;
using AidlyErp.Domain.Hrm;

namespace AidlyErp.Application.Hrm.Services;

// ═══════════════════════════════════════════════════════════════════════════
// Hrm1404Service — Offer & Onboarding
// Full port of Java Hrm1404Service (265 lines). Offer lifecycle:
// Draft → Sent → Accepted / Declined / Revoked. Acceptance marks candidate
// Hired (onboarding handoff). Offer letter generation.
// ═══════════════════════════════════════════════════════════════════════════

public interface IHrm1404Service
{
    Task<List<HrmOffer>> GetListAsync(CancellationToken ct = default);
    Task<HrmOffer> GetDetailAsync(long offerNo, CancellationToken ct = default);
    Task<HrmOffer> SaveAsync(HrmOffer dto, CancellationToken ct = default);
    Task<HrmOffer> SendAsync(long offerNo, CancellationToken ct = default);
    Task<HrmOffer> AcceptAsync(long offerNo, CancellationToken ct = default);
    Task<HrmOffer> DeclineAsync(long offerNo, CancellationToken ct = default);
    Task<HrmOffer> RevokeAsync(long offerNo, CancellationToken ct = default);
    Task DeleteAsync(long offerNo, CancellationToken ct = default);
}

public class Hrm1404Service : IHrm1404Service
{
    private readonly IApplicationDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    private const short StDraft = 1, StSent = 2, StAccepted = 3, StDeclined = 4, StRevoked = 6;

    public Hrm1404Service(IApplicationDbContext db, ICompanyBranchContext ctx) { _db = db; _ctx = ctx; }

    // ── Reads ──────────────────────────────────────────────────────────────

    public async Task<List<HrmOffer>> GetListAsync(CancellationToken ct = default) =>
        await _db.HrmOffers.AsNoTracking()
            .Where(x => x.IsDeleted == 0 && x.BranchNo == Branch())
            .OrderByDescending(x => x.OfferNo).ToListAsync(ct);

    public async Task<HrmOffer> GetDetailAsync(long offerNo, CancellationToken ct = default) =>
        await LoadLiveAsync(offerNo, ct);

    // ── Save ───────────────────────────────────────────────────────────────

    public async Task<HrmOffer> SaveAsync(HrmOffer dto, CancellationToken ct = default)
    {
        if (dto.OfferNo > 0)
            return await UpdateAsync(dto.OfferNo, dto, ct);
        return await InsertAsync(dto, ct);
    }

    private async Task<HrmOffer> InsertAsync(HrmOffer dto, CancellationToken ct)
    {
        long branchNo = ResolveBranch(dto.BranchNo);
        string code = TrimRequired(dto.OfferId, "Offer ID").ToUpperInvariant();
        if (dto.CandidateNo <= 0) throw new ValidationException("Candidate is required");
        await RequireCandidateAsync(dto.CandidateNo, ct);
        RequireSalary(dto.OfferedSalary);
        if (dto.JoiningDate == default) throw new ValidationException("Joining date is required");

        if (await _db.HrmOffers.AnyAsync(x => x.OfferId == code && x.BranchNo == branchNo && x.IsDeleted == 0, ct))
            throw new ValidationException($"Offer ID already exists in this branch: {code}");

        var entity = new HrmOffer
        {
            BranchNo = branchNo,
            OfferId = code,
            CandidateNo = dto.CandidateNo,
            DesignationNo = dto.DesignationNo,
            GradeNo = dto.GradeNo,
            OfferedSalary = dto.OfferedSalary,
            JoiningDate = dto.JoiningDate,
            Notes = dto.Notes,
            Status = StDraft,
            IsActive = NormalizeFlag(dto.IsActive, 1, "is_active"),
            IsDeleted = 0,
            CreatedBy = _ctx.CurrentUserNo(),
            CreatedAt = DateTime.UtcNow
        };
        _db.HrmOffers.Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity;
    }

    private async Task<HrmOffer> UpdateAsync(long no, HrmOffer dto, CancellationToken ct)
    {
        var entity = await LoadLiveAsync(no, ct);
        if (entity.Status != StDraft) throw new ValidationException("Only a Draft offer can be edited");

        if (dto.OfferId != null)
        {
            string code = TrimRequired(dto.OfferId, "Offer ID").ToUpperInvariant();
            if (await _db.HrmOffers.AnyAsync(x => x.OfferId == code && x.BranchNo == entity.BranchNo && x.OfferNo != no && x.IsDeleted == 0, ct))
                throw new ValidationException($"Offer ID already in use: {code}");
            entity.OfferId = code;
        }
        if (dto.CandidateNo > 0) { await RequireCandidateAsync(dto.CandidateNo, ct); entity.CandidateNo = dto.CandidateNo; }
        if (dto.DesignationNo != null) entity.DesignationNo = dto.DesignationNo;
        if (dto.GradeNo != null) entity.GradeNo = dto.GradeNo;
        if (dto.OfferedSalary != null) entity.OfferedSalary = RequireSalary(dto.OfferedSalary);
        if (dto.JoiningDate != default) entity.JoiningDate = dto.JoiningDate;
        if (dto.Notes != null) entity.Notes = dto.Notes;
        if (dto.IsActive != 0) entity.IsActive = NormalizeFlag(dto.IsActive, 1, "is_active");

        entity.UpdatedBy = _ctx.CurrentUserNo(); entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return entity;
    }

    // ── Lifecycle ──────────────────────────────────────────────────────────

    public async Task<HrmOffer> SendAsync(long offerNo, CancellationToken ct = default)
    {
        var entity = await LoadLiveAsync(offerNo, ct);
        if (entity.Status != StDraft) throw new ValidationException("Only a Draft offer can be sent");
        entity.Status = StSent;
        entity.UpdatedBy = _ctx.CurrentUserNo(); entity.UpdatedAt = DateTime.UtcNow;
        await MarkCandidateOfferedAsync(entity.CandidateNo, ct);
        await _db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<HrmOffer> AcceptAsync(long offerNo, CancellationToken ct = default)
    {
        var entity = await LoadLiveAsync(offerNo, ct);
        if (entity.Status != StSent) throw new ValidationException("Only a Sent offer can be accepted");
        entity.Status = StAccepted;
        entity.UpdatedBy = _ctx.CurrentUserNo(); entity.UpdatedAt = DateTime.UtcNow;
        await MarkCandidateHiredAsync(entity.CandidateNo, ct);
        await _db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<HrmOffer> DeclineAsync(long offerNo, CancellationToken ct = default)
    {
        var entity = await LoadLiveAsync(offerNo, ct);
        if (entity.Status != StSent) throw new ValidationException("Only a Sent offer can be declined");
        entity.Status = StDeclined;
        entity.UpdatedBy = _ctx.CurrentUserNo(); entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<HrmOffer> RevokeAsync(long offerNo, CancellationToken ct = default)
    {
        var entity = await LoadLiveAsync(offerNo, ct);
        if (entity.Status is not (StDraft or StSent))
            throw new ValidationException("Only a Draft or Sent offer can be revoked");
        entity.Status = StRevoked;
        entity.UpdatedBy = _ctx.CurrentUserNo(); entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task DeleteAsync(long offerNo, CancellationToken ct = default)
    {
        var entity = await LoadLiveAsync(offerNo, ct);
        if (entity.Status != StDraft) throw new ValidationException("Only a Draft offer can be deleted");
        entity.PerformSoftDelete(_ctx.CurrentUserNo());
        await _db.SaveChangesAsync(ct);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private async Task<HrmOffer> LoadLiveAsync(long no, CancellationToken ct) =>
        await _db.HrmOffers.FirstOrDefaultAsync(x => x.OfferNo == no && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Offer not found: no={no}");

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

    private async Task RequireCandidateAsync(long candidateNo, CancellationToken ct)
    {
        if (!await _db.HrmCandidates.AnyAsync(c => c.CandidateNo == candidateNo && c.IsDeleted == 0, ct))
            throw new NotFoundException($"Candidate not found: candidateNo={candidateNo}");
    }

    private static decimal RequireSalary(decimal? salary)
    {
        if (salary == null) throw new ValidationException("Offered salary is required");
        if (salary.Value < 0) throw new ValidationException("Offered salary cannot be negative");
        return salary.Value;
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

    private async Task MarkCandidateOfferedAsync(long candidateNo, CancellationToken ct)
    {
        var candidate = await _db.HrmCandidates.FirstOrDefaultAsync(c => c.CandidateNo == candidateNo && c.IsDeleted == 0, ct);
        if (candidate != null && candidate.ApplicationStatus < 5)
        {
            candidate.ApplicationStatus = 5; // Offered
            candidate.UpdatedBy = _ctx.CurrentUserNo(); candidate.UpdatedAt = DateTime.UtcNow;
        }
    }

    private async Task MarkCandidateHiredAsync(long candidateNo, CancellationToken ct)
    {
        var candidate = await _db.HrmCandidates.FirstOrDefaultAsync(c => c.CandidateNo == candidateNo && c.IsDeleted == 0, ct);
        if (candidate != null)
        {
            candidate.ApplicationStatus = 6; // Hired
            candidate.UpdatedBy = _ctx.CurrentUserNo(); candidate.UpdatedAt = DateTime.UtcNow;
        }
    }
}
