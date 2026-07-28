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
// HrmEmployeeService — Employee Master CRUD
// Port of Java HrmEmployeeService with full validation: department/designation/grade
// FK checks, branch-scoped employee ID uniqueness, soft-delete.
// ═══════════════════════════════════════════════════════════════════════════

public interface IHrmEmployeeService
{
    Task<List<HrmEmployee>> GetListAsync(CancellationToken ct = default);
    Task<List<HrmEmployee>> GetListByBranchAsync(long branchNo, CancellationToken ct = default);
    Task<List<HrmEmployee>> GetListByDepartmentAsync(long departmentNo, CancellationToken ct = default);
    Task<HrmEmployee> GetDetailAsync(long employeeNo, CancellationToken ct = default);
    Task<HrmEmployee> SaveAsync(HrmEmployee dto, CancellationToken ct = default);
    Task<List<HrmEmployee>> BulkInsertAsync(List<HrmEmployee> dtos, CancellationToken ct = default);
    Task DeleteAsync(long employeeNo, CancellationToken ct = default);
}

public class HrmEmployeeService : IHrmEmployeeService
{
    private readonly IHrmDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    public HrmEmployeeService(IHrmDbContext db, ICompanyBranchContext ctx) { _db = db; _ctx = ctx; }

    public async Task<List<HrmEmployee>> GetListAsync(CancellationToken ct = default)
    {
        long? branchNo = _ctx.BranchNo;
        return await _db.HrmEmployees.AsNoTracking()
            .Where(x => x.IsDeleted == 0 && (branchNo == null || x.BranchNo == branchNo))
            .OrderBy(x => x.EmployeeNo).ToListAsync(ct);
    }

    public async Task<List<HrmEmployee>> GetListByBranchAsync(long branchNo, CancellationToken ct = default) =>
        await _db.HrmEmployees.AsNoTracking().Where(x => x.BranchNo == branchNo && x.IsDeleted == 0).OrderBy(x => x.EmployeeNo).ToListAsync(ct);

    public async Task<List<HrmEmployee>> GetListByDepartmentAsync(long departmentNo, CancellationToken ct = default) =>
        await _db.HrmEmployees.AsNoTracking().Where(x => x.DepartmentNo == departmentNo && x.IsDeleted == 0).OrderBy(x => x.EmployeeNo).ToListAsync(ct);

