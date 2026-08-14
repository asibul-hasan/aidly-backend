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
// Hrm1007Service — Salary Component Setup
// Port of Java Hrm1007Service with full validation: component type (1-4),
// calc type (1-4), branch-scoped uniqueness, flag validation on 6 fields.
// ═══════════════════════════════════════════════════════════════════════════

public interface IHrm1007Service
{
    Task<List<HrmSalaryComponent>> GetListAsync(CancellationToken ct = default);
    Task<List<HrmSalaryComponent>> GetListByBranchAsync(long branchNo, CancellationToken ct = default);
    Task<HrmSalaryComponent> GetDetailAsync(long componentNo, CancellationToken ct = default);
    Task<HrmSalaryComponent> SaveAsync(HrmSalaryComponent dto, CancellationToken ct = default);
    Task DeleteAsync(long componentNo, CancellationToken ct = default);
}

public class Hrm1007Service : IHrm1007Service
{
    private readonly IHrmDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    public Hrm1007Service(IHrmDbContext db, ICompanyBranchContext ctx) { _db = db; _ctx = ctx; }

    public async Task<List<HrmSalaryComponent>> GetListAsync(CancellationToken ct = default)
    {
        long? branchNo = _ctx.BranchNo;
        return await _db.HrmSalaryComponents.AsNoTracking()
            .Where(x => x.IsDeleted == 0 && (branchNo == null || x.BranchNo == branchNo))
            .OrderBy(x => x.DisplayOrder).ThenBy(x => x.ComponentNo).ToListAsync(ct);
    }

    public async Task<List<HrmSalaryComponent>> GetListByBranchAsync(long branchNo, CancellationToken ct = default) =>
        await _db.HrmSalaryComponents.AsNoTracking().Where(x => x.BranchNo == branchNo && x.IsDeleted == 0).OrderBy(x => x.DisplayOrder).ThenBy(x => x.ComponentNo).ToListAsync(ct);

