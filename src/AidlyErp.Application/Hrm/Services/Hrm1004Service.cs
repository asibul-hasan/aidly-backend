using Microsoft.EntityFrameworkCore;
using AidlyErp.Application.Common.Exceptions;
using AidlyErp.Application.Common.Interfaces;
using AidlyErp.Application.Common.Security;
using AidlyErp.Application.Hrm.Dto;
using AidlyErp.Domain.Hrm;

namespace AidlyErp.Application.Hrm.Services;

// ═══════════════════════════════════════════════════════════════════════════
// Hrm1004Service — Grade / Pay-Scale Setup
// Full port of Java Hrm1004Service (296 lines). Grade CRUD with steps,
// salary band validation, step amount validation, delete cascades to steps.
// ═══════════════════════════════════════════════════════════════════════════

public interface IHrm1004Service
{
    Task<List<Hrm1004GradeDto>> GetListAsync(CancellationToken ct = default);
    Task<List<Hrm1004GradeDto>> GetListByBranchAsync(long branchNo, CancellationToken ct = default);
    Task<Hrm1004GradeDto> GetDetailAsync(long gradeNo, CancellationToken ct = default);
    Task<Hrm1004GradeDto> SaveAsync(Hrm1004GradeDto dto, CancellationToken ct = default);
    Task DeleteAsync(long gradeNo, CancellationToken ct = default);
}

public class Hrm1004Service : IHrm1004Service
{
    private readonly IApplicationDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    public Hrm1004Service(IApplicationDbContext db, ICompanyBranchContext ctx) { _db = db; _ctx = ctx; }

    // ── Reads ──────────────────────────────────────────────────────────────

    public async Task<List<Hrm1004GradeDto>> GetListAsync(CancellationToken ct = default)
    {
        long? branchNo = _ctx.BranchNo;
        var grades = await _db.HrmGrades.AsNoTracking()
            .Where(x => x.IsDeleted == 0 && (branchNo == null || x.BranchNo == branchNo))
            .OrderBy(x => x.GradeNo).ToListAsync(ct);
        return await MapGradesAsync(grades, ct);
    }

    public async Task<List<Hrm1004GradeDto>> GetListByBranchAsync(long branchNo, CancellationToken ct = default)
    {
        var grades = await _db.HrmGrades.AsNoTracking()
            .Where(x => x.BranchNo == branchNo && x.IsDeleted == 0)
            .OrderBy(x => x.GradeNo).ToListAsync(ct);
        return await MapGradesAsync(grades, ct);
    }

    public async Task<Hrm1004GradeDto> GetDetailAsync(long gradeNo, CancellationToken ct = default)
    {
        var grade = await LoadLiveAsync(gradeNo, ct);
        return await ToDtoAsync(grade, ct);
    }

    // ── Save ───────────────────────────────────────────────────────────────

    public async Task<Hrm1004GradeDto> SaveAsync(Hrm1004GradeDto dto, CancellationToken ct = default)
    {
        return dto.GradeNo.HasValue
            ? await UpdateAsync(dto.GradeNo.Value, dto, ct)
            : await InsertAsync(dto, ct);
    }

    public async Task DeleteAsync(long gradeNo, CancellationToken ct = default)
    {
        var entity = await LoadLiveAsync(gradeNo, ct);
        var userNo = _ctx.CurrentUserNo();
        entity.PerformSoftDelete(userNo);

        // Cascade: soft-delete all child grade steps.
        var steps = await _db.HrmGradeSteps.Where(s => s.GradeNo == gradeNo && s.IsDeleted == 0).ToListAsync(ct);
        foreach (var step in steps) step.PerformSoftDelete(userNo);

        await _db.SaveChangesAsync(ct);
    }

    // ── Write paths ────────────────────────────────────────────────────────