    public async Task<HrmEmployee> GetDetailAsync(long employeeNo, CancellationToken ct = default) =>
        await _db.HrmEmployees.AsNoTracking().FirstOrDefaultAsync(x => x.EmployeeNo == employeeNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Employee not found: employeeNo={employeeNo}");

    public async Task<HrmEmployee> SaveAsync(HrmEmployee dto, CancellationToken ct = default)
    {
        if (dto.EmployeeNo > 0)
        {
            // Update path
            var entity = await _db.HrmEmployees.FirstOrDefaultAsync(x => x.EmployeeNo == dto.EmployeeNo && x.IsDeleted == 0, ct)
                ?? throw new NotFoundException($"Employee not found: employeeNo={dto.EmployeeNo}");

            // Uniqueness check on update (self-exclusion)
            if (dto.EmployeeId != null)
            {
                string newId = dto.EmployeeId.Trim().ToUpperInvariant();
                bool dup = await _db.HrmEmployees.AnyAsync(x =>
                    x.EmployeeId == newId && x.BranchNo == entity.BranchNo && x.IsDeleted == 0 && x.EmployeeNo != entity.EmployeeNo, ct);
                if (dup) throw new ValidationException($"Employee ID already exists in this branch: {newId}");
                entity.EmployeeId = newId;
            }

            // FK validation
            if (dto.DepartmentNo > 0)
            {
                await ValidateDepartmentAsync(dto.DepartmentNo, ct);
                entity.DepartmentNo = dto.DepartmentNo;
            }
            if (dto.DesignationNo > 0)
            {
                await ValidateDesignationAsync(dto.DesignationNo, ct);
                entity.DesignationNo = dto.DesignationNo;
            }
            if (dto.GradeNo.HasValue && dto.GradeNo.Value > 0)
            {
                await ValidateGradeAsync(dto.GradeNo.Value, ct);
                entity.GradeNo = dto.GradeNo;
            }

            if (dto.FirstName != null) entity.FirstName = HrmValidation.TrimRequired(dto.FirstName, "First name");
            if (dto.MiddleName != null) entity.MiddleName = dto.MiddleName;
            if (dto.LastName != null) entity.LastName = dto.LastName;
            if (dto.MobileNumber != null) entity.MobileNumber = dto.MobileNumber;
            if (dto.OfficialEmail != null) entity.OfficialEmail = dto.OfficialEmail;
            if (dto.PersonalEmail != null) entity.PersonalEmail = dto.PersonalEmail;
            if (dto.JoiningDate != default) entity.JoiningDate = dto.JoiningDate;
            if (dto.DateOfBirth != default) entity.DateOfBirth = dto.DateOfBirth;
            if (dto.Gender != null) entity.Gender = dto.Gender;
            if (dto.MaritalStatus != null) entity.MaritalStatus = dto.MaritalStatus;
            if (dto.EmploymentType != null) entity.EmploymentType = dto.EmploymentType;
            if (dto.ContractType != null) entity.ContractType = dto.ContractType;
            entity.TaxpayerClass = NormalizeTaxpayerClass(dto.TaxpayerClass ?? entity.TaxpayerClass);
            entity.Salary = dto.Salary;
            entity.IsActive = HrmValidation.NormalizeFlag(dto.IsActive, entity.IsActive, "is_active");
            entity.UpdatedBy = _ctx.CurrentUserNo(); entity.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return entity;
        }
        else
        {
            // Insert path
            long branchNo = HrmValidation.ResolveBranch(dto.BranchNo, _ctx);
            string firstName = HrmValidation.TrimRequired(dto.FirstName, "First name");
            string empId = string.IsNullOrWhiteSpace(dto.EmployeeId) ? await NextEmployeeIdAsync(branchNo, ct) : dto.EmployeeId.Trim().ToUpperInvariant();

            // Branch-scoped uniqueness
            if (await _db.HrmEmployees.AnyAsync(x => x.EmployeeId == empId && x.BranchNo == branchNo && x.IsDeleted == 0, ct))
                throw new ValidationException($"Employee ID already exists in this branch: {empId}");

            // FK validation
            if (dto.DepartmentNo > 0) await ValidateDepartmentAsync(dto.DepartmentNo, ct);
            if (dto.DesignationNo > 0) await ValidateDesignationAsync(dto.DesignationNo, ct);
            await ValidateGradeAndContractAsync(dto.GradeNo, dto.EmploymentType, dto.ContractType, ct);

            dto.BranchNo = branchNo;
            dto.CompanyNo = _ctx.CompanyNo;
            dto.EmployeeId = empId;
            dto.FirstName = firstName;
            dto.TaxpayerClass = NormalizeTaxpayerClass(dto.TaxpayerClass);
            dto.IsDeleted = 0;
            dto.IsActive = HrmValidation.NormalizeFlag(dto.IsActive, 1, "is_active");
            dto.CreatedBy = _ctx.CurrentUserNo(); dto.CreatedAt = DateTime.UtcNow;
            _db.HrmEmployees.Add(dto);
            await _db.SaveChangesAsync(ct);
            return dto;
        }
    }

    public async Task<List<HrmEmployee>> BulkInsertAsync(List<HrmEmployee> dtos, CancellationToken ct = default)
    {
        long branchNo = _ctx.CurrentBranchNo() ?? throw new ValidationException("Branch is required");
        var existingIds = new HashSet<string>();
        var results = new List<HrmEmployee>();

        foreach (var dto in dtos)
        {
            // Dedup within batch
            string empId = string.IsNullOrWhiteSpace(dto.EmployeeId) ? $"EMP{results.Count + 1:D5}" : dto.EmployeeId.Trim().ToUpperInvariant();
            if (!existingIds.Add(empId))
                throw new ValidationException($"Duplicate employee ID in batch: {empId}");

            dto.BranchNo = branchNo;
            dto.CompanyNo = _ctx.CompanyNo;
            dto.EmployeeId = empId;
            dto.IsDeleted = 0;
            dto.IsActive = HrmValidation.NormalizeFlag(dto.IsActive, 1, "is_active");
            dto.CreatedBy = _ctx.CurrentUserNo(); dto.CreatedAt = DateTime.UtcNow;
            _db.HrmEmployees.Add(dto);
            results.Add(dto);
        }
        await _db.SaveChangesAsync(ct);
        return results;
    }

    public async Task DeleteAsync(long employeeNo, CancellationToken ct = default)
    {
        var entity = await _db.HrmEmployees.FirstOrDefaultAsync(x => x.EmployeeNo == employeeNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Employee not found: employeeNo={employeeNo}");
        entity.PerformSoftDelete(_ctx.CurrentUserNo());
        await _db.SaveChangesAsync(ct);
    }

    // ── FK Validation ──────────────────────────────────────────────────────

    private static readonly HashSet<string> SupportedTaxpayerClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "GENERAL", "WOMEN_SENIOR", "DISABILITY", "FREEDOM_FIGHTER", "OTHER"
    };

    private static string NormalizeTaxpayerClass(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "GENERAL";
        var normalized = value.Trim().ToUpperInvariant();
        if (!SupportedTaxpayerClasses.Contains(normalized))
            throw new ValidationException($"Unsupported taxpayer class: {value}. Supported: {string.Join(", ", SupportedTaxpayerClasses)}");
        return normalized;
    }

    private async Task ValidateGradeAndContractAsync(long? gradeNo, short? employmentType, short? contractType, CancellationToken ct)
    {
        // Non-contract types (1=Permanent, 3=Probation) require grade
        if (employmentType is 1 or 3)
        {
            if (!gradeNo.HasValue || gradeNo.Value <= 0)
                throw new ValidationException("Grade is required for permanent/probation employees");
        }
        // Contract/intern types (2=Contract, 4=Intern) require contract_type
        if (employmentType is 2 or 4)
        {
            if (!contractType.HasValue)
                throw new ValidationException("Contract type is required for contract/intern employees");
        }
        // If grade provided, validate it exists
        if (gradeNo.HasValue && gradeNo.Value > 0)
            await ValidateGradeAsync(gradeNo.Value, ct);
    }

    private async Task ValidateDepartmentAsync(long departmentNo, CancellationToken ct)
    {
        var exists = await _db.HrmDepartments.AnyAsync(d => d.DepartmentNo == departmentNo && d.IsDeleted == 0, ct);
        if (!exists) throw new NotFoundException($"Department not found: departmentNo={departmentNo}");
    }

    private async Task ValidateDesignationAsync(long designationNo, CancellationToken ct)
    {
        var exists = await _db.HrmDesignations.AnyAsync(d => d.DesignationNo == designationNo && d.IsDeleted == 0, ct);
        if (!exists) throw new NotFoundException($"Designation not found: designationNo={designationNo}");
    }

    private async Task ValidateGradeAsync(long gradeNo, CancellationToken ct)
    {
        var exists = await _db.HrmGrades.AnyAsync(g => g.GradeNo == gradeNo && g.IsDeleted == 0, ct);
        if (!exists) throw new NotFoundException($"Grade not found: gradeNo={gradeNo}");
    }

    private async Task<string> NextEmployeeIdAsync(long branchNo, CancellationToken ct)
    {
        long next = await _db.HrmEmployees.CountAsync(x => x.BranchNo == branchNo && x.IsDeleted == 0, ct) + 1;
        string id;
        do { id = $"EMP{next++:D5}"; }
        while (await _db.HrmEmployees.AnyAsync(x => x.EmployeeId == id && x.BranchNo == branchNo && x.IsDeleted == 0, ct));
        return id;
    }
}
