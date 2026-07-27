using Microsoft.EntityFrameworkCore;
using AidlyErp.Application.Common.Exceptions;
using AidlyErp.Application.Common.Interfaces;
using AidlyErp.Application.Common.Security;
using AidlyErp.Application.Hrm.Dto;
using AidlyErp.Domain.Hrm;

namespace AidlyErp.Application.Hrm.Services;

// ═══════════════════════════════════════════════════════════════════════════
// Hrm1201Service — Salary Structure CRUD
// Full port of Java Hrm1201Service (661 lines). Handles salary structure
// headers + component lines, revision templates, grade/step validation,
// line normalization (BASIC injection), and prior-active supersession.
// ═══════════════════════════════════════════════════════════════════════════

public interface IHrm1201Service
{
    Task<List<Hrm1201SalaryStructureDto>> GetListAsync(CancellationToken ct = default);
    Task<List<Hrm1201SalaryStructureDto>> GetListByEmployeeAsync(long employeeNo, CancellationToken ct = default);
    Task<Hrm1201SalaryStructureDto> GetDetailAsync(long salaryStructureNo, CancellationToken ct = default);
    Task<Hrm1201SalaryStructureDto> GetRevisionTemplateAsync(long employeeNo, DateTime? effectiveFrom, CancellationToken ct = default);
    Task<Hrm1201SalaryStructureDto> SaveAsync(Hrm1201SalaryStructureDto dto, CancellationToken ct = default);
    Task DeleteAsync(long salaryStructureNo, CancellationToken ct = default);
    Task<Hrm1201SalaryStructureDto> SyncRevisionFromMovementAsync(HrmEmployeeMovement movement, HrmEmployee employee, CancellationToken ct = default);
}

public class Hrm1201Service : IHrm1201Service
{
    private readonly IApplicationDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    private const short StActive = 1, StSuperseded = 2, StDraft = 3;
    private const string BasicComponentId = "BASIC";

    public Hrm1201Service(IApplicationDbContext db, ICompanyBranchContext ctx) { _db = db; _ctx = ctx; }

    // ── reads ──────────────────────────────────────────────────────────────

    public async Task<List<Hrm1201SalaryStructureDto>> GetListAsync(CancellationToken ct = default) =>
        await _db.HrmSalaryStructures.AsNoTracking()
            .Where(x => x.IsDeleted == 0)
            .OrderByDescending(x => x.SalaryStructureNo)
            .Select(x => ToDto(x, false))
            .ToListAsync(ct);

    public async Task<List<Hrm1201SalaryStructureDto>> GetListByEmployeeAsync(long employeeNo, CancellationToken ct = default) =>
        await _db.HrmSalaryStructures.AsNoTracking()
            .Where(x => x.EmployeeNo == employeeNo && x.IsDeleted == 0)
            .OrderByDescending(x => x.EffectiveFrom)
            .Select(x => ToDto(x, false))
            .ToListAsync(ct);

    public async Task<Hrm1201SalaryStructureDto> GetDetailAsync(long salaryStructureNo, CancellationToken ct = default)
    {
        var header = await LoadLiveAsync(salaryStructureNo, ct);
        return await ToDtoAsync(header, true, ct);
    }

    public async Task<Hrm1201SalaryStructureDto> GetRevisionTemplateAsync(long employeeNo, DateTime? effectiveFrom, CancellationToken ct)
    {
        var employee = await RequireEmployeeAsync(employeeNo, ct);
        var from = effectiveFrom ?? DateTime.UtcNow;
        var source = await FindRevisionSourceAsync(employeeNo, from, ct);

        var dto = new Hrm1201SalaryStructureDto
        {
            EmployeeNo = employeeNo,
            DesignationNo = source?.DesignationNo ?? employee.DesignationNo,
            GradeNo = source?.GradeNo ?? employee.GradeNo,
            GradeStepNo = source?.GradeStepNo,
            EffectiveFrom = from,
            EffectiveTo = null,
            BasicSalary = source?.BasicSalary ?? employee.Salary,
            GrossSalary = source?.GrossSalary ?? 0,
            Status = StActive,
            BranchNo = ResolveBranch(employee.BranchNo),
            RevisionReason = null,
            Remarks = null,
            IsActive = 1,
            Lines = source != null ? await CloneLinesAsync(source.SalaryStructureNo, ct) : new()
        };
        EnrichRevisionSource(dto, source);
        return dto;
    }

