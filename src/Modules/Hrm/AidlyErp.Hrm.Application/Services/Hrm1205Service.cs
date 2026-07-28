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
// Hrm1205Service — Bonus / Festival
// Full port of Java Hrm1205Service (437 lines). Scope filtering by designation
// or employee, bonus basis resolution from salary structure, approval engine,
// delete cascades to lines + scopes.
// ═══════════════════════════════════════════════════════════════════════════

public interface IHrm1205Service
{
    Task<List<HrmBonusRun>> GetListAsync(CancellationToken ct = default);
    Task<HrmBonusRun> GetDetailAsync(long no, CancellationToken ct = default);
    Task<HrmBonusRun> SaveAsync(HrmBonusRun dto, CancellationToken ct = default);
    Task<HrmBonusRun> CalculateAsync(long no, CancellationToken ct = default);
    Task<HrmBonusRun> ApproveAsync(long no, CancellationToken ct = default);
    Task<HrmBonusRun> RejectAsync(long no, CancellationToken ct = default);
    Task<HrmBonusRun> MarkPaidAsync(long no, CancellationToken ct = default);
    Task<HrmBonusRun> CancelAsync(long no, CancellationToken ct = default);
    Task DeleteAsync(long no, CancellationToken ct = default);
    Task ApplyApprovalOutcomeAsync(long bonusRunNo, bool approved, CancellationToken ct = default);
}

public class Hrm1205Service : IHrm1205Service
{
    private readonly IHrmDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    private readonly IApprovalService _approvalService;
    public const string DocType = "HRM_BONUS";
    private const short StDraft = 1, StCalculated = 2, StApproved = 3, StPaid = 4, StCancelled = 5;
    private const short CalcBasic = 1, CalcGross = 2;

    public Hrm1205Service(IHrmDbContext db, ICompanyBranchContext ctx, IApprovalService approvalService)
    {
        _db = db;
        _ctx = ctx;
        _approvalService = approvalService;
    }

    // ── Reads ──────────────────────────────────────────────────────────────

    public async Task<List<HrmBonusRun>> GetListAsync(CancellationToken ct = default) =>
        await _db.HrmBonusRuns.AsNoTracking()
            .Where(x => x.IsDeleted == 0 && x.BranchNo == Branch())
            .OrderByDescending(x => x.BonusRunNo).ToListAsync(ct);

    public async Task<HrmBonusRun> GetDetailAsync(long no, CancellationToken ct = default) =>
        await LoadLiveAsync(no, ct);

    // ── Save ───────────────────────────────────────────────────────────────

    public async Task<HrmBonusRun> SaveAsync(HrmBonusRun dto, CancellationToken ct = default)
    {
        if (dto.BonusRunNo > 0)
            return await UpdateAsync(dto.BonusRunNo, dto, ct);
        return await InsertAsync(dto, ct);
    }

    private async Task<HrmBonusRun> InsertAsync(HrmBonusRun dto, CancellationToken ct)
    {
        long branchNo = ResolveBranch(dto.BranchNo);
        string bonusId = TrimRequired(dto.BonusId, "Bonus ID").ToUpperInvariant();

        if (await _db.HrmBonusRuns.AnyAsync(x => x.BonusId == bonusId && x.BranchNo == branchNo && x.IsDeleted == 0, ct))
            throw new ValidationException($"Bonus ID already exists in this branch: {bonusId}");

        var entity = new HrmBonusRun
        {
            BranchNo = branchNo,
            CompanyNo = _ctx.CompanyNo ?? 0,
            BonusId = bonusId,
            BonusTitle = TrimRequired(dto.BonusTitle, "Bonus name"),
            BonusType = dto.BonusType,
            BonusDate = RequireDate(dto.BonusDate, "Bonus date"),
            PayPeriod = dto.PayPeriod,
            CalculationType = NormalizeCalculationType(dto.CalculationType),
            ApplicabilityType = NormalizeApplicabilityType(dto.ApplicabilityType),
            PercentageOfBasic = NonNeg(dto.PercentageOfBasic, "Percentage"),
            FixedAmount = NonNeg(dto.FixedAmount, "Fixed amount"),
            Status = StDraft,
            IsActive = NormalizeFlag(dto.IsActive, 1, "is_active"),
            IsDeleted = 0,
            CreatedBy = _ctx.CurrentUserNo(),
            CreatedAt = DateTime.UtcNow
        };
        _db.HrmBonusRuns.Add(entity);
        await _db.SaveChangesAsync(ct);

        entity.SelectedDesignationNos = dto.SelectedDesignationNos;
        entity.SelectedEmployeeNos = dto.SelectedEmployeeNos;
        await SyncScopesAsync(entity, null, ct);

        return entity;
    }