    public async Task<HrmSalaryComponent> GetDetailAsync(long componentNo, CancellationToken ct = default) =>
        await _db.HrmSalaryComponents.AsNoTracking().FirstOrDefaultAsync(x => x.ComponentNo == componentNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Salary component not found: componentNo={componentNo}");

    public async Task<HrmSalaryComponent> SaveAsync(HrmSalaryComponent dto, CancellationToken ct = default)
    {
        if (dto.ComponentNo > 0)
        {
            // Update path
            var entity = await _db.HrmSalaryComponents.FirstOrDefaultAsync(x => x.ComponentNo == dto.ComponentNo && x.IsDeleted == 0, ct)
                ?? throw new NotFoundException($"Salary component not found: componentNo={dto.ComponentNo}");

            // Uniqueness check on update (self-exclusion)
            if (dto.ComponentId != null)
            {
                string newId = dto.ComponentId.Trim().ToUpperInvariant();
                bool dup = await _db.HrmSalaryComponents.AnyAsync(x =>
                    x.ComponentId == newId && x.BranchNo == entity.BranchNo && x.IsDeleted == 0 && x.ComponentNo != entity.ComponentNo, ct);
                if (dup) throw new ValidationException($"Component code already exists in this branch: {newId}");
                entity.ComponentId = newId;
            }

            if (dto.ComponentName != null) entity.ComponentName = HrmValidation.TrimRequired(dto.ComponentName, "Component name");
            entity.ComponentType = ValidateComponentType(dto.ComponentType);
            entity.CalcType = ValidateCalcType(dto.CalcType);
            entity.CalcValue = dto.CalcValue;
            entity.FormulaExpression = TrimToNull(dto.FormulaExpression);
            entity.BaseComponentNo = dto.BaseComponentNo;
            entity.IsProratable = HrmValidation.NormalizeFlag(dto.IsProratable, entity.IsProratable, "is_proratable");
            entity.IsAttendanceDependent = HrmValidation.NormalizeFlag(dto.IsAttendanceDependent, entity.IsAttendanceDependent, "is_attendance_dependent");
            entity.IsRegular = HrmValidation.NormalizeFlag(dto.IsRegular, entity.IsRegular, "is_regular");
            entity.IsTaxable = HrmValidation.NormalizeFlag(dto.IsTaxable ?? 0, entity.IsTaxable ?? 0, "is_taxable");
            entity.AffectsNet = HrmValidation.NormalizeFlag(dto.AffectsNet ?? 0, entity.AffectsNet ?? 0, "affects_net");
            entity.IsStatutory = HrmValidation.NormalizeFlag(dto.IsStatutory ?? 0, entity.IsStatutory ?? 0, "is_statutory");
            entity.CountryCode = TrimToNull(dto.CountryCode);
            entity.GlAccountCode = TrimToNull(dto.GlAccountCode);
            entity.DisplayOrder = dto.DisplayOrder;
            entity.RoundingMode = dto.RoundingMode;
            entity.RoundingScale = dto.RoundingScale;
            entity.Remarks = dto.Remarks;
            entity.IsActive = HrmValidation.NormalizeFlag(dto.IsActive ?? 0, entity.IsActive ?? 0, "is_active");
            entity.UpdatedBy = _ctx.CurrentUserNo(); entity.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return entity;
        }
        else
        {
            // Insert path
            long branchNo = HrmValidation.ResolveBranch(dto.BranchNo, _ctx);
            string componentName = HrmValidation.TrimRequired(dto.ComponentName, "Component name");
            string componentId = string.IsNullOrWhiteSpace(dto.ComponentId) ? await NextComponentIdAsync(branchNo, ct) : dto.ComponentId.Trim().ToUpperInvariant();

            // Branch-scoped uniqueness
            if (await _db.HrmSalaryComponents.AnyAsync(x => x.ComponentId == componentId && x.BranchNo == branchNo && x.IsDeleted == 0, ct))
                throw new ValidationException($"Component code already exists in this branch: {componentId}");

            var entity = new HrmSalaryComponent
            {
                BranchNo = branchNo,
                CompanyNo = _ctx.CompanyNo,
                ComponentId = componentId,
                ComponentName = componentName,
                ComponentType = ValidateComponentType(dto.ComponentType),
                CalcType = ValidateCalcType(dto.CalcType),
                CalcValue = dto.CalcValue,
                FormulaExpression = TrimToNull(dto.FormulaExpression),
                BaseComponentNo = dto.BaseComponentNo,
                IsProratable = HrmValidation.NormalizeFlag(dto.IsProratable, 1, "is_proratable"),
                IsAttendanceDependent = HrmValidation.NormalizeFlag(dto.IsAttendanceDependent, 0, "is_attendance_dependent"),
                IsRegular = HrmValidation.NormalizeFlag(dto.IsRegular, 1, "is_regular"),
                IsTaxable = HrmValidation.NormalizeFlag(dto.IsTaxable ?? 0, 1, "is_taxable"),
                AffectsNet = HrmValidation.NormalizeFlag(dto.AffectsNet ?? 0, 1, "affects_net"),
                IsStatutory = HrmValidation.NormalizeFlag(dto.IsStatutory ?? 0, 0, "is_statutory"),
                CountryCode = TrimToNull(dto.CountryCode),
                GlAccountCode = TrimToNull(dto.GlAccountCode),
                DisplayOrder = dto.DisplayOrder,
                RoundingMode = dto.RoundingMode,
                RoundingScale = dto.RoundingScale,
                Remarks = dto.Remarks,
                IsActive = HrmValidation.NormalizeFlag(dto.IsActive ?? 0, 1, "is_active"),
                IsDeleted = 0,
                CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow
            };
            _db.HrmSalaryComponents.Add(entity);
            await _db.SaveChangesAsync(ct);
            return entity;
        }
    }

    public async Task DeleteAsync(long componentNo, CancellationToken ct = default)
    {
        var entity = await _db.HrmSalaryComponents.FirstOrDefaultAsync(x => x.ComponentNo == componentNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Salary component not found: componentNo={componentNo}");
        entity.PerformSoftDelete(_ctx.CurrentUserNo());
        await _db.SaveChangesAsync(ct);
    }

    private async Task<string> NextComponentIdAsync(long branchNo, CancellationToken ct)
    {
        long next = await _db.HrmSalaryComponents.CountAsync(x => x.BranchNo == branchNo && x.IsDeleted == 0, ct) + 1;
        string id;
        do { id = $"CMP{next++:D4}"; }
        while (await _db.HrmSalaryComponents.AnyAsync(x => x.ComponentId == id && x.BranchNo == branchNo && x.IsDeleted == 0, ct));
        return id;
    }

    private static short ValidateComponentType(short value)
    {
        if (value is < 1 or > 4) throw new ValidationException("Component type must be 1 (Earning), 2 (Deduction), 3 (EmployerContribution), or 4 (StatutoryDeduction)");
        return value;
    }

    private static short ValidateCalcType(short value)
    {
        if (value is < 1 or > 4) throw new ValidationException("Calc type must be 1 (Fixed), 2 (PercentOfBasic), 3 (PercentOfGross), or 4 (Formula)");
        return value;
    }

    private static string? TrimToNull(string? value)
    {
        if (value == null) return null;
        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}