    // ── write ──────────────────────────────────────────────────────────────

    public async Task<Hrm1201SalaryStructureDto> SaveAsync(Hrm1201SalaryStructureDto dto, CancellationToken ct = default)
    {
        return dto.SalaryStructureNo.HasValue
            ? await UpdateAsync(dto.SalaryStructureNo.Value, dto, ct)
            : await InsertAsync(dto, ct);
    }

    public async Task DeleteAsync(long salaryStructureNo, CancellationToken ct = default)
    {
        var header = await LoadLiveAsync(salaryStructureNo, ct);
        var userNo = _ctx.CurrentUserNo();
        var lines = await LiveLinesAsync(salaryStructureNo, ct);
        foreach (var line in lines) line.PerformSoftDelete(userNo);
        header.PerformSoftDelete(userNo);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<Hrm1201SalaryStructureDto> SyncRevisionFromMovementAsync(HrmEmployeeMovement movement, HrmEmployee employee, CancellationToken ct)
    {
        if (movement == null) throw new ValidationException("Movement is required for salary structure sync");
        if (employee == null) throw new ValidationException("Employee is required for salary structure sync");

        var effectiveFrom = movement.EffectiveDate != default ? movement.EffectiveDate.ToDateTime(TimeOnly.MinValue) : throw new ValidationException("Effective date is required");
        var branchNo = ResolveBranch(employee.BranchNo);
        long? designationNo = employee.DesignationNo;
        long? gradeNo = employee.GradeNo;
        decimal basicSalary = movement.NewSalary ?? employee.Salary;
        var source = await FindRevisionSourceAsync(employee.EmployeeNo, effectiveFrom, ct);
        var gradeStepNo = await ResolveMatchingGradeStepNoAsync(gradeNo, basicSalary, source, ct);

        if (!NeedsRevision(source, branchNo, designationNo, gradeNo, gradeStepNo, basicSalary))
            return await ToDtoAsync(source!, true, ct);

        var lines = await BuildRevisionLinesAsync(branchNo, basicSalary, source, ct);
        var revisionReason = SummarizeMovementRevision(movement);

        if (source != null && source.Status == StActive && source.EffectiveFrom == effectiveFrom)
            return await ReviseExistingActiveAsync(source, branchNo, designationNo, gradeNo, gradeStepNo, basicSalary, lines, revisionReason, ct);

        var dto = new Hrm1201SalaryStructureDto
        {
            EmployeeNo = employee.EmployeeNo,
            DesignationNo = designationNo,
            GradeNo = gradeNo,
            GradeStepNo = gradeStepNo,
            EffectiveFrom = effectiveFrom,
            BasicSalary = basicSalary,
            Status = StActive,
            BranchNo = branchNo,
            RevisionReason = revisionReason,
            Remarks = $"Auto-synced from approved movement {movement.MovementId}",
            IsActive = 1,
            Lines = lines
        };
        return await InsertAsync(dto, ct);
    }

    // ── insert / update ────────────────────────────────────────────────────

    private async Task<Hrm1201SalaryStructureDto> InsertAsync(Hrm1201SalaryStructureDto dto, CancellationToken ct)
    {
        var branchNo = ResolveBranch(dto.BranchNo);
        var employee = await RequireEmployeeAsync(dto.EmployeeNo, ct);
        var from = RequireDate(dto.EffectiveFrom);
        ValidateDateWindow(from, dto.EffectiveTo);
        var status = ValidateStatus(dto.Status);
        var context = await ResolveStructureContextAsync(employee, dto.DesignationNo, dto.GradeNo, dto.GradeStepNo, dto.BasicSalary, ct);
        var lines = await NormalizeLinesAsync(branchNo, context.BasicSalary, RequireLines(dto.Lines), ct);

        if (status == StActive)
            await SupersedePriorActiveAsync(employee.EmployeeNo, from, ct);

        var header = new HrmSalaryStructure
        {
            BranchNo = branchNo,
            EmployeeNo = employee.EmployeeNo,
            DesignationNo = context.DesignationNo,
            GradeNo = context.GradeNo,
            GradeStepNo = context.GradeStepNo,
            EffectiveFrom = from,
            EffectiveTo = dto.EffectiveTo,
            BasicSalary = context.BasicSalary,
            Status = status,
            GrossSalary = 0,
            RevisionReason = dto.RevisionReason,
            Remarks = dto.Remarks,
            IsActive = NormalizeFlag(dto.IsActive, 1),
            IsDeleted = 0,
            CreatedBy = _ctx.CurrentUserNo(),
            CreatedAt = DateTime.UtcNow
        };
        _db.HrmSalaryStructures.Add(header);
        await _db.SaveChangesAsync(ct);

        var gross = await SaveLinesAsync(header.SalaryStructureNo, branchNo, lines, ct);
        header.GrossSalary = gross;
        await _db.SaveChangesAsync(ct);

        return await ToDtoAsync(header, true, ct);
    }

    private async Task<Hrm1201SalaryStructureDto> UpdateAsync(long salaryStructureNo, Hrm1201SalaryStructureDto dto, CancellationToken ct)
    {
        var header = await LoadLiveAsync(salaryStructureNo, ct);
        var employee = await RequireEmployeeAsync(header.EmployeeNo, ct);
        var branchNo = header.BranchNo ?? 0;

        header.DesignationNo = dto.DesignationNo ?? header.DesignationNo;
        header.GradeNo = dto.GradeNo ?? header.GradeNo;
        header.GradeStepNo = dto.GradeStepNo ?? header.GradeStepNo;
        header.EffectiveFrom = RequireDate(dto.EffectiveFrom);
        header.EffectiveTo = dto.EffectiveTo;
        header.Status = ValidateStatus(dto.Status);
        header.RevisionReason = dto.RevisionReason;
        header.Remarks = dto.Remarks;
        header.IsActive = NormalizeFlag(dto.IsActive, 1);

        var context = await ResolveStructureContextAsync(employee, header.DesignationNo, header.GradeNo, header.GradeStepNo, dto.BasicSalary, ct);
        header.DesignationNo = context.DesignationNo;
        header.GradeNo = context.GradeNo;
        header.GradeStepNo = context.GradeStepNo;
        header.BasicSalary = context.BasicSalary;
        ValidateDateWindow(header.EffectiveFrom, header.EffectiveTo);

        var lines = await NormalizeLinesAsync(branchNo, context.BasicSalary, RequireLines(dto.Lines), ct);
        var userNo = _ctx.CurrentUserNo();
        var oldLines = await LiveLinesAsync(salaryStructureNo, ct);
        foreach (var old in oldLines) old.PerformSoftDelete(userNo);

        var gross = await SaveLinesAsync(salaryStructureNo, branchNo, lines, ct);
        header.GrossSalary = gross;
        header.UpdatedBy = userNo; header.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return await ToDtoAsync(header, true, ct);
    }

    private async Task<Hrm1201SalaryStructureDto> ReviseExistingActiveAsync(
        HrmSalaryStructure header, long branchNo, long? designationNo, long? gradeNo, long? gradeStepNo,
        decimal basicSalary, List<Hrm1201StructureLineDto> lines, string revisionReason, CancellationToken ct)
    {
        header.BranchNo = branchNo;
        header.DesignationNo = designationNo;
        header.GradeNo = gradeNo;
        header.GradeStepNo = gradeStepNo;
        header.BasicSalary = basicSalary;
        header.Status = StActive;
        header.RevisionReason = revisionReason;
        header.Remarks = "Auto-synced from approved movement";
        header.IsActive = 1;

        var userNo = _ctx.CurrentUserNo();
        var oldLines = await LiveLinesAsync(header.SalaryStructureNo, ct);
        foreach (var old in oldLines) old.PerformSoftDelete(userNo);

        var normalizedLines = await NormalizeLinesAsync(branchNo, basicSalary, lines, ct);
        var gross = await SaveLinesAsync(header.SalaryStructureNo, branchNo, normalizedLines, ct);
        header.GrossSalary = gross;
        header.UpdatedBy = userNo; header.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return await ToDtoAsync(header, true, ct);
    }

    // ── line management ────────────────────────────────────────────────────

    private async Task<List<Hrm1201StructureLineDto>> CloneLinesAsync(long structureNo, CancellationToken ct)
    {
        var lines = await LiveLinesAsync(structureNo, ct);
        return lines.Select(l => new Hrm1201StructureLineDto
        {
            ComponentNo = l.ComponentNo,
            Amount = l.Amount
        }).ToList();
    }

    private async Task<decimal> SaveLinesAsync(long structureNo, long branchNo, List<Hrm1201StructureLineDto> lines, CancellationToken ct)
    {
        decimal gross = 0;
        foreach (var line in lines)
        {
            if (line.ComponentNo == null) throw new ValidationException("Each line needs a salary component");
            var component = await _db.HrmSalaryComponents.AsNoTracking()
                .FirstOrDefaultAsync(c => c.ComponentNo == line.ComponentNo && c.IsDeleted == 0, ct)
                ?? throw new NotFoundException($"Salary component not found: componentNo={line.ComponentNo}");

            decimal amount = NormalizeAmount(line.Amount);
            var dtl = new HrmSalaryStructureDtl
            {
                SalaryStructureNo = structureNo,
                ComponentNo = line.ComponentNo.Value,
                CalcType = component.CalcType,
                CalcValue = component.CalcValue,
                FormulaExpression = component.FormulaExpression,
                IsProratable = component.IsProratable,
                DisplayOrder = component.DisplayOrder,
                Amount = amount,
                BranchNo = branchNo,
                IsDeleted = 0,
                CreatedBy = _ctx.CurrentUserNo(),
                CreatedAt = DateTime.UtcNow
            };
            _db.HrmSalaryStructureDtls.Add(dtl);
            gross += amount;
        }
        await _db.SaveChangesAsync(ct);
        return gross;
    }

    private async Task<List<Hrm1201StructureLineDto>> NormalizeLinesAsync(
        long branchNo, decimal basicSalary, List<Hrm1201StructureLineDto> lines, CancellationToken ct)
    {
        var normalized = lines.Select(line => new Hrm1201StructureLineDto
        {
            StructureDtlNo = line.StructureDtlNo,
            ComponentNo = line.ComponentNo,
            Amount = NormalizeAmount(line.Amount)
        }).ToList();

        // Ensure BASIC component line exists with correct salary.
        var basicComponent = await _db.HrmSalaryComponents.AsNoTracking()
            .FirstOrDefaultAsync(c => c.ComponentId == BasicComponentId && (c.BranchNo == branchNo || c.BranchNo == null) && c.IsDeleted == 0, ct);

        if (basicComponent == null) return normalized;

        bool matched = false;
        foreach (var line in normalized)
        {
            if (basicComponent.ComponentNo == line.ComponentNo)
            {
                line.Amount = basicSalary;
                matched = true;
                break;
            }
        }

        if (!matched)
        {
            normalized.Insert(0, new Hrm1201StructureLineDto
            {
                ComponentNo = basicComponent.ComponentNo,
                Amount = basicSalary
            });
        }

        return normalized;
    }

    // ── structure context resolution ───────────────────────────────────────

    private async Task<StructureContext> ResolveStructureContextAsync(
        HrmEmployee employee, long? designationNo, long? gradeNo, long? gradeStepNo, decimal basicSalary, CancellationToken ct)
    {
        long? resolvedDesignationNo = designationNo ?? employee.DesignationNo;
        if (resolvedDesignationNo == null) throw new ValidationException("Designation is required");

        var designation = await _db.HrmDesignations.AsNoTracking()
            .FirstOrDefaultAsync(d => d.DesignationNo == resolvedDesignationNo && d.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Designation not found: designationNo={resolvedDesignationNo}");

        long? resolvedGradeNo = gradeNo ?? employee.GradeNo ?? designation.GradeNo;
        HrmGrade? grade = null;
        if (resolvedGradeNo.HasValue)
        {
            grade = await _db.HrmGrades.AsNoTracking()
                .FirstOrDefaultAsync(g => g.GradeNo == resolvedGradeNo.Value && g.IsDeleted == 0, ct)
                ?? throw new NotFoundException($"Grade not found: gradeNo={resolvedGradeNo}");
        }

        long? resolvedGradeStepNo = gradeStepNo;
        decimal resolvedBasicSalary = NormalizeAmount(basicSalary);
        if (resolvedGradeStepNo.HasValue)
        {
            var step = await _db.HrmGradeSteps.AsNoTracking()
                .FirstOrDefaultAsync(s => s.GradeStepNo == resolvedGradeStepNo.Value && s.IsDeleted == 0, ct)
                ?? throw new NotFoundException($"Grade step not found: gradeStepNo={resolvedGradeStepNo}");
            if (grade == null)
            {
                resolvedGradeNo = step.GradeNo;
                grade = await _db.HrmGrades.AsNoTracking()
                    .FirstOrDefaultAsync(g => g.GradeNo == step.GradeNo && g.IsDeleted == 0, ct)
                    ?? throw new NotFoundException($"Grade not found: gradeNo={step.GradeNo}");
            }
            if (step.GradeNo != resolvedGradeNo) throw new ValidationException("Selected step does not belong to the selected grade");
            resolvedBasicSalary = NormalizeAmount(step.BasicSalary);
        }

        if (grade != null && designation.GradeNo != null && designation.GradeNo != grade.GradeNo)
            throw new ValidationException("Selected grade does not match the designation setup");

        return new StructureContext(employee.EmployeeNo, designation.DesignationNo, grade?.GradeNo, resolvedGradeStepNo, resolvedBasicSalary);
    }

    // ── revision helpers ───────────────────────────────────────────────────

    private async Task<HrmSalaryStructure?> FindRevisionSourceAsync(long employeeNo, DateTime effectiveFrom, CancellationToken ct)
    {
        var history = await _db.HrmSalaryStructures.AsNoTracking()
            .Where(x => x.EmployeeNo == employeeNo && x.IsDeleted == 0)
            .OrderByDescending(x => x.EffectiveFrom)
            .ToListAsync(ct);

        foreach (var s in history)
        {
            if (s.EffectiveFrom <= effectiveFrom) return s;
        }
        return history.Count > 0 ? history[0] : null;
    }

    private static bool NeedsRevision(HrmSalaryStructure? source, long branchNo, long? designationNo, long? gradeNo, long? gradeStepNo, decimal basicSalary)
    {
        if (source == null) return true;
        return source.BranchNo != branchNo
            || source.DesignationNo != designationNo
            || source.GradeNo != gradeNo
            || source.GradeStepNo != gradeStepNo
            || NormalizeAmount(source.BasicSalary) != NormalizeAmount(basicSalary)
            || source.Status != StActive;
    }

    private async Task<long?> ResolveMatchingGradeStepNoAsync(long? gradeNo, decimal basicSalary, HrmSalaryStructure? source, CancellationToken ct)
    {
        if (!gradeNo.HasValue) return null;
        if (source != null && source.GradeNo == gradeNo && source.GradeStepNo != null
            && NormalizeAmount(source.BasicSalary) == NormalizeAmount(basicSalary))
            return source.GradeStepNo;

        var steps = await _db.HrmGradeSteps.AsNoTracking()
            .Where(s => s.GradeNo == gradeNo.Value && s.IsDeleted == 0)
            .OrderBy(s => s.GradeStepNo)
            .ToListAsync(ct);

        foreach (var step in steps)
        {
            if (NormalizeAmount(step.BasicSalary) == NormalizeAmount(basicSalary))
                return step.GradeStepNo;
        }
        return null;
    }

    private async Task<List<Hrm1201StructureLineDto>> BuildRevisionLinesAsync(
        long branchNo, decimal basicSalary, HrmSalaryStructure? source, CancellationToken ct)
    {
        var lines = new List<Hrm1201StructureLineDto>();
        if (source != null)
        {
            var currentLines = await LiveLinesAsync(source.SalaryStructureNo, ct);
            foreach (var currentLine in currentLines)
            {
                var component = await _db.HrmSalaryComponents.AsNoTracking()
                    .FirstOrDefaultAsync(c => c.ComponentNo == currentLine.ComponentNo && c.IsDeleted == 0, ct);
                lines.Add(new Hrm1201StructureLineDto
                {
                    ComponentNo = currentLine.ComponentNo,
                    Amount = RecalculateRevisionAmount(component, currentLine.Amount, basicSalary)
                });
            }
        }

        if (lines.Count == 0)
        {
            var basicComponent = await _db.HrmSalaryComponents.AsNoTracking()
                .FirstOrDefaultAsync(c => c.ComponentId == BasicComponentId && (c.BranchNo == branchNo || c.BranchNo == null) && c.IsDeleted == 0, ct);
            if (basicComponent != null)
            {
                lines.Add(new Hrm1201StructureLineDto
                {
                    ComponentNo = basicComponent.ComponentNo,
                    Amount = basicSalary
                });
            }
        }

        if (lines.Count == 0)
            throw new ValidationException("Unable to build a salary revision because no reusable salary lines exist for the employee");

        return lines;
    }

    private static decimal RecalculateRevisionAmount(HrmSalaryComponent? component, decimal currentAmount, decimal basicSalary)
    {
        if (component == null) return NormalizeAmount(currentAmount);
        if (BasicComponentId.Equals(component.ComponentId, StringComparison.OrdinalIgnoreCase)) return NormalizeAmount(basicSalary);
        if (component.CalcType == 2 && component.CalcValue != null) return PercentOf(basicSalary, component.CalcValue.Value);
        return NormalizeAmount(currentAmount);
    }

    private static string SummarizeMovementRevision(HrmEmployeeMovement movement)
    {
        var reason = movement.Reason?.Trim() ?? "";
        var baseStr = $"Movement {movement.MovementId}";
        if (!string.IsNullOrWhiteSpace(reason)) baseStr += $" - {reason}";
        return baseStr.Length > 120 ? baseStr[..120] : baseStr;
    }

    private async Task SupersedePriorActiveAsync(long employeeNo, DateTime newFrom, CancellationToken ct)
    {
        var priors = await _db.HrmSalaryStructures
            .Where(x => x.EmployeeNo == employeeNo && x.Status == StActive && x.IsDeleted == 0)
            .ToListAsync(ct);

        foreach (var prior in priors)
        {
            prior.Status = StSuperseded;
            var priorEnd = newFrom.AddDays(-1);
            if (prior.EffectiveTo == null || prior.EffectiveTo > priorEnd)
                prior.EffectiveTo = priorEnd;
        }
    }

    // ── load helpers ───────────────────────────────────────────────────────

    private async Task<HrmSalaryStructure> LoadLiveAsync(long no, CancellationToken ct) =>
        await _db.HrmSalaryStructures.AsNoTracking().FirstOrDefaultAsync(x => x.SalaryStructureNo == no && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Salary structure not found: salaryStructureNo={no}");

    private async Task<List<HrmSalaryStructureDtl>> LiveLinesAsync(long structureNo, CancellationToken ct) =>
        await _db.HrmSalaryStructureDtls.AsNoTracking()
            .Where(x => x.SalaryStructureNo == structureNo && x.IsDeleted == 0)
            .OrderBy(x => x.StructureDtlNo)
            .ToListAsync(ct);

    private async Task<HrmEmployee> RequireEmployeeAsync(long employeeNo, CancellationToken ct) =>
        await _db.HrmEmployees.AsNoTracking().FirstOrDefaultAsync(x => x.EmployeeNo == employeeNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Employee not found: employeeNo={employeeNo}");

    // ── validation ─────────────────────────────────────────────────────────

    private long ResolveBranch(long? branchNoFromDto)
    {
        var branchNo = branchNoFromDto ?? _ctx.BranchNo;
        if (branchNo == null) throw new ValidationException("Branch is required");
        return branchNo.Value;
    }

    private static DateTime RequireDate(DateTime? date) =>
        date ?? throw new ValidationException("Effective-from date is required");

    private static void ValidateDateWindow(DateTime from, DateTime? to)
    {
        if (to.HasValue && to.Value < from)
            throw new ValidationException("Effective-to cannot be before effective-from");
    }

    private static List<Hrm1201StructureLineDto> RequireLines(List<Hrm1201StructureLineDto>? lines) =>
        lines == null || lines.Count == 0 ? throw new ValidationException("At least one salary component line is required") : lines;

    private static short ValidateStatus(short status)
    {
        if (status is not (StActive or StSuperseded or StDraft))
            throw new ValidationException("Status must be 1 (Active), 2 (Superseded) or 3 (Draft)");
        return status;
    }

    private static short NormalizeFlag(short value, short defaultValue) =>
        value is 0 or 1 ? value : throw new ValidationException("Flag values must be 0 or 1");

    private static decimal NormalizeAmount(decimal amount) =>
        amount < 0 ? throw new ValidationException("Line amount cannot be negative") : amount;

    private static decimal PercentOf(decimal baseVal, decimal percent) =>
        NormalizeAmount(baseVal) * NormalizeAmount(percent) / 100m;

    // ── DTO mapping ────────────────────────────────────────────────────────

    private static Hrm1201SalaryStructureDto ToDto(HrmSalaryStructure h, bool withLines)
    {
        var dto = new Hrm1201SalaryStructureDto
        {
            SalaryStructureNo = h.SalaryStructureNo,
            EmployeeNo = h.EmployeeNo,
            DesignationNo = h.DesignationNo,
            GradeNo = h.GradeNo,
            GradeStepNo = h.GradeStepNo,
            EffectiveFrom = h.EffectiveFrom,
            EffectiveTo = h.EffectiveTo,
            BasicSalary = h.BasicSalary,
            GrossSalary = h.GrossSalary,
            Status = h.Status,
            BranchNo = h.BranchNo,
            RevisionReason = h.RevisionReason,
            Remarks = h.Remarks,
            IsActive = h.IsActive,
            RowVersion = h.RowVersion
        };
        EnrichRevisionSource(dto, h);
        return dto;
    }

    private async Task<Hrm1201SalaryStructureDto> ToDtoAsync(HrmSalaryStructure h, bool withLines, CancellationToken ct)
    {
        var dto = ToDto(h, false);
        if (withLines)
        {
            var lines = await LiveLinesAsync(h.SalaryStructureNo, ct);
            dto.Lines = lines.Select(l => new Hrm1201StructureLineDto
            {
                StructureDtlNo = l.StructureDtlNo,
                ComponentNo = l.ComponentNo,
                Amount = l.Amount
            }).ToList();
        }
        return dto;
    }

    private static void EnrichRevisionSource(Hrm1201SalaryStructureDto dto, HrmSalaryStructure? source)
    {
        if (dto == null || source == null) return;
        dto.SourceStructureNo = source.SalaryStructureNo;
        dto.SourceEffectiveFrom = source.EffectiveFrom;
        dto.SourceBasicSalary = source.BasicSalary;
        dto.SourceGrossSalary = source.GrossSalary;
        dto.SourceRevisionReason = source.RevisionReason;
        dto.SourceStatus = source.Status;
    }

    private record StructureContext(long EmployeeNo, long? DesignationNo, long? GradeNo, long? GradeStepNo, decimal BasicSalary);
}