    private async Task<HrmBonusRun> UpdateAsync(long no, HrmBonusRun dto, CancellationToken ct)
    {
        var entity = await LoadLiveAsync(no, ct);
        if (entity.Status is not (StDraft or StCalculated))
            throw new ValidationException("Only a Draft or Calculated bonus run can be edited");

        if (dto.BonusTitle != null) entity.BonusTitle = TrimRequired(dto.BonusTitle, "Bonus name");
        if (dto.BonusType != null) entity.BonusType = dto.BonusType;
        if (dto.BonusDate != null) entity.BonusDate = dto.BonusDate;
        if (dto.PayPeriod != null) entity.PayPeriod = dto.PayPeriod;
        if (dto.CalculationType != null) entity.CalculationType = NormalizeCalculationType(dto.CalculationType);
        if (dto.ApplicabilityType != null) entity.ApplicabilityType = NormalizeApplicabilityType(dto.ApplicabilityType);
        if (dto.PercentageOfBasic != null) entity.PercentageOfBasic = NonNeg(dto.PercentageOfBasic, "Percentage");
        if (dto.FixedAmount != null) entity.FixedAmount = NonNeg(dto.FixedAmount, "Fixed amount");
        if (dto.IsActive != null) entity.IsActive = NormalizeFlag(dto.IsActive, 1, "is_active");

        entity.UpdatedBy = _ctx.CurrentUserNo(); entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        entity.SelectedDesignationNos = dto.SelectedDesignationNos;
        entity.SelectedEmployeeNos = dto.SelectedEmployeeNos;
        await SyncScopesAsync(entity, _ctx.CurrentUserNo(), ct);

        return entity;
    }

    // ── Calculate ──────────────────────────────────────────────────────────

    public async Task<HrmBonusRun> CalculateAsync(long no, CancellationToken ct = default)
    {
        var run = await LoadLiveAsync(no, ct);
        if (run.Status is not (StDraft or StCalculated))
            throw new ValidationException("Only a Draft or Calculated run can be (re)calculated");

        var userNo = _ctx.CurrentUserNo();

        // Soft-delete previous lines.
        var oldLines = await _db.HrmBonusLines.Where(l => l.BonusRunNo == no && l.IsDeleted == 0).ToListAsync(ct);
        foreach (var old in oldLines) old.PerformSoftDelete(userNo);

        decimal pct = run.PercentageOfBasic ?? 0m;
        decimal fixedAmt = run.FixedAmount ?? 0m;
        decimal total = 0;
        int count = 0;
        var asOf = run.BonusDate ?? DateTime.UtcNow;

        // Load scope filters.
        var selectedDesigNos = await SelectedDesignationNosAsync(run.BonusRunNo, ct);
        var selectedEmpNos = await SelectedEmployeeNosAsync(run.BonusRunNo, ct);

        // Load active employees in the branch.
        var employees = await _db.HrmEmployees
            .Where(e => e.BranchNo == run.BranchNo && e.IsDeleted == 0 && e.IsActive == 1)
            .ToListAsync(ct);

        foreach (var emp in employees)
        {
            // Scope filtering: employee scope takes priority over designation scope.
            if (selectedEmpNos.Count > 0 && !selectedEmpNos.Contains(emp.EmployeeNo)) continue;
            if (selectedEmpNos.Count == 0 && selectedDesigNos.Count > 0 && !selectedDesigNos.Contains(emp.DesignationNo)) continue;

            var structure = await ActiveStructureForAsync(emp.EmployeeNo, asOf, ct);
            decimal basis = ResolveBonusBasis(run, emp, structure);
            decimal bonus = basis * pct / 100m + fixedAmt;

            var line = new HrmBonusLine
            {
                BonusRunNo = run.BonusRunNo,
                EmployeeNo = emp.EmployeeNo,
                BaseSalary = basis,
                BonusAmount = bonus,
                BranchNo = run.BranchNo,
                IsActive = 1,
                IsDeleted = 0,
                CreatedBy = userNo,
                CreatedAt = DateTime.UtcNow
            };
            _db.HrmBonusLines.Add(line);
            total += bonus;
            count++;
        }

        run.TotalAmount = total;
        run.EmployeeCount = count;
        run.Status = StCalculated;
        run.UpdatedBy = userNo; run.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return run;
    }

