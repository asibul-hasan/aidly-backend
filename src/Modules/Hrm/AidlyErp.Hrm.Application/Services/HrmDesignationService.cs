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
// HrmDesignationService — HRM Designation CRUD
// Port of Java HrmDesignationService with branch-scoped uniqueness on update,
// department FK validation, flag validation.
// ═══════════════════════════════════════════════════════════════════════════

public interface IHrmDesignationService
{
    Task<List<HrmDesignation>> GetListAsync(CancellationToken ct = default);
    Task<List<HrmDesignation>> GetListByBranchAsync(long branchNo, CancellationToken ct = default);
    Task<List<HrmDesignation>> GetListByDepartmentAsync(long departmentNo, CancellationToken ct = default);
    Task<HrmDesignation> GetDetailAsync(long designationNo, CancellationToken ct = default);
    Task<HrmDesignation> SaveAsync(HrmDesignation dto, CancellationToken ct = default);
    Task DeleteAsync(long designationNo, CancellationToken ct = default);
}

public class HrmDesignationService : IHrmDesignationService
{
    private readonly IHrmDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    public HrmDesignationService(IHrmDbContext db, ICompanyBranchContext ctx) { _db = db; _ctx = ctx; }

    public async Task<List<HrmDesignation>> GetListAsync(CancellationToken ct = default)
    {
        long? branchNo = _ctx.BranchNo;
        return await _db.HrmDesignations.AsNoTracking().IgnoreQueryFilters()
            .Where(x => x.IsDeleted == 0 && (branchNo == null || x.BranchNo == null || x.BranchNo == branchNo))
            .OrderBy(x => x.DesignationNo).ToListAsync(ct);
    }

    public async Task<List<HrmDesignation>> GetListByBranchAsync(long branchNo, CancellationToken ct = default) =>
        await _db.HrmDesignations.AsNoTracking().Where(x => x.BranchNo == branchNo && x.IsDeleted == 0).ToListAsync(ct);

    public async Task<List<HrmDesignation>> GetListByDepartmentAsync(long departmentNo, CancellationToken ct = default) =>
        await _db.HrmDesignations.AsNoTracking().Where(x => x.DepartmentNo == departmentNo && x.IsDeleted == 0).ToListAsync(ct);

    public async Task<HrmDesignation> GetDetailAsync(long designationNo, CancellationToken ct = default) =>
        await _db.HrmDesignations.AsNoTracking().FirstOrDefaultAsync(x => x.DesignationNo == designationNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Designation not found: designationNo={designationNo}");

    public async Task<HrmDesignation> SaveAsync(HrmDesignation dto, CancellationToken ct = default)
    {
        if (dto.DesignationNo > 0)
        {
            var entity = await _db.HrmDesignations.FirstOrDefaultAsync(x => x.DesignationNo == dto.DesignationNo && x.IsDeleted == 0, ct)
                ?? throw new NotFoundException($"Designation not found: designationNo={dto.DesignationNo}");

            // Uniqueness check on update (self-exclusion)
            if (dto.DesignationId != null)
            {
                string newId = dto.DesignationId.Trim().ToUpperInvariant();
                bool dup = await _db.HrmDesignations.AnyAsync(x =>
                    x.DesignationId == newId && x.BranchNo == entity.BranchNo && x.IsDeleted == 0 && x.DesignationNo != entity.DesignationNo, ct);
                if (dup) throw new ValidationException($"Designation ID already exists in this branch: {newId}");
                entity.DesignationId = newId;
            }

            if (dto.DesignationName != null) entity.DesignationName = HrmValidation.TrimRequired(dto.DesignationName, "Designation name");
            if (dto.DepartmentNo > 0)
            {
                await ValidateDepartmentAsync(dto.DepartmentNo, ct);
                entity.DepartmentNo = dto.DepartmentNo;
            }
            entity.GradeNo = dto.GradeNo;
            entity.GradeLevel = dto.GradeLevel;
            entity.MinSalary = dto.MinSalary;
            entity.MaxSalary = dto.MaxSalary;
            entity.IsOvertimeEligible = HrmValidation.NormalizeFlag(dto.IsOvertimeEligible, entity.IsOvertimeEligible, "is_overtime_eligible");
            entity.IsActive = HrmValidation.NormalizeFlag(dto.IsActive, entity.IsActive, "is_active");
            entity.UpdatedBy = _ctx.CurrentUserNo(); entity.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return entity;
        }
        else
        {
            long branchNo = HrmValidation.ResolveBranch(dto.BranchNo, _ctx);
            string desigName = HrmValidation.TrimRequired(dto.DesignationName, "Designation name");
            string desigId = string.IsNullOrWhiteSpace(dto.DesignationId) ? await NextDesignationIdAsync(branchNo, ct) : dto.DesignationId.Trim().ToUpperInvariant();

            // Branch-scoped uniqueness
            if (await _db.HrmDesignations.AnyAsync(x => x.DesignationId == desigId && x.BranchNo == branchNo && x.IsDeleted == 0, ct))
                throw new ValidationException($"Designation ID already exists in this branch: {desigId}");

            // Department FK validation
            if (dto.DepartmentNo > 0) await ValidateDepartmentAsync(dto.DepartmentNo, ct);

            var entity = new HrmDesignation
            {
                BranchNo = branchNo,
                CompanyNo = _ctx.CompanyNo,
                DesignationId = desigId,
                DesignationName = desigName,
                DepartmentNo = dto.DepartmentNo,
                GradeNo = dto.GradeNo,
                GradeLevel = dto.GradeLevel,
                MinSalary = dto.MinSalary,
                MaxSalary = dto.MaxSalary,
                IsOvertimeEligible = HrmValidation.NormalizeFlag(dto.IsOvertimeEligible, 0, "is_overtime_eligible"),
                IsActive = HrmValidation.NormalizeFlag(dto.IsActive, 1, "is_active"),
                IsDeleted = 0,
                CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow
            };
            _db.HrmDesignations.Add(entity);
            await _db.SaveChangesAsync(ct);
            return entity;
        }
    }

    public async Task DeleteAsync(long designationNo, CancellationToken ct = default)
    {
        var entity = await _db.HrmDesignations.FirstOrDefaultAsync(x => x.DesignationNo == designationNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Designation not found: designationNo={designationNo}");
        entity.PerformSoftDelete(_ctx.CurrentUserNo());
        await _db.SaveChangesAsync(ct);
    }

    private async Task ValidateDepartmentAsync(long departmentNo, CancellationToken ct)
    {
        var exists = await _db.HrmDepartments.AnyAsync(d => d.DepartmentNo == departmentNo && d.IsDeleted == 0, ct);
        if (!exists) throw new NotFoundException($"Department not found: departmentNo={departmentNo}");
    }

    private async Task<string> NextDesignationIdAsync(long branchNo, CancellationToken ct)
    {
        long next = await _db.HrmDesignations.CountAsync(x => x.BranchNo == branchNo && x.IsDeleted == 0, ct) + 1;
        string id;
        do { id = $"DES{next++:D4}"; }
        while (await _db.HrmDesignations.AnyAsync(x => x.DesignationId == id && x.BranchNo == branchNo && x.IsDeleted == 0, ct));
        return id;
    }
}
