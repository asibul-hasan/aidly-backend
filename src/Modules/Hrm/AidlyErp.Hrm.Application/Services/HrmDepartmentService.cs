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
// HrmDepartmentService — HRM Department CRUD
// Port of Java HrmDepartmentService with branch-scoped uniqueness on update,
// flag validation, TrimRequired.
// ═══════════════════════════════════════════════════════════════════════════

public interface IHrmDepartmentService
{
    Task<List<HrmDepartment>> GetListAsync(CancellationToken ct = default);
    Task<List<HrmDepartment>> GetListByBranchAsync(long branchNo, CancellationToken ct = default);
    Task<HrmDepartment> GetDetailAsync(long departmentNo, CancellationToken ct = default);
    Task<HrmDepartment> SaveAsync(HrmDepartment dto, CancellationToken ct = default);
    Task DeleteAsync(long departmentNo, CancellationToken ct = default);
}

public class HrmDepartmentService : IHrmDepartmentService
{
    private readonly IHrmDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    public HrmDepartmentService(IHrmDbContext db, ICompanyBranchContext ctx) { _db = db; _ctx = ctx; }

    public async Task<List<HrmDepartment>> GetListAsync(CancellationToken ct = default)
    {
        long? branchNo = _ctx.BranchNo;
        return await _db.HrmDepartments.AsNoTracking().IgnoreQueryFilters()
            .Where(x => x.IsDeleted == 0 && (branchNo == null || x.BranchNo == null || x.BranchNo == branchNo))
            .OrderBy(x => x.DepartmentNo).ToListAsync(ct);
    }

    public async Task<List<HrmDepartment>> GetListByBranchAsync(long branchNo, CancellationToken ct = default) =>
        await _db.HrmDepartments.AsNoTracking().Where(x => x.BranchNo == branchNo && x.IsDeleted == 0).OrderBy(x => x.DepartmentNo).ToListAsync(ct);

    public async Task<HrmDepartment> GetDetailAsync(long departmentNo, CancellationToken ct = default) =>
        await _db.HrmDepartments.AsNoTracking().FirstOrDefaultAsync(x => x.DepartmentNo == departmentNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Department not found: departmentNo={departmentNo}");

    public async Task<HrmDepartment> SaveAsync(HrmDepartment dto, CancellationToken ct = default)
    {
        if (dto.DepartmentNo > 0)
        {
            var entity = await _db.HrmDepartments.FirstOrDefaultAsync(x => x.DepartmentNo == dto.DepartmentNo && x.IsDeleted == 0, ct)
                ?? throw new NotFoundException($"Department not found: departmentNo={dto.DepartmentNo}");

            // Uniqueness check on update (self-exclusion)
            if (dto.DepartmentId != null)
            {
                string newId = dto.DepartmentId.Trim().ToUpperInvariant();
                bool dup = await _db.HrmDepartments.AnyAsync(x =>
                    x.DepartmentId == newId && x.BranchNo == entity.BranchNo && x.IsDeleted == 0 && x.DepartmentNo != entity.DepartmentNo, ct);
                if (dup) throw new ValidationException($"Department ID already exists in this branch: {newId}");
                entity.DepartmentId = newId;
            }

            if (dto.DepartmentName != null) entity.DepartmentName = HrmValidation.TrimRequired(dto.DepartmentName, "Department name");
            entity.ParentDepartmentNo = dto.ParentDepartmentNo;
            entity.CostCenterNo = dto.CostCenterNo;
            entity.Remarks = dto.Remarks;
            entity.IsActive = HrmValidation.NormalizeFlag(dto.IsActive, entity.IsActive, "is_active");
            entity.UpdatedBy = _ctx.CurrentUserNo(); entity.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return entity;
        }
        else
        {
            long branchNo = HrmValidation.ResolveBranch(dto.BranchNo, _ctx);
            string deptName = HrmValidation.TrimRequired(dto.DepartmentName, "Department name");
            string deptId = string.IsNullOrWhiteSpace(dto.DepartmentId) ? await NextDeptIdAsync(branchNo, ct) : dto.DepartmentId.Trim().ToUpperInvariant();

            // Branch-scoped uniqueness
            if (await _db.HrmDepartments.AnyAsync(x => x.DepartmentId == deptId && x.BranchNo == branchNo && x.IsDeleted == 0, ct))
                throw new ValidationException($"Department ID already exists in this branch: {deptId}");

            var entity = new HrmDepartment
            {
                BranchNo = branchNo,
                CompanyNo = _ctx.CompanyNo,
                DepartmentId = deptId,
                DepartmentName = deptName,
                ParentDepartmentNo = dto.ParentDepartmentNo,
                CostCenterNo = dto.CostCenterNo,
                Remarks = dto.Remarks,
                IsActive = HrmValidation.NormalizeFlag(dto.IsActive, 1, "is_active"),
                IsDeleted = 0,
                CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow
            };
            _db.HrmDepartments.Add(entity);
            await _db.SaveChangesAsync(ct);
            return entity;
        }
    }

    public async Task DeleteAsync(long departmentNo, CancellationToken ct = default)
    {
        var entity = await _db.HrmDepartments.FirstOrDefaultAsync(x => x.DepartmentNo == departmentNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Department not found: departmentNo={departmentNo}");
        entity.PerformSoftDelete(_ctx.CurrentUserNo());
        await _db.SaveChangesAsync(ct);
    }

    private async Task<string> NextDeptIdAsync(long branchNo, CancellationToken ct)
    {
        long next = await _db.HrmDepartments.CountAsync(x => x.BranchNo == branchNo && x.IsDeleted == 0, ct) + 1;
        string id;
        do { id = $"DEP{next++:D4}"; }
        while (await _db.HrmDepartments.AnyAsync(x => x.DepartmentId == id && x.BranchNo == branchNo && x.IsDeleted == 0, ct));
        return id;
    }
}