    // ── Workflow ───────────────────────────────────────────────────────────

    public async Task<HrmBonusRun> ApproveAsync(long no, CancellationToken ct = default)
    {
        var run = await LoadLiveAsync(no, ct);
        if (run.Status != StCalculated) throw new ValidationException("Only a Calculated run can be approved");

        if (run.ApprovalRequestNo == null)
        {
            var outcome = await _approvalService.RaiseAsync(DocType, run.BonusRunNo, $"BON-{run.BonusRunNo}", run.TotalAmount, ct);
            if (outcome.AutoApproved)
            {
                await ApplyApprovalOutcomeAsync(run.BonusRunNo, true, ct);
            }
            else
            {
                run.ApprovalRequestNo = outcome.ApprovalRequestNo;
                run.UpdatedBy = _ctx.CurrentUserNo(); run.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);
            }
        }
        else
        {
            await _approvalService.ActAsync(run.ApprovalRequestNo.Value, true, null, ct);
        }
        return await LoadLiveAsync(no, ct);
    }

    public async Task<HrmBonusRun> RejectAsync(long no, CancellationToken ct = default)
    {
        var run = await LoadLiveAsync(no, ct);
        if (run.ApprovalRequestNo == null) throw new ValidationException("No approval request — approve first");
        await _approvalService.ActAsync(run.ApprovalRequestNo.Value, false, null, ct);
        return await LoadLiveAsync(no, ct);
    }

    public async Task ApplyApprovalOutcomeAsync(long bonusRunNo, bool approved, CancellationToken ct = default)
    {
        var run = await LoadLiveAsync(bonusRunNo, ct);
        if (!approved)
        {
            run.ApprovalRequestNo = null;
            run.Status = StCalculated;
            run.UpdatedBy = _ctx.CurrentUserNo(); run.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return;
        }
        run.Status = StApproved;
        run.ApprovedBy = _ctx.CurrentUserNo();
        run.ApprovedAt = DateTime.UtcNow;
        run.UpdatedBy = _ctx.CurrentUserNo(); run.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<HrmBonusRun> MarkPaidAsync(long no, CancellationToken ct = default)
    {
        var run = await LoadLiveAsync(no, ct);
        if (run.Status != StApproved) throw new ValidationException("Only an Approved run can be marked Paid");
        run.Status = StPaid;
        run.UpdatedBy = _ctx.CurrentUserNo(); run.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return run;
    }

    public async Task<HrmBonusRun> CancelAsync(long no, CancellationToken ct = default)
    {
        var run = await LoadLiveAsync(no, ct);
        if (run.Status is StApproved or StPaid)
            throw new ValidationException("An approved/paid bonus run is immutable");
        if (run.Status == StCancelled) throw new ValidationException("Run is already cancelled");
        run.Status = StCancelled;
        run.UpdatedBy = _ctx.CurrentUserNo(); run.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return run;
    }

    public async Task DeleteAsync(long no, CancellationToken ct = default)
    {
        var run = await LoadLiveAsync(no, ct);
        if (run.Status is not (StDraft or StCancelled))
            throw new ValidationException("Only a Draft or Cancelled run can be deleted");

        var userNo = _ctx.CurrentUserNo();

        var lines = await _db.HrmBonusLines.Where(l => l.BonusRunNo == no && l.IsDeleted == 0).ToListAsync(ct);
        foreach (var line in lines) line.PerformSoftDelete(userNo);

        var desigScopes = await _db.HrmBonusScopeDesignations.Where(s => s.BonusRunNo == no && s.IsDeleted == 0).ToListAsync(ct);
        foreach (var scope in desigScopes) scope.PerformSoftDelete(userNo);

        var empScopes = await _db.HrmBonusScopeEmployees.Where(s => s.BonusRunNo == no && s.IsDeleted == 0).ToListAsync(ct);
        foreach (var scope in empScopes) scope.PerformSoftDelete(userNo);

        run.PerformSoftDelete(userNo);
        await _db.SaveChangesAsync(ct);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private async Task<HrmBonusRun> LoadLiveAsync(long no, CancellationToken ct) =>
        await _db.HrmBonusRuns.FirstOrDefaultAsync(x => x.BonusRunNo == no && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Bonus run not found: no={no}");

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

    private static decimal NonNeg(decimal? v, string field)
    {
        if (v == null) return 0m;
        if (v.Value < 0) throw new ValidationException($"{field} cannot be negative");
        return v.Value;
    }

    private static short NormalizeCalculationType(short? value)
    {
        if (value == null) return CalcBasic;
        if (value is not (CalcBasic or CalcGross))
            throw new ValidationException("Calculation type must be Basic or Gross");
        return value.Value;
    }

    private static DateTime? RequireDate(DateTime? value, string fieldName)
    {
        if (value == null) throw new ValidationException($"{fieldName} is required");
        return value;
    }

    private static short NormalizeApplicabilityType(short? value)
    {
        if (value == null) return 1;
        if (value < 1 || value > 3)
            throw new ValidationException("Applicability type is invalid");
        return value.Value;
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

    private async Task<HrmSalaryStructure?> ActiveStructureForAsync(long employeeNo, DateTime asOf, CancellationToken ct)
    {
        return await _db.HrmSalaryStructures.AsNoTracking()
            .Where(s => s.EmployeeNo == employeeNo && s.Status == 1 && s.IsDeleted == 0)
            .Where(s => s.EffectiveFrom != default && s.EffectiveFrom <= asOf)
            .Where(s => s.EffectiveTo == null || s.EffectiveTo >= asOf)
            .OrderByDescending(s => s.EffectiveFrom)
            .FirstOrDefaultAsync(ct);
    }

    private decimal ResolveBonusBasis(HrmBonusRun run, HrmEmployee employee, HrmSalaryStructure? structure)
    {
        if (run.CalculationType == CalcGross)
        {
            if (structure != null && structure.GrossSalary != 0) return structure.GrossSalary;
            return employee.Salary;
        }
        // Default: CALC_BASIC
        if (structure != null && structure.BasicSalary != 0) return structure.BasicSalary;
        return employee.Salary;
    }

    private async Task<HashSet<long>> SelectedDesignationNosAsync(long bonusRunNo, CancellationToken ct)
    {
        return (await _db.HrmBonusScopeDesignations
            .Where(s => s.BonusRunNo == bonusRunNo && s.IsDeleted == 0)
            .Select(s => s.DesignationNo)
            .ToListAsync(ct)).ToHashSet();
    }

    private async Task<HashSet<long>> SelectedEmployeeNosAsync(long bonusRunNo, CancellationToken ct)
    {
        return (await _db.HrmBonusScopeEmployees
            .Where(s => s.BonusRunNo == bonusRunNo && s.IsDeleted == 0)
            .Select(s => s.EmployeeNo)
            .ToListAsync(ct)).ToHashSet();
    }

    private async Task SyncScopesAsync(HrmBonusRun run, long? userNo, CancellationToken ct)
    {
        var actorNo = userNo ?? _ctx.CurrentUserNo();

        // Soft-delete old scopes.
        var oldDesig = await _db.HrmBonusScopeDesignations
            .Where(s => s.BonusRunNo == run.BonusRunNo && s.IsDeleted == 0).ToListAsync(ct);
        foreach (var old in oldDesig) old.PerformSoftDelete(actorNo);

        var oldEmp = await _db.HrmBonusScopeEmployees
            .Where(s => s.BonusRunNo == run.BonusRunNo && s.IsDeleted == 0).ToListAsync(ct);
        foreach (var old in oldEmp) old.PerformSoftDelete(actorNo);

        // Insert new designation scopes.
        foreach (var desigNo in NormalizeIds(run.SelectedDesignationNos))
        {
            _db.HrmBonusScopeDesignations.Add(new HrmBonusScopeDesignation
            {
                BonusRunNo = run.BonusRunNo,
                DesignationNo = desigNo,
                BranchNo = run.BranchNo,
                IsActive = 1,
                IsDeleted = 0,
                CreatedBy = actorNo,
                CreatedAt = DateTime.UtcNow
            });
        }

        // Insert new employee scopes.
        foreach (var empNo in NormalizeIds(run.SelectedEmployeeNos))
        {
            _db.HrmBonusScopeEmployees.Add(new HrmBonusScopeEmployee
            {
                BonusRunNo = run.BonusRunNo,
                EmployeeNo = empNo,
                BranchNo = run.BranchNo,
                IsActive = 1,
                IsDeleted = 0,
                CreatedBy = actorNo,
                CreatedAt = DateTime.UtcNow
            });
        }
    }

    private static List<long> NormalizeIds(List<long>? values)
    {
        if (values == null || values.Count == 0) return new();
        return values.Where(v => v > 0).Distinct().ToList();
    }
}