    private async Task<Hrm1004GradeDto> InsertAsync(Hrm1004GradeDto dto, CancellationToken ct)
    {
        long branchNo = await ResolveBranchAsync(dto.BranchNo, ct);
        string gradeId = string.IsNullOrWhiteSpace(dto.GradeId) ? await NextGradeIdAsync(branchNo, ct) : dto.GradeId.Trim().ToUpperInvariant();
        string gradeName = HrmValidation.TrimRequired(dto.GradeName, "Grade name");
        ValidateSalaryBand(dto.MinSalary, dto.MaxSalary);
        ValidateGradeSteps(dto.Steps, dto.MinSalary, dto.MaxSalary);


        if (await _db.HrmGrades.AnyAsync(x => x.GradeId == gradeId && x.BranchNo == branchNo && x.IsDeleted == 0, ct))
            throw new ValidationException($"Grade code already exists in this branch: {gradeId}");

        var entity = new HrmGrade
        {
            BranchNo = branchNo,
            GradeId = gradeId,
            GradeName = gradeName,
            RankOrder = dto.RankOrder,
            MinSalary = dto.MinSalary,
            MaxSalary = dto.MaxSalary,
            IsOvertimeEligible = HrmValidation.NormalizeFlag(dto.IsOvertimeEligible ?? 0, 0, "is_overtime_eligible"),
            Remarks = dto.Remarks,
            IsActive = HrmValidation.NormalizeFlag(dto.IsActive ?? 1, 1, "is_active"),
            IsDeleted = 0,
            CreatedBy = _ctx.CurrentUserNo(),
            CreatedAt = DateTime.UtcNow
        };
        _db.HrmGrades.Add(entity);
        await _db.SaveChangesAsync(ct);

        await ReplaceStepsAsync(entity.GradeNo, dto.Steps, entity.MinSalary ?? 0, entity.MaxSalary ?? 0, ct);
        return await ToDtoAsync(entity, ct);
    }

    private async Task<Hrm1004GradeDto> UpdateAsync(long gradeNo, Hrm1004GradeDto dto, CancellationToken ct)
    {
        var entity = await LoadLiveAsync(gradeNo, ct);
        long branchNo = entity.BranchNo ?? 0; // Branch is immutable for existing grade.

        if (dto.GradeId != null)
        {
            string gradeId = string.IsNullOrWhiteSpace(dto.GradeId) ? entity.GradeId : dto.GradeId.Trim().ToUpperInvariant();
            var other = await _db.HrmGrades.FirstOrDefaultAsync(x =>
                x.GradeId == gradeId && x.BranchNo == branchNo && x.IsDeleted == 0 && x.GradeNo != gradeNo, ct);
            if (other != null) throw new ValidationException($"Grade code already in use in this branch: {gradeId}");
            entity.GradeId = gradeId;
        }

        if (dto.GradeName != null) entity.GradeName = HrmValidation.TrimRequired(dto.GradeName, "Grade name");
        if (dto.RankOrder.HasValue) entity.RankOrder = dto.RankOrder;

        decimal minSalary = dto.MinSalary > 0 ? dto.MinSalary : (entity.MinSalary ?? 0);
        decimal maxSalary = dto.MaxSalary > 0 ? dto.MaxSalary : (entity.MaxSalary ?? 0);
        ValidateSalaryBand(minSalary, maxSalary);
        ValidateGradeSteps(dto.Steps, minSalary, maxSalary);
        entity.MinSalary = minSalary;
        entity.MaxSalary = maxSalary;

        if (dto.IsOvertimeEligible.HasValue) entity.IsOvertimeEligible = HrmValidation.NormalizeFlag(dto.IsOvertimeEligible.Value, 0, "is_overtime_eligible");
        if (dto.Remarks != null) entity.Remarks = dto.Remarks;
        if (dto.IsActive.HasValue) entity.IsActive = HrmValidation.NormalizeFlag(dto.IsActive.Value, 1, "is_active");

        entity.UpdatedBy = _ctx.CurrentUserNo(); entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await ReplaceStepsAsync(gradeNo, dto.Steps, entity.MinSalary ?? 0, entity.MaxSalary ?? 0, ct);
        return await ToDtoAsync(entity, ct);
    }

    // ── Grade steps ────────────────────────────────────────────────────────

    private async Task ReplaceStepsAsync(long gradeNo, List<Hrm1004GradeStepDto>? steps, decimal minSalary, decimal maxSalary, CancellationToken ct)
    {
        // Soft-delete existing steps.
        var existing = await _db.HrmGradeSteps.Where(s => s.GradeNo == gradeNo && s.IsDeleted == 0).ToListAsync(ct);
        var userNo = _ctx.CurrentUserNo();
        foreach (var step in existing) step.PerformSoftDelete(userNo);

        if (steps == null || steps.Count == 0) return;

        foreach (var dto in steps)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.StepName) || !dto.Amount.HasValue) continue;
            ValidateStepAmount(dto.StepName, dto.Amount.Value, minSalary, maxSalary);

            var step = new HrmGradeStep
            {
                GradeNo = gradeNo,
                StepName = dto.StepName.Trim(),
                BasicSalary = dto.Amount.Value,
                IsActive = HrmValidation.NormalizeFlag(dto.IsActive ?? 1, 1, "step is_active"),
                IsDeleted = 0,
                CreatedBy = userNo,
                CreatedAt = DateTime.UtcNow
            };
            _db.HrmGradeSteps.Add(step);
        }
        await _db.SaveChangesAsync(ct);
    }

    // ── Validation ─────────────────────────────────────────────────────────

    private static void ValidateSalaryBand(decimal minSalary, decimal maxSalary)
    {
        if (minSalary < 0) throw new ValidationException("Minimum salary cannot be negative");
        if (maxSalary < 0) throw new ValidationException("Maximum salary cannot be negative");
        if (minSalary > maxSalary) throw new ValidationException("Minimum salary cannot be greater than maximum salary");
    }

    private static void ValidateGradeSteps(List<Hrm1004GradeStepDto>? steps, decimal minSalary, decimal maxSalary)
    {
        if (steps == null) return;
        foreach (var step in steps)
        {
            if (step == null || string.IsNullOrWhiteSpace(step.StepName) || !step.Amount.HasValue) continue;
            ValidateStepAmount(step.StepName, step.Amount.Value, minSalary, maxSalary);
        }
    }

    private static void ValidateStepAmount(string stepName, decimal amount, decimal minSalary, decimal maxSalary)
    {
        if (amount < 0) throw new ValidationException($"{stepName} amount cannot be negative");
        if (amount < minSalary) throw new ValidationException($"{stepName} amount cannot be lower than the grade minimum salary");
        if (amount > maxSalary) throw new ValidationException($"{stepName} amount cannot be higher than the grade maximum salary");
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private async Task<HrmGrade> LoadLiveAsync(long gradeNo, CancellationToken ct) =>
        await _db.HrmGrades.AsNoTracking().FirstOrDefaultAsync(x => x.GradeNo == gradeNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Grade not found: gradeNo={gradeNo}");

    private async Task<long> ResolveBranchAsync(long? branchNoFromDto, CancellationToken ct)
    {
        var branchNo = branchNoFromDto ?? _ctx.BranchNo;
        if (!branchNo.HasValue) throw new ValidationException("Branch is required");
        var exists = await _db.Branches.AnyAsync(b => b.BranchNo == branchNo.Value && b.IsDeleted == 0, ct);
        if (!exists) throw new NotFoundException($"Branch not found: branchNo={branchNo}");
        return branchNo.Value;
    }

    private async Task<string> NextGradeIdAsync(long branchNo, CancellationToken ct)
    {
        long next = await _db.HrmGrades.CountAsync(x => x.BranchNo == branchNo && x.IsDeleted == 0, ct) + 1;
        string id;
        do { id = $"GRD{next++:D4}"; }
        while (await _db.HrmGrades.AnyAsync(x => x.GradeId == id && x.BranchNo == branchNo && x.IsDeleted == 0, ct));
        return id;
    }

    // ── DTO mapping ────────────────────────────────────────────────────────

    private async Task<List<Hrm1004GradeDto>> MapGradesAsync(List<HrmGrade> grades, CancellationToken ct)
    {
        var result = new List<Hrm1004GradeDto>();
        foreach (var g in grades) result.Add(await ToDtoAsync(g, ct));
        return result;
    }

    private async Task<Hrm1004GradeDto> ToDtoAsync(HrmGrade e, CancellationToken ct)
    {
        var steps = await _db.HrmGradeSteps.AsNoTracking()
            .Where(s => s.GradeNo == e.GradeNo && s.IsDeleted == 0)
            .OrderBy(s => s.GradeStepNo)
            .Select(s => new Hrm1004GradeStepDto
            {
                GradeStepNo = s.GradeStepNo,
                GradeNo = s.GradeNo,
                StepName = s.StepName,
                Amount = s.BasicSalary,
                IsActive = s.IsActive,
                RowVersion = s.RowVersion
            })
            .ToListAsync(ct);

        return new Hrm1004GradeDto
        {
            GradeNo = e.GradeNo,
            GradeId = e.GradeId,
            GradeName = e.GradeName,
            RankOrder = e.RankOrder,
            MinSalary = e.MinSalary ?? 0,
            MaxSalary = e.MaxSalary ?? 0,
            IsOvertimeEligible = e.IsOvertimeEligible,
            Steps = steps,
            BranchNo = e.BranchNo,
            Remarks = e.Remarks,
            IsActive = e.IsActive,
            RowVersion = e.RowVersion
        };
    }
}
