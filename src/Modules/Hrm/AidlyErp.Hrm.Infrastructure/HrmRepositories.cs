using Microsoft.EntityFrameworkCore;
using AidlyErp.Shared.Core.Abstractions;
using AidlyErp.Shared.Contracts;
using AidlyErp.Sys.Contracts;
using AidlyErp.Hrm.Application.Interfaces;
using AidlyErp.Hrm.Domain;

namespace AidlyErp.Shared.Infrastructure.Persistence.Repositories;

// ═══════════════════════════════════════════════════════════════════════════
// HRM Repository Implementations — EF Core translations of Spring Data repos
// ═══════════════════════════════════════════════════════════════════════════

// ── Setup (1000-series) ──────────────────────────────────────────────────

public class HrmDepartmentRepository : IHrmDepartmentRepository
{
    private readonly IHrmDbContext _db;
    public HrmDepartmentRepository(IHrmDbContext db) => _db = db;

    public async Task<List<HrmDepartment>> FindAllAsync(CancellationToken ct = default) =>
        await _db.HrmDepartments.AsNoTracking().Where(x => x.IsDeleted == 0).OrderBy(x => x.DepartmentNo).ToListAsync(ct);

    public async Task<List<HrmDepartment>> FindAllPaginatedAsync(int skip, int take, CancellationToken ct = default) =>
        await _db.HrmDepartments.AsNoTracking().Where(x => x.IsDeleted == 0).OrderBy(x => x.DepartmentNo).Skip(skip).Take(take).ToListAsync(ct);

    public async Task<HrmDepartment?> FindByIdAsync(long departmentNo, CancellationToken ct = default) =>
        await _db.HrmDepartments.AsNoTracking().FirstOrDefaultAsync(x => x.DepartmentNo == departmentNo && x.IsDeleted == 0, ct);

    public async Task<List<HrmDepartment>> FindByBranchAsync(long branchNo, CancellationToken ct = default) =>
        await _db.HrmDepartments.AsNoTracking().Where(x => x.BranchNo == branchNo && x.IsDeleted == 0).OrderBy(x => x.DepartmentNo).ToListAsync(ct);

    public async Task<List<HrmDepartment>> FindByBranchPaginatedAsync(long branchNo, int skip, int take, CancellationToken ct = default) =>
        await _db.HrmDepartments.AsNoTracking().Where(x => x.BranchNo == branchNo && x.IsDeleted == 0).OrderBy(x => x.DepartmentNo).Skip(skip).Take(take).ToListAsync(ct);

    public async Task<HrmDepartment?> FindByCodeAsync(string departmentId, CancellationToken ct = default) =>
        await _db.HrmDepartments.AsNoTracking().FirstOrDefaultAsync(x => x.DepartmentId == departmentId && x.IsDeleted == 0, ct);

    public async Task<bool> ExistsByCodeAsync(string departmentId, CancellationToken ct = default) =>
        await _db.HrmDepartments.AnyAsync(x => x.DepartmentId == departmentId && x.IsDeleted == 0, ct);

    public void Add(HrmDepartment entity) => _db.HrmDepartments.Add(entity);
    public void Update(HrmDepartment entity) => _db.HrmDepartments.Update(entity);
}

public class HrmDesignationRepository : IHrmDesignationRepository
{
    private readonly IHrmDbContext _db;
    public HrmDesignationRepository(IHrmDbContext db) => _db = db;

    public async Task<List<HrmDesignation>> FindAllAsync(CancellationToken ct = default) =>
        await _db.HrmDesignations.AsNoTracking().Where(x => x.IsDeleted == 0).OrderBy(x => x.DesignationNo).ToListAsync(ct);

    public async Task<List<HrmDesignation>> FindAllPaginatedAsync(int skip, int take, CancellationToken ct = default) =>
        await _db.HrmDesignations.AsNoTracking().Where(x => x.IsDeleted == 0).OrderBy(x => x.DesignationNo).Skip(skip).Take(take).ToListAsync(ct);

    public async Task<HrmDesignation?> FindByIdAsync(long designationNo, CancellationToken ct = default) =>
        await _db.HrmDesignations.AsNoTracking().FirstOrDefaultAsync(x => x.DesignationNo == designationNo && x.IsDeleted == 0, ct);

    public async Task<List<HrmDesignation>> FindByBranchAsync(long branchNo, CancellationToken ct = default) =>
        await _db.HrmDesignations.AsNoTracking().Where(x => x.BranchNo == branchNo && x.IsDeleted == 0).ToListAsync(ct);

    public async Task<List<HrmDesignation>> FindByBranchPaginatedAsync(long branchNo, int skip, int take, CancellationToken ct = default) =>
        await _db.HrmDesignations.AsNoTracking().Where(x => x.BranchNo == branchNo && x.IsDeleted == 0).Skip(skip).Take(take).ToListAsync(ct);

    public async Task<List<HrmDesignation>> FindByDepartmentAsync(long departmentNo, CancellationToken ct = default) =>
        await _db.HrmDesignations.AsNoTracking().Where(x => x.DepartmentNo == departmentNo && x.IsDeleted == 0).ToListAsync(ct);

    public async Task<HrmDesignation?> FindByCodeAsync(string designationId, CancellationToken ct = default) =>
        await _db.HrmDesignations.AsNoTracking().FirstOrDefaultAsync(x => x.DesignationId == designationId && x.IsDeleted == 0, ct);

    public async Task<bool> ExistsByCodeAsync(string designationId, CancellationToken ct = default) =>
        await _db.HrmDesignations.AnyAsync(x => x.DesignationId == designationId && x.IsDeleted == 0, ct);

    public void Add(HrmDesignation entity) => _db.HrmDesignations.Add(entity);
    public void Update(HrmDesignation entity) => _db.HrmDesignations.Update(entity);
}

public class HrmEmployeeRepository : IHrmEmployeeRepository
{
    private readonly IHrmDbContext _db;
    public HrmEmployeeRepository(IHrmDbContext db) => _db = db;

    public async Task<List<HrmEmployee>> FindAllAsync(CancellationToken ct = default) =>
        await _db.HrmEmployees.AsNoTracking().Where(x => x.IsDeleted == 0).OrderBy(x => x.EmployeeNo).ToListAsync(ct);

    public async Task<List<HrmEmployee>> FindAllPaginatedAsync(int skip, int take, CancellationToken ct = default) =>
        await _db.HrmEmployees.AsNoTracking().Where(x => x.IsDeleted == 0).OrderBy(x => x.EmployeeNo).Skip(skip).Take(take).ToListAsync(ct);

    public async Task<HrmEmployee?> FindByIdAsync(long employeeNo, CancellationToken ct = default) =>
        await _db.HrmEmployees.AsNoTracking().FirstOrDefaultAsync(x => x.EmployeeNo == employeeNo && x.IsDeleted == 0, ct);

    public async Task<List<HrmEmployee>> FindByBranchAsync(long branchNo, CancellationToken ct = default) =>
        await _db.HrmEmployees.AsNoTracking().Where(x => x.BranchNo == branchNo && x.IsDeleted == 0).ToListAsync(ct);

    public async Task<List<HrmEmployee>> FindByBranchPaginatedAsync(long branchNo, int skip, int take, CancellationToken ct = default) =>
        await _db.HrmEmployees.AsNoTracking().Where(x => x.BranchNo == branchNo && x.IsDeleted == 0).Skip(skip).Take(take).ToListAsync(ct);

    public async Task<List<HrmEmployee>> FindByDepartmentAsync(long departmentNo, CancellationToken ct = default) =>
        await _db.HrmEmployees.AsNoTracking().Where(x => x.DepartmentNo == departmentNo && x.IsDeleted == 0).ToListAsync(ct);

    public async Task<List<HrmEmployee>> FindByDepartmentPaginatedAsync(long departmentNo, int skip, int take, CancellationToken ct = default) =>
        await _db.HrmEmployees.AsNoTracking().Where(x => x.DepartmentNo == departmentNo && x.IsDeleted == 0).Skip(skip).Take(take).ToListAsync(ct);

    public async Task<HrmEmployee?> FindByCodeAsync(string employeeId, long branchNo, CancellationToken ct = default) =>
        await _db.HrmEmployees.AsNoTracking().FirstOrDefaultAsync(x => x.EmployeeId == employeeId && x.BranchNo == branchNo && x.IsDeleted == 0, ct);

    public async Task<bool> ExistsByCodeAsync(string employeeId, long branchNo, CancellationToken ct = default) =>
        await _db.HrmEmployees.AnyAsync(x => x.EmployeeId == employeeId && x.BranchNo == branchNo && x.IsDeleted == 0, ct);

    public async Task<List<HrmEmployee>> FindWithFiltersAsync(short? isActive, short? employmentType, long? departmentNo, CancellationToken ct = default)
    {
        var query = _db.HrmEmployees.AsNoTracking()
            .Where(x => x.IsDeleted == 0);
        if (isActive.HasValue) query = query.Where(x => x.IsActive == isActive.Value);
        if (employmentType.HasValue) query = query.Where(x => x.EmploymentType == employmentType.Value);
        if (departmentNo.HasValue) query = query.Where(x => x.DepartmentNo == departmentNo.Value);
        return await query.OrderBy(x => x.EmployeeNo).ToListAsync(ct);
    }

    public void Add(HrmEmployee entity) => _db.HrmEmployees.Add(entity);
    public void Update(HrmEmployee entity) => _db.HrmEmployees.Update(entity);
}

public class HrmShiftRepository : IHrmShiftRepository
{
    private readonly IHrmDbContext _db;
    public HrmShiftRepository(IHrmDbContext db) => _db = db;

    public async Task<List<HrmShift>> FindAllAsync(CancellationToken ct = default) =>
        await _db.HrmShifts.AsNoTracking().Where(x => x.IsDeleted == 0).OrderBy(x => x.ShiftNo).ToListAsync(ct);

    public async Task<List<HrmShift>> FindAllPaginatedAsync(int skip, int take, CancellationToken ct = default) =>
        await _db.HrmShifts.AsNoTracking().Where(x => x.IsDeleted == 0).OrderBy(x => x.ShiftNo).Skip(skip).Take(take).ToListAsync(ct);

    public async Task<HrmShift?> FindByIdAsync(long shiftNo, CancellationToken ct = default) =>
        await _db.HrmShifts.AsNoTracking().FirstOrDefaultAsync(x => x.ShiftNo == shiftNo && x.IsDeleted == 0, ct);

    public async Task<List<HrmShift>> FindByBranchAsync(long branchNo, CancellationToken ct = default) =>
        await _db.HrmShifts.AsNoTracking().Where(x => x.BranchNo == branchNo && x.IsDeleted == 0).OrderBy(x => x.ShiftNo).ToListAsync(ct);

    public async Task<HrmShift?> FindByCodeAsync(string shiftId, long branchNo, CancellationToken ct = default) =>
        await _db.HrmShifts.AsNoTracking().FirstOrDefaultAsync(x => x.ShiftId == shiftId && x.BranchNo == branchNo && x.IsDeleted == 0, ct);

    public async Task<bool> ExistsByCodeAsync(string shiftId, long branchNo, CancellationToken ct = default) =>
        await _db.HrmShifts.AnyAsync(x => x.ShiftId == shiftId && x.BranchNo == branchNo && x.IsDeleted == 0, ct);

    public void Add(HrmShift entity) => _db.HrmShifts.Add(entity);
    public void Update(HrmShift entity) => _db.HrmShifts.Update(entity);
}

public class HrmGradeRepository : IHrmGradeRepository
{
    private readonly IHrmDbContext _db;
    public HrmGradeRepository(IHrmDbContext db) => _db = db;

    public async Task<List<HrmGrade>> FindAllAsync(CancellationToken ct = default) =>
        await _db.HrmGrades.AsNoTracking().Where(x => x.IsDeleted == 0).OrderBy(x => x.GradeNo).ToListAsync(ct);

    public async Task<List<HrmGrade>> FindAllPaginatedAsync(int skip, int take, CancellationToken ct = default) =>
        await _db.HrmGrades.AsNoTracking().Where(x => x.IsDeleted == 0).OrderBy(x => x.GradeNo).Skip(skip).Take(take).ToListAsync(ct);

    public async Task<HrmGrade?> FindByIdAsync(long gradeNo, CancellationToken ct = default) =>
        await _db.HrmGrades.AsNoTracking().FirstOrDefaultAsync(x => x.GradeNo == gradeNo && x.IsDeleted == 0, ct);

    public async Task<List<HrmGrade>> FindByBranchAsync(long branchNo, CancellationToken ct = default) =>
        await _db.HrmGrades.AsNoTracking().Where(x => x.BranchNo == branchNo && x.IsDeleted == 0).OrderBy(x => x.GradeNo).ToListAsync(ct);

    public async Task<HrmGrade?> FindByCodeAsync(string gradeId, long branchNo, CancellationToken ct = default) =>
        await _db.HrmGrades.AsNoTracking().FirstOrDefaultAsync(x => x.GradeId == gradeId && x.BranchNo == branchNo && x.IsDeleted == 0, ct);

    public async Task<bool> ExistsByCodeAsync(string gradeId, long branchNo, CancellationToken ct = default) =>
        await _db.HrmGrades.AnyAsync(x => x.GradeId == gradeId && x.BranchNo == branchNo && x.IsDeleted == 0, ct);

    public void Add(HrmGrade entity) => _db.HrmGrades.Add(entity);
    public void Update(HrmGrade entity) => _db.HrmGrades.Update(entity);
}

public class HrmGradeStepRepository : IHrmGradeStepRepository
{
    private readonly IHrmDbContext _db;
    public HrmGradeStepRepository(IHrmDbContext db) => _db = db;

    public async Task<List<HrmGradeStep>> FindByGradeAsync(long gradeNo, CancellationToken ct = default) =>
        await _db.HrmGradeSteps.AsNoTracking().Where(x => x.GradeNo == gradeNo && x.IsDeleted == 0).OrderBy(x => x.GradeStepNo).ToListAsync(ct);

    public void Add(HrmGradeStep entity) => _db.HrmGradeSteps.Add(entity);
    public void AddRange(IEnumerable<HrmGradeStep> entities) => _db.HrmGradeSteps.AddRange(entities);
    public void RemoveRange(IEnumerable<HrmGradeStep> entities) => _db.HrmGradeSteps.RemoveRange(entities);
}

public class HrmLeaveTypeRepository : IHrmLeaveTypeRepository
{
    private readonly IHrmDbContext _db;
    public HrmLeaveTypeRepository(IHrmDbContext db) => _db = db;

    public async Task<List<HrmLeaveType>> FindAllAsync(CancellationToken ct = default) =>
        await _db.HrmLeaveTypes.AsNoTracking().Where(x => x.IsDeleted == 0).OrderBy(x => x.LeaveTypeNo).ToListAsync(ct);

    public async Task<List<HrmLeaveType>> FindAllPaginatedAsync(int skip, int take, CancellationToken ct = default) =>
        await _db.HrmLeaveTypes.AsNoTracking().Where(x => x.IsDeleted == 0).OrderBy(x => x.LeaveTypeNo).Skip(skip).Take(take).ToListAsync(ct);

    public async Task<HrmLeaveType?> FindByIdAsync(long leaveTypeNo, CancellationToken ct = default) =>
        await _db.HrmLeaveTypes.AsNoTracking().FirstOrDefaultAsync(x => x.LeaveTypeNo == leaveTypeNo && x.IsDeleted == 0, ct);

    public async Task<List<HrmLeaveType>> FindByBranchAsync(long branchNo, CancellationToken ct = default) =>
        await _db.HrmLeaveTypes.AsNoTracking().Where(x => x.BranchNo == branchNo && x.IsDeleted == 0).OrderBy(x => x.LeaveTypeNo).ToListAsync(ct);

    public async Task<HrmLeaveType?> FindByCodeAsync(string leaveTypeId, long branchNo, CancellationToken ct = default) =>
        await _db.HrmLeaveTypes.AsNoTracking().FirstOrDefaultAsync(x => x.LeaveTypeId == leaveTypeId && x.BranchNo == branchNo && x.IsDeleted == 0, ct);

    public async Task<bool> ExistsByCodeAsync(string leaveTypeId, long branchNo, CancellationToken ct = default) =>
        await _db.HrmLeaveTypes.AnyAsync(x => x.LeaveTypeId == leaveTypeId && x.BranchNo == branchNo && x.IsDeleted == 0, ct);

    public void Add(HrmLeaveType entity) => _db.HrmLeaveTypes.Add(entity);
    public void Update(HrmLeaveType entity) => _db.HrmLeaveTypes.Update(entity);
}

public class HrmHolidayRepository : IHrmHolidayRepository
{
    private readonly IHrmDbContext _db;
    public HrmHolidayRepository(IHrmDbContext db) => _db = db;

    public async Task<List<HrmHoliday>> FindAllAsync(CancellationToken ct = default) =>
        await _db.HrmHolidays.AsNoTracking().Where(x => x.IsDeleted == 0).OrderBy(x => x.HolidayDate).ToListAsync(ct);

    public async Task<List<HrmHoliday>> FindAllPaginatedAsync(int skip, int take, CancellationToken ct = default) =>
        await _db.HrmHolidays.AsNoTracking().Where(x => x.IsDeleted == 0).OrderBy(x => x.HolidayDate).Skip(skip).Take(take).ToListAsync(ct);

    public async Task<HrmHoliday?> FindByIdAsync(long holidayNo, CancellationToken ct = default) =>
        await _db.HrmHolidays.AsNoTracking().FirstOrDefaultAsync(x => x.HolidayNo == holidayNo && x.IsDeleted == 0, ct);

    public async Task<List<HrmHoliday>> FindByBranchAsync(long branchNo, CancellationToken ct = default) =>
        await _db.HrmHolidays.AsNoTracking().Where(x => x.BranchNo == branchNo && x.IsDeleted == 0).OrderBy(x => x.HolidayDate).ToListAsync(ct);

    public async Task<HrmHoliday?> FindByDateAndBranchAsync(DateOnly holidayDate, long branchNo, CancellationToken ct = default) =>
        await _db.HrmHolidays.AsNoTracking().FirstOrDefaultAsync(x => x.HolidayDate == holidayDate && x.BranchNo == branchNo && x.IsDeleted == 0, ct);

    public async Task<bool> ExistsByDateAndBranchAsync(DateOnly holidayDate, long branchNo, CancellationToken ct = default) =>
        await _db.HrmHolidays.AnyAsync(x => x.HolidayDate == holidayDate && x.BranchNo == branchNo && x.IsDeleted == 0, ct);

    public void Add(HrmHoliday entity) => _db.HrmHolidays.Add(entity);
    public void Update(HrmHoliday entity) => _db.HrmHolidays.Update(entity);
}

public class HrmSalaryComponentRepository : IHrmSalaryComponentRepository
{
    private readonly IHrmDbContext _db;
    public HrmSalaryComponentRepository(IHrmDbContext db) => _db = db;

    public async Task<List<HrmSalaryComponent>> FindAllAsync(CancellationToken ct = default) =>
        await _db.HrmSalaryComponents.AsNoTracking().Where(x => x.IsDeleted == 0).OrderBy(x => x.ComponentNo).ToListAsync(ct);

    public async Task<List<HrmSalaryComponent>> FindAllPaginatedAsync(int skip, int take, CancellationToken ct = default) =>
        await _db.HrmSalaryComponents.AsNoTracking().Where(x => x.IsDeleted == 0).OrderBy(x => x.ComponentNo).Skip(skip).Take(take).ToListAsync(ct);

    public async Task<HrmSalaryComponent?> FindByIdAsync(long componentNo, CancellationToken ct = default) =>
        await _db.HrmSalaryComponents.AsNoTracking().FirstOrDefaultAsync(x => x.ComponentNo == componentNo && x.IsDeleted == 0, ct);

    public async Task<List<HrmSalaryComponent>> FindByBranchAsync(long branchNo, CancellationToken ct = default) =>
        await _db.HrmSalaryComponents.AsNoTracking().Where(x => x.BranchNo == branchNo && x.IsDeleted == 0).OrderBy(x => x.ComponentNo).ToListAsync(ct);

    public async Task<HrmSalaryComponent?> FindByCodeAsync(string componentId, long branchNo, CancellationToken ct = default) =>
        await _db.HrmSalaryComponents.AsNoTracking().FirstOrDefaultAsync(x => x.ComponentId == componentId && x.BranchNo == branchNo && x.IsDeleted == 0, ct);

    public async Task<bool> ExistsByCodeAsync(string componentId, long branchNo, CancellationToken ct = default) =>
        await _db.HrmSalaryComponents.AnyAsync(x => x.ComponentId == componentId && x.BranchNo == branchNo && x.IsDeleted == 0, ct);

    /// <summary>
    /// Port of Java <c>findPreferredByComponentId</c>: returns branch-specific row first,
    /// then global fallback (branch_no IS NULL), ordered by CASE WHEN.
    /// </summary>
    public async Task<List<HrmSalaryComponent>> FindPreferredByComponentIdAsync(string componentId, long branchNo, CancellationToken ct = default) =>
        await _db.HrmSalaryComponents.AsNoTracking()
            .Where(x => x.ComponentId == componentId && x.IsDeleted == 0 && (x.BranchNo == branchNo || x.BranchNo == null))
            .OrderBy(x => x.BranchNo == branchNo ? 0 : 1).ThenBy(x => x.ComponentNo)
            .ToListAsync(ct);

    public void Add(HrmSalaryComponent entity) => _db.HrmSalaryComponents.Add(entity);
    public void Update(HrmSalaryComponent entity) => _db.HrmSalaryComponents.Update(entity);
}

public class HrmPayrollPolicyRepository : IHrmPayrollPolicyRepository
{
    private readonly IHrmDbContext _db;
    public HrmPayrollPolicyRepository(IHrmDbContext db) => _db = db;

    public async Task<List<HrmPayrollPolicy>> FindAllAsync(CancellationToken ct = default) =>
        await _db.HrmPayrollPolicies.AsNoTracking().Where(x => x.IsDeleted == 0).OrderByDescending(x => x.PolicyNo).ToListAsync(ct);

    public async Task<HrmPayrollPolicy?> FindByIdAsync(long policyNo, CancellationToken ct = default) =>
        await _db.HrmPayrollPolicies.AsNoTracking().FirstOrDefaultAsync(x => x.PolicyNo == policyNo && x.IsDeleted == 0, ct);

    public async Task<List<HrmPayrollPolicy>> FindActivePoliciesAsync(string countryCode, DateOnly asOf, CancellationToken ct = default) =>
        // HrmPayrollPolicy .NET entity does not yet have CountryCode/EffectiveFrom/EffectiveTo columns.
        // Return all active policies ordered by PolicyNo; the caller filters by CompanyNo.
        await _db.HrmPayrollPolicies.AsNoTracking()
            .Where(x => x.IsDeleted == 0 && x.IsActive == 1)
            .OrderByDescending(x => x.PolicyNo)
            .ToListAsync(ct);

    public void Add(HrmPayrollPolicy entity) => _db.HrmPayrollPolicies.Add(entity);
    public void Update(HrmPayrollPolicy entity) => _db.HrmPayrollPolicies.Update(entity);
}

public class HrmTaxSlabRepository : IHrmTaxSlabRepository
{
    private readonly IHrmDbContext _db;
    public HrmTaxSlabRepository(IHrmDbContext db) => _db = db;

    public async Task<List<HrmTaxSlab>> FindAllProfilesOrderedAsync(CancellationToken ct = default) =>
        await _db.HrmTaxSlabs.AsNoTracking().Where(x => x.IsDeleted == 0)
            .OrderBy(x => x.CountryCode).ThenByDescending(x => x.FiscalYear).ThenBy(x => x.TaxpayerClass).ThenBy(x => x.SlabOrder).ThenBy(x => x.TaxSlabNo)
            .ToListAsync(ct);

    public async Task<HrmTaxSlab?> FindByIdAsync(long taxSlabNo, CancellationToken ct = default) =>
        await _db.HrmTaxSlabs.AsNoTracking().FirstOrDefaultAsync(x => x.TaxSlabNo == taxSlabNo && x.IsDeleted == 0, ct);

    public async Task<List<HrmTaxSlab>> FindByProfileAsync(string countryCode, string fiscalYear, string taxpayerClass, CancellationToken ct = default) =>
        await _db.HrmTaxSlabs.AsNoTracking()
            .Where(x => x.CountryCode == countryCode && x.FiscalYear == fiscalYear && x.TaxpayerClass == taxpayerClass && x.IsDeleted == 0)
            .OrderBy(x => x.SlabOrder).ToListAsync(ct);

    public async Task<bool> ExistsByProfileAsync(string countryCode, string fiscalYear, string taxpayerClass, CancellationToken ct = default) =>
        await _db.HrmTaxSlabs.AnyAsync(x => x.CountryCode == countryCode && x.FiscalYear == fiscalYear && x.TaxpayerClass == taxpayerClass && x.IsDeleted == 0, ct);

    public void Add(HrmTaxSlab entity) => _db.HrmTaxSlabs.Add(entity);
    public void Update(HrmTaxSlab entity) => _db.HrmTaxSlabs.Update(entity);
    public void RemoveRange(IEnumerable<HrmTaxSlab> entities) => _db.HrmTaxSlabs.RemoveRange(entities);
}

// ── Attendance (1100-series) ─────────────────────────────────────────────

public class HrmAttendanceRepository : IHrmAttendanceRepository
{
    private readonly IHrmDbContext _db;
    public HrmAttendanceRepository(IHrmDbContext db) => _db = db;

    public async Task<List<HrmAttendance>> FindAllAsync(CancellationToken ct = default) =>
        await _db.HrmAttendances.AsNoTracking().Where(x => x.IsDeleted == 0).OrderByDescending(x => x.AttDate).ThenByDescending(x => x.AttendanceNo).ToListAsync(ct);

    public async Task<List<HrmAttendance>> FindAllPaginatedAsync(int skip, int take, CancellationToken ct = default) =>
        await _db.HrmAttendances.AsNoTracking().Where(x => x.IsDeleted == 0).OrderByDescending(x => x.AttDate).ThenByDescending(x => x.AttendanceNo).Skip(skip).Take(take).ToListAsync(ct);

    public async Task<HrmAttendance?> FindByIdAsync(long attendanceNo, CancellationToken ct = default) =>
        await _db.HrmAttendances.AsNoTracking().FirstOrDefaultAsync(x => x.AttendanceNo == attendanceNo && x.IsDeleted == 0, ct);

    public async Task<List<HrmAttendance>> FindByBranchAsync(long branchNo, CancellationToken ct = default) =>
        await _db.HrmAttendances.AsNoTracking().Where(x => x.BranchNo == branchNo && x.IsDeleted == 0).OrderByDescending(x => x.AttDate).ThenByDescending(x => x.AttendanceNo).ToListAsync(ct);

    public async Task<List<HrmAttendance>> FindByEmployeeAsync(long employeeNo, CancellationToken ct = default) =>
        await _db.HrmAttendances.AsNoTracking().Where(x => x.EmployeeNo == employeeNo && x.IsDeleted == 0).OrderByDescending(x => x.AttDate).ToListAsync(ct);

    public async Task<HrmAttendance?> FindByEmployeeAndDateAsync(long employeeNo, DateTime attDate, CancellationToken ct = default) =>
        await _db.HrmAttendances.AsNoTracking().FirstOrDefaultAsync(x => x.EmployeeNo == employeeNo && x.AttDate == attDate && x.IsDeleted == 0, ct);

    public async Task<bool> ExistsByEmployeeAndDateAsync(long employeeNo, DateTime attDate, CancellationToken ct = default) =>
        await _db.HrmAttendances.AnyAsync(x => x.EmployeeNo == employeeNo && x.AttDate == attDate && x.IsDeleted == 0, ct);

    public void Add(HrmAttendance entity) => _db.HrmAttendances.Add(entity);
    public void Update(HrmAttendance entity) => _db.HrmAttendances.Update(entity);
}

public class HrmAttendanceAdjustmentRepository : IHrmAttendanceAdjustmentRepository
{
    private readonly IHrmDbContext _db;
    public HrmAttendanceAdjustmentRepository(IHrmDbContext db) => _db = db;

    public async Task<List<HrmAttendanceAdjustment>> FindAllAsync(CancellationToken ct = default) =>
        await _db.HrmAttendanceAdjustments.AsNoTracking().Where(x => x.IsDeleted == 0).OrderByDescending(x => x.AdjustmentNo).ToListAsync(ct);

    public async Task<List<HrmAttendanceAdjustment>> FindAllPaginatedAsync(int skip, int take, CancellationToken ct = default) =>
        await _db.HrmAttendanceAdjustments.AsNoTracking().Where(x => x.IsDeleted == 0).OrderByDescending(x => x.AdjustmentNo).Skip(skip).Take(take).ToListAsync(ct);

    public async Task<HrmAttendanceAdjustment?> FindByIdAsync(long adjustmentNo, CancellationToken ct = default) =>
        await _db.HrmAttendanceAdjustments.AsNoTracking().FirstOrDefaultAsync(x => x.AdjustmentNo == adjustmentNo && x.IsDeleted == 0, ct);

    public async Task<List<HrmAttendanceAdjustment>> FindByEmployeeAsync(long employeeNo, CancellationToken ct = default) =>
        await _db.HrmAttendanceAdjustments.AsNoTracking().Where(x => x.EmployeeNo == employeeNo && x.IsDeleted == 0).OrderByDescending(x => x.AttDate).ToListAsync(ct);

    public void Add(HrmAttendanceAdjustment entity) => _db.HrmAttendanceAdjustments.Add(entity);
    public void Update(HrmAttendanceAdjustment entity) => _db.HrmAttendanceAdjustments.Update(entity);
}

public class HrmShiftRosterRepository : IHrmShiftRosterRepository
{
    private readonly IHrmDbContext _db;
    public HrmShiftRosterRepository(IHrmDbContext db) => _db = db;

    public async Task<List<HrmShiftRoster>> FindAllAsync(CancellationToken ct = default) =>
        await _db.HrmShiftRosters.AsNoTracking().Where(x => x.IsDeleted == 0).OrderByDescending(x => x.RosterNo).ToListAsync(ct);

    public async Task<List<HrmShiftRoster>> FindAllPaginatedAsync(int skip, int take, CancellationToken ct = default) =>
        await _db.HrmShiftRosters.AsNoTracking().Where(x => x.IsDeleted == 0).OrderByDescending(x => x.RosterNo).Skip(skip).Take(take).ToListAsync(ct);

    public async Task<HrmShiftRoster?> FindByIdAsync(long rosterNo, CancellationToken ct = default) =>
        await _db.HrmShiftRosters.AsNoTracking().FirstOrDefaultAsync(x => x.RosterNo == rosterNo && x.IsDeleted == 0, ct);

    public async Task<bool> ExistsByCodeAsync(string rosterId, long branchNo, CancellationToken ct = default) =>
        await _db.HrmShiftRosters.AnyAsync(x => x.RosterId == rosterId && x.BranchNo == branchNo && x.IsDeleted == 0, ct);

    public void Add(HrmShiftRoster entity) => _db.HrmShiftRosters.Add(entity);
    public void Update(HrmShiftRoster entity) => _db.HrmShiftRosters.Update(entity);
}

public class HrmShiftRosterLineRepository : IHrmShiftRosterLineRepository
{
    private readonly IHrmDbContext _db;
    public HrmShiftRosterLineRepository(IHrmDbContext db) => _db = db;

    public async Task<List<HrmShiftRosterLine>> FindByRosterAsync(long rosterNo, CancellationToken ct = default) =>
        await _db.HrmShiftRosterLines.AsNoTracking().Where(x => x.RosterNo == rosterNo && x.IsDeleted == 0).OrderBy(x => x.RosterLineNo).ToListAsync(ct);

    public void Add(HrmShiftRosterLine entity) => _db.HrmShiftRosterLines.Add(entity);
    public void AddRange(IEnumerable<HrmShiftRosterLine> entities) => _db.HrmShiftRosterLines.AddRange(entities);
    public void RemoveRange(IEnumerable<HrmShiftRosterLine> entities) => _db.HrmShiftRosterLines.RemoveRange(entities);
}

public class HrmOvertimeRepository : IHrmOvertimeRepository
{
    private readonly IHrmDbContext _db;
    public HrmOvertimeRepository(IHrmDbContext db) => _db = db;

    public async Task<List<HrmOvertime>> FindAllAsync(CancellationToken ct = default) =>
        await _db.HrmOvertimes.AsNoTracking().Where(x => x.IsDeleted == 0).OrderByDescending(x => x.OvertimeNo).ToListAsync(ct);

    public async Task<List<HrmOvertime>> FindAllPaginatedAsync(int skip, int take, CancellationToken ct = default) =>
        await _db.HrmOvertimes.AsNoTracking().Where(x => x.IsDeleted == 0).OrderByDescending(x => x.OvertimeNo).Skip(skip).Take(take).ToListAsync(ct);

    public async Task<HrmOvertime?> FindByIdAsync(long overtimeNo, CancellationToken ct = default) =>
        await _db.HrmOvertimes.AsNoTracking().FirstOrDefaultAsync(x => x.OvertimeNo == overtimeNo && x.IsDeleted == 0, ct);

    public async Task<bool> ExistsByCodeAsync(string overtimeId, long branchNo, CancellationToken ct = default) =>
        await _db.HrmOvertimes.AnyAsync(x => x.OvertimeId == overtimeId && x.BranchNo == branchNo && x.IsDeleted == 0, ct);

    public void Add(HrmOvertime entity) => _db.HrmOvertimes.Add(entity);
    public void Update(HrmOvertime entity) => _db.HrmOvertimes.Update(entity);
}

public class HrmEmployeeMovementRepository : IHrmEmployeeMovementRepository
{
    private readonly IHrmDbContext _db;
    public HrmEmployeeMovementRepository(IHrmDbContext db) => _db = db;

    public async Task<List<HrmEmployeeMovement>> FindAllAsync(CancellationToken ct = default) =>
        await _db.HrmEmployeeMovements.AsNoTracking().Where(x => x.IsDeleted == 0).OrderByDescending(x => x.MovementNo).ToListAsync(ct);

    public async Task<List<HrmEmployeeMovement>> FindAllPaginatedAsync(int skip, int take, CancellationToken ct = default) =>
        await _db.HrmEmployeeMovements.AsNoTracking().Where(x => x.IsDeleted == 0).OrderByDescending(x => x.MovementNo).Skip(skip).Take(take).ToListAsync(ct);

    public async Task<HrmEmployeeMovement?> FindByIdAsync(long movementNo, CancellationToken ct = default) =>
        await _db.HrmEmployeeMovements.AsNoTracking().FirstOrDefaultAsync(x => x.MovementNo == movementNo && x.IsDeleted == 0, ct);

    public async Task<List<HrmEmployeeMovement>> FindByEmployeeAsync(long employeeNo, CancellationToken ct = default) =>
        await _db.HrmEmployeeMovements.AsNoTracking().Where(x => x.EmployeeNo == employeeNo && x.IsDeleted == 0).OrderByDescending(x => x.MovementNo).ToListAsync(ct);

    public async Task<bool> ExistsByCodeAsync(string movementId, long branchNo, CancellationToken ct = default) =>
        await _db.HrmEmployeeMovements.AnyAsync(x => x.MovementId == movementId && x.BranchNo == branchNo && x.IsDeleted == 0, ct);

    public void Add(HrmEmployeeMovement entity) => _db.HrmEmployeeMovements.Add(entity);
    public void Update(HrmEmployeeMovement entity) => _db.HrmEmployeeMovements.Update(entity);
}

// ── Payroll (1200-series) ────────────────────────────────────────────────

public class HrmSalaryStructureRepository : IHrmSalaryStructureRepository
{
    private readonly IHrmDbContext _db;
    public HrmSalaryStructureRepository(IHrmDbContext db) => _db = db;

    public async Task<List<HrmSalaryStructure>> FindAllAsync(CancellationToken ct = default) =>
        await _db.HrmSalaryStructures.AsNoTracking().Where(x => x.IsDeleted == 0).OrderByDescending(x => x.SalaryStructureNo).ToListAsync(ct);

    public async Task<List<HrmSalaryStructure>> FindAllPaginatedAsync(int skip, int take, CancellationToken ct = default) =>
        await _db.HrmSalaryStructures.AsNoTracking().Where(x => x.IsDeleted == 0).OrderByDescending(x => x.SalaryStructureNo).Skip(skip).Take(take).ToListAsync(ct);

    public async Task<HrmSalaryStructure?> FindByIdAsync(long salaryStructureNo, CancellationToken ct = default) =>
        await _db.HrmSalaryStructures.AsNoTracking().FirstOrDefaultAsync(x => x.SalaryStructureNo == salaryStructureNo && x.IsDeleted == 0, ct);

    public async Task<List<HrmSalaryStructure>> FindByEmployeeAsync(long employeeNo, CancellationToken ct = default) =>
        await _db.HrmSalaryStructures.AsNoTracking().Where(x => x.EmployeeNo == employeeNo && x.IsDeleted == 0).OrderByDescending(x => x.EffectiveFrom).ToListAsync(ct);

    public async Task<HrmSalaryStructure?> FindByEmployeeAndStatusAsync(long employeeNo, short status, CancellationToken ct = default) =>
        await _db.HrmSalaryStructures.AsNoTracking().FirstOrDefaultAsync(x => x.EmployeeNo == employeeNo && x.Status == status && x.IsDeleted == 0, ct);

    public void Add(HrmSalaryStructure entity) => _db.HrmSalaryStructures.Add(entity);
    public void Update(HrmSalaryStructure entity) => _db.HrmSalaryStructures.Update(entity);
}

public class HrmSalaryStructureDtlRepository : IHrmSalaryStructureDtlRepository
{
    private readonly IHrmDbContext _db;
    public HrmSalaryStructureDtlRepository(IHrmDbContext db) => _db = db;

    public async Task<List<HrmSalaryStructureDtl>> FindByStructureAsync(long salaryStructureNo, CancellationToken ct = default) =>
        await _db.HrmSalaryStructureDtls.AsNoTracking().Where(x => x.SalaryStructureNo == salaryStructureNo && x.IsDeleted == 0).OrderBy(x => x.StructureDtlNo).ToListAsync(ct);

    public void Add(HrmSalaryStructureDtl entity) => _db.HrmSalaryStructureDtls.Add(entity);
    public void AddRange(IEnumerable<HrmSalaryStructureDtl> entities) => _db.HrmSalaryStructureDtls.AddRange(entities);
    public void RemoveRange(IEnumerable<HrmSalaryStructureDtl> entities) => _db.HrmSalaryStructureDtls.RemoveRange(entities);
}

public class HrmPayrollRunRepository : IHrmPayrollRunRepository
{
    private readonly IHrmDbContext _db;
    public HrmPayrollRunRepository(IHrmDbContext db) => _db = db;

    public async Task<List<HrmPayrollRun>> FindAllAsync(CancellationToken ct = default) =>
        await _db.HrmPayrollRuns.AsNoTracking().Where(x => x.IsDeleted == 0).OrderByDescending(x => x.PayrollRunNo).ToListAsync(ct);

    public async Task<List<HrmPayrollRun>> FindAllPaginatedAsync(int skip, int take, CancellationToken ct = default) =>
        await _db.HrmPayrollRuns.AsNoTracking().Where(x => x.IsDeleted == 0).OrderByDescending(x => x.PayrollRunNo).Skip(skip).Take(take).ToListAsync(ct);

    public async Task<HrmPayrollRun?> FindByIdAsync(long payrollRunNo, CancellationToken ct = default) =>
        await _db.HrmPayrollRuns.AsNoTracking().FirstOrDefaultAsync(x => x.PayrollRunNo == payrollRunNo && x.IsDeleted == 0, ct);

    public async Task<bool> ExistsDuplicateAsync(long branchNo, string payPeriod, short runType, CancellationToken ct = default) =>
        await _db.HrmPayrollRuns.AnyAsync(x => x.BranchNo == branchNo && x.PayPeriod == payPeriod && x.RunType == runType && x.IsDeleted == 0, ct);

    public async Task<bool> ExistsDuplicateWithBonusAsync(long branchNo, string payPeriod, short runType, long bonusRunNo, CancellationToken ct = default) =>
        await _db.HrmPayrollRuns.AnyAsync(x => x.BranchNo == branchNo && x.PayPeriod == payPeriod && x.RunType == runType && x.BonusRunNo == bonusRunNo && x.IsDeleted == 0, ct);

    public async Task<HrmPayrollRun?> FindByBonusRunAsync(long bonusRunNo, CancellationToken ct = default) =>
        await _db.HrmPayrollRuns.AsNoTracking().FirstOrDefaultAsync(x => x.BonusRunNo == bonusRunNo && x.IsDeleted == 0, ct);

    public void Add(HrmPayrollRun entity) => _db.HrmPayrollRuns.Add(entity);
    public void Update(HrmPayrollRun entity) => _db.HrmPayrollRuns.Update(entity);
}

public class HrmPayslipRepository : IHrmPayslipRepository
{
    private readonly IHrmDbContext _db;
    public HrmPayslipRepository(IHrmDbContext db) => _db = db;

    public async Task<List<HrmPayslip>> FindByRunAsync(long payrollRunNo, CancellationToken ct = default) =>
        await _db.HrmPayslips.AsNoTracking().Where(x => x.PayrollRunNo == payrollRunNo && x.IsDeleted == 0).OrderBy(x => x.PayslipNo).ToListAsync(ct);

    public async Task<HrmPayslip?> FindByIdAsync(long payslipNo, CancellationToken ct = default) =>
        await _db.HrmPayslips.AsNoTracking().FirstOrDefaultAsync(x => x.PayslipNo == payslipNo && x.IsDeleted == 0, ct);

    public async Task<List<HrmSettlementPayrollDueView>> FindSettlementDueRowsAsync(long employeeNo, long branchNo, DateTime lastWorkingDay, CancellationToken ct = default) =>
        await (from s in _db.HrmPayslips.AsNoTracking()
               join r in _db.HrmPayrollRuns.AsNoTracking() on s.PayrollRunNo equals r.PayrollRunNo
               where s.EmployeeNo == employeeNo && s.BranchNo == branchNo
                     && s.IsDeleted == 0 && r.IsDeleted == 0
                     && r.PeriodEnd <= lastWorkingDay
               orderby r.PeriodEnd, s.PayslipNo
               select new HrmSettlementPayrollDueView
               {
                   PayslipNo = s.PayslipNo,
                   PayrollRunNo = s.PayrollRunNo,
                   PayPeriod = r.PayPeriod,
                   RunType = r.RunType,
                   RunStatus = r.Status,
                   PeriodEnd = DateOnly.FromDateTime(r.PeriodEnd),
                   NetPay = s.NetPay,
                   PaymentStatus = s.PaymentStatus
               }).ToListAsync(ct);

    public void Add(HrmPayslip entity) => _db.HrmPayslips.Add(entity);
    public void AddRange(IEnumerable<HrmPayslip> entities) => _db.HrmPayslips.AddRange(entities);
    public void Update(HrmPayslip entity) => _db.HrmPayslips.Update(entity);
}

public class HrmPayslipDtlRepository : IHrmPayslipDtlRepository
{
    private readonly IHrmDbContext _db;
    public HrmPayslipDtlRepository(IHrmDbContext db) => _db = db;

    public async Task<List<HrmPayslipDtl>> FindByPayslipAsync(long payslipNo, CancellationToken ct = default) =>
        await _db.HrmPayslipDtls.AsNoTracking().Where(x => x.PayslipNo == payslipNo && x.IsDeleted == 0).OrderBy(x => x.PayslipDtlNo).ToListAsync(ct);

    public void Add(HrmPayslipDtl entity) => _db.HrmPayslipDtls.Add(entity);
    public void AddRange(IEnumerable<HrmPayslipDtl> entities) => _db.HrmPayslipDtls.AddRange(entities);
    public void RemoveRange(IEnumerable<HrmPayslipDtl> entities) => _db.HrmPayslipDtls.RemoveRange(entities);
}

public class HrmBonusRunRepository : IHrmBonusRunRepository
{
    private readonly IHrmDbContext _db;
    public HrmBonusRunRepository(IHrmDbContext db) => _db = db;

    public async Task<List<HrmBonusRun>> FindAllAsync(CancellationToken ct = default) =>
        await _db.HrmBonusRuns.AsNoTracking().Where(x => x.IsDeleted == 0).OrderByDescending(x => x.BonusRunNo).ToListAsync(ct);

    public async Task<List<HrmBonusRun>> FindAllPaginatedAsync(int skip, int take, CancellationToken ct = default) =>
        await _db.HrmBonusRuns.AsNoTracking().Where(x => x.IsDeleted == 0).OrderByDescending(x => x.BonusRunNo).Skip(skip).Take(take).ToListAsync(ct);

    public async Task<HrmBonusRun?> FindByIdAsync(long bonusRunNo, CancellationToken ct = default) =>
        await _db.HrmBonusRuns.AsNoTracking().FirstOrDefaultAsync(x => x.BonusRunNo == bonusRunNo && x.IsDeleted == 0, ct);

    public async Task<bool> ExistsByCodeAsync(string bonusId, long branchNo, CancellationToken ct = default) =>
        // HrmBonusRun .NET entity does not have a BonusId column; use BonusTitle as the business code.
        await _db.HrmBonusRuns.AnyAsync(x => x.BonusTitle == bonusId && x.BranchNo == branchNo && x.IsDeleted == 0, ct);

    public void Add(HrmBonusRun entity) => _db.HrmBonusRuns.Add(entity);
    public void Update(HrmBonusRun entity) => _db.HrmBonusRuns.Update(entity);
}

public class HrmBonusLineRepository : IHrmBonusLineRepository
{
    private readonly IHrmDbContext _db;
    public HrmBonusLineRepository(IHrmDbContext db) => _db = db;

    public async Task<List<HrmBonusLine>> FindByRunAsync(long bonusRunNo, CancellationToken ct = default) =>
        await _db.HrmBonusLines.AsNoTracking().Where(x => x.BonusRunNo == bonusRunNo && x.IsDeleted == 0).OrderBy(x => x.BonusLineNo).ToListAsync(ct);

    public async Task<HrmBonusLine?> FindByRunAndEmployeeAsync(long bonusRunNo, long employeeNo, CancellationToken ct = default) =>
        await _db.HrmBonusLines.AsNoTracking().FirstOrDefaultAsync(x => x.BonusRunNo == bonusRunNo && x.EmployeeNo == employeeNo && x.IsDeleted == 0, ct);

    public void Add(HrmBonusLine entity) => _db.HrmBonusLines.Add(entity);
    public void AddRange(IEnumerable<HrmBonusLine> entities) => _db.HrmBonusLines.AddRange(entities);
    public void RemoveRange(IEnumerable<HrmBonusLine> entities) => _db.HrmBonusLines.RemoveRange(entities);
}

public class HrmBonusScopeDesignationRepository : IHrmBonusScopeDesignationRepository
{
    private readonly IHrmDbContext _db;
    public HrmBonusScopeDesignationRepository(IHrmDbContext db) => _db = db;

    public async Task<List<HrmBonusScopeDesignation>> FindByRunAsync(long bonusRunNo, CancellationToken ct = default) =>
        await _db.HrmBonusScopeDesignations.AsNoTracking().Where(x => x.BonusRunNo == bonusRunNo && x.IsDeleted == 0).OrderBy(x => x.ScopeDesigNo).ToListAsync(ct);

    public void Add(HrmBonusScopeDesignation entity) => _db.HrmBonusScopeDesignations.Add(entity);
    public void AddRange(IEnumerable<HrmBonusScopeDesignation> entities) => _db.HrmBonusScopeDesignations.AddRange(entities);
    public void RemoveRange(IEnumerable<HrmBonusScopeDesignation> entities) => _db.HrmBonusScopeDesignations.RemoveRange(entities);
}

public class HrmBonusScopeEmployeeRepository : IHrmBonusScopeEmployeeRepository
{
    private readonly IHrmDbContext _db;
    public HrmBonusScopeEmployeeRepository(IHrmDbContext db) => _db = db;

    public async Task<List<HrmBonusScopeEmployee>> FindByRunAsync(long bonusRunNo, CancellationToken ct = default) =>
        await _db.HrmBonusScopeEmployees.AsNoTracking().Where(x => x.BonusRunNo == bonusRunNo && x.IsDeleted == 0).OrderBy(x => x.ScopeEmpNo).ToListAsync(ct);

    public void Add(HrmBonusScopeEmployee entity) => _db.HrmBonusScopeEmployees.Add(entity);
    public void AddRange(IEnumerable<HrmBonusScopeEmployee> entities) => _db.HrmBonusScopeEmployees.AddRange(entities);
    public void RemoveRange(IEnumerable<HrmBonusScopeEmployee> entities) => _db.HrmBonusScopeEmployees.RemoveRange(entities);
}

public class HrmLoanAdvanceRepository : IHrmLoanAdvanceRepository
{
    private readonly IHrmDbContext _db;
    public HrmLoanAdvanceRepository(IHrmDbContext db) => _db = db;

    public async Task<List<HrmLoanAdvance>> FindAllAsync(CancellationToken ct = default) =>
        await _db.HrmLoanAdvances.AsNoTracking().Where(x => x.IsDeleted == 0).OrderByDescending(x => x.LoanNo).ToListAsync(ct);

    public async Task<List<HrmLoanAdvance>> FindAllPaginatedAsync(int skip, int take, CancellationToken ct = default) =>
        await _db.HrmLoanAdvances.AsNoTracking().Where(x => x.IsDeleted == 0).OrderByDescending(x => x.LoanNo).Skip(skip).Take(take).ToListAsync(ct);

    public async Task<HrmLoanAdvance?> FindByIdAsync(long loanNo, CancellationToken ct = default) =>
        await _db.HrmLoanAdvances.AsNoTracking().FirstOrDefaultAsync(x => x.LoanNo == loanNo && x.IsDeleted == 0, ct);

    public async Task<HrmLoanAdvance?> FindByCodeAsync(string loanId, long branchNo, CancellationToken ct = default) =>
        await _db.HrmLoanAdvances.AsNoTracking().FirstOrDefaultAsync(x => x.LoanId == loanId && x.BranchNo == branchNo && x.IsDeleted == 0, ct);

    public async Task<bool> ExistsByCodeAsync(string loanId, long branchNo, CancellationToken ct = default) =>
        await _db.HrmLoanAdvances.AnyAsync(x => x.LoanId == loanId && x.BranchNo == branchNo && x.IsDeleted == 0, ct);

    public void Add(HrmLoanAdvance entity) => _db.HrmLoanAdvances.Add(entity);
    public void Update(HrmLoanAdvance entity) => _db.HrmLoanAdvances.Update(entity);
}

public class HrmFinalSettlementRepository : IHrmFinalSettlementRepository
{
    private readonly IHrmDbContext _db;
    public HrmFinalSettlementRepository(IHrmDbContext db) => _db = db;

    public async Task<List<HrmFinalSettlement>> FindAllAsync(CancellationToken ct = default) =>
        await _db.HrmFinalSettlements.AsNoTracking().Where(x => x.IsDeleted == 0).OrderByDescending(x => x.SettlementNo).ToListAsync(ct);

    public async Task<List<HrmFinalSettlement>> FindAllPaginatedAsync(int skip, int take, CancellationToken ct = default) =>
        await _db.HrmFinalSettlements.AsNoTracking().Where(x => x.IsDeleted == 0).OrderByDescending(x => x.SettlementNo).Skip(skip).Take(take).ToListAsync(ct);

    public async Task<HrmFinalSettlement?> FindByIdAsync(long settlementNo, CancellationToken ct = default) =>
        await _db.HrmFinalSettlements.AsNoTracking().FirstOrDefaultAsync(x => x.SettlementNo == settlementNo && x.IsDeleted == 0, ct);

    public async Task<HrmFinalSettlement?> FindByCodeAsync(string settlementId, long branchNo, CancellationToken ct = default) =>
        await _db.HrmFinalSettlements.AsNoTracking().FirstOrDefaultAsync(x => x.SettlementId == settlementId && x.BranchNo == branchNo && x.IsDeleted == 0, ct);

    public async Task<bool> ExistsByCodeAsync(string settlementId, long branchNo, CancellationToken ct = default) =>
        await _db.HrmFinalSettlements.AnyAsync(x => x.SettlementId == settlementId && x.BranchNo == branchNo && x.IsDeleted == 0, ct);

    public void Add(HrmFinalSettlement entity) => _db.HrmFinalSettlements.Add(entity);
    public void Update(HrmFinalSettlement entity) => _db.HrmFinalSettlements.Update(entity);
}

// ── Leave (1300-series) ──────────────────────────────────────────────────

public class HrmLeaveApplicationRepository : IHrmLeaveApplicationRepository
{
    private readonly IHrmDbContext _db;
    public HrmLeaveApplicationRepository(IHrmDbContext db) => _db = db;

    public async Task<List<HrmLeaveApplication>> FindAllAsync(CancellationToken ct = default) =>
        await _db.HrmLeaveApplications.AsNoTracking().Where(x => x.IsDeleted == 0).OrderByDescending(x => x.LeaveApplicationNo).ToListAsync(ct);

    public async Task<List<HrmLeaveApplication>> FindAllPaginatedAsync(int skip, int take, CancellationToken ct = default) =>
        await _db.HrmLeaveApplications.AsNoTracking().Where(x => x.IsDeleted == 0).OrderByDescending(x => x.LeaveApplicationNo).Skip(skip).Take(take).ToListAsync(ct);

    public async Task<HrmLeaveApplication?> FindByIdAsync(long leaveApplicationNo, CancellationToken ct = default) =>
        await _db.HrmLeaveApplications.AsNoTracking().FirstOrDefaultAsync(x => x.LeaveApplicationNo == leaveApplicationNo && x.IsDeleted == 0, ct);

    public async Task<List<HrmLeaveApplication>> FindByEmployeeAsync(long employeeNo, CancellationToken ct = default) =>
        await _db.HrmLeaveApplications.AsNoTracking().Where(x => x.EmployeeNo == employeeNo && x.IsDeleted == 0).OrderByDescending(x => x.FromDate).ToListAsync(ct);

    public async Task<List<HrmLeaveApplication>> FindFilteredHistoryAsync(long employeeNo, long? leaveTypeNo, DateTime? fromDate, DateTime? toDate, CancellationToken ct = default)
    {
        var query = _db.HrmLeaveApplications.AsNoTracking().Where(x => x.EmployeeNo == employeeNo && x.IsDeleted == 0);
        if (leaveTypeNo.HasValue) query = query.Where(x => x.LeaveTypeNo == leaveTypeNo.Value);
        if (fromDate.HasValue) query = query.Where(x => x.FromDate >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(x => x.FromDate <= toDate.Value);
        return await query.OrderByDescending(x => x.FromDate).ToListAsync(ct);
    }

    public async Task<List<HrmLeaveApplication>> FindOverlappingAsync(long employeeNo, DateTime fromDate, DateTime toDate, List<short> blockingStatuses, long? excludeNo, CancellationToken ct = default) =>
        await _db.HrmLeaveApplications.AsNoTracking()
            .Where(x => x.EmployeeNo == employeeNo && x.IsDeleted == 0
                && blockingStatuses.Contains(x.Status)
                && (excludeNo == null || x.LeaveApplicationNo != excludeNo)
                && x.FromDate <= toDate && x.ToDate >= fromDate)
            .ToListAsync(ct);

    public async Task<long> CountEmployeesOnLeaveInDepartmentAsync(long departmentNo, DateTime fromDate, DateTime toDate, List<short> blockingStatuses, long? excludeNo, CancellationToken ct = default) =>
        await (from l in _db.HrmLeaveApplications.AsNoTracking()
               join e in _db.HrmEmployees.AsNoTracking() on l.EmployeeNo equals e.EmployeeNo
               where e.DepartmentNo == departmentNo && e.IsDeleted == 0 && l.IsDeleted == 0
                     && blockingStatuses.Contains(l.Status)
                     && (excludeNo == null || l.LeaveApplicationNo != excludeNo)
                     && l.FromDate <= toDate && l.ToDate >= fromDate
               select l.EmployeeNo).Distinct().LongCountAsync(ct);

    public async Task<long> CountActiveEmployeesInDepartmentAsync(long departmentNo, CancellationToken ct = default) =>
        await _db.HrmEmployees.LongCountAsync(x => x.DepartmentNo == departmentNo && x.IsDeleted == 0 && x.IsActive == 1, ct);

    public async Task<List<HrmLeaveApplication>> FindPendingForApprovalAsync(short status, long? branchNo, CancellationToken ct = default)
    {
        var query = _db.HrmLeaveApplications.AsNoTracking().Where(x => x.IsDeleted == 0 && x.Status == status);
        if (branchNo.HasValue) query = query.Where(x => x.BranchNo == branchNo.Value);
        return await query.OrderByDescending(x => x.LeaveApplicationNo).ToListAsync(ct);
    }

    public void Add(HrmLeaveApplication entity) => _db.HrmLeaveApplications.Add(entity);
    public void Update(HrmLeaveApplication entity) => _db.HrmLeaveApplications.Update(entity);
}

public class HrmLeaveBalanceRepository : IHrmLeaveBalanceRepository
{
    private readonly IHrmDbContext _db;
    public HrmLeaveBalanceRepository(IHrmDbContext db) => _db = db;

    public async Task<HrmLeaveBalance?> FindByEmployeeTypeYearAsync(long employeeNo, long leaveTypeNo, int leaveYear, CancellationToken ct = default) =>
        await _db.HrmLeaveBalances.FirstOrDefaultAsync(x => x.EmployeeNo == employeeNo && x.LeaveTypeNo == leaveTypeNo && x.LeaveYear == leaveYear && x.IsDeleted == 0, ct);

    public async Task<List<HrmLeaveBalance>> FindByEmployeeYearAsync(long employeeNo, int leaveYear, CancellationToken ct = default) =>
        await _db.HrmLeaveBalances.AsNoTracking().Where(x => x.EmployeeNo == employeeNo && x.LeaveYear == leaveYear && x.IsDeleted == 0).ToListAsync(ct);

    public async Task<List<HrmLeaveBalance>> FindByYearAsync(int leaveYear, CancellationToken ct = default) =>
        await _db.HrmLeaveBalances.AsNoTracking().Where(x => x.LeaveYear == leaveYear && x.IsDeleted == 0).OrderBy(x => x.EmployeeNo).ToListAsync(ct);

    public async Task<List<HrmLeaveBalance>> FindByYearAndBranchAsync(int leaveYear, long branchNo, CancellationToken ct = default) =>
        await _db.HrmLeaveBalances.AsNoTracking().Where(x => x.LeaveYear == leaveYear && x.BranchNo == branchNo && x.IsDeleted == 0).OrderBy(x => x.EmployeeNo).ToListAsync(ct);

    public void Add(HrmLeaveBalance entity) => _db.HrmLeaveBalances.Add(entity);
    public void Update(HrmLeaveBalance entity) => _db.HrmLeaveBalances.Update(entity);
}

public class HrmLeaveLedgerRepository : IHrmLeaveLedgerRepository
{
    private readonly IHrmDbContext _db;
    public HrmLeaveLedgerRepository(IHrmDbContext db) => _db = db;

    public async Task<List<HrmLeaveLedger>> FindByEmployeeTypeYearAsync(long employeeNo, long leaveTypeNo, int leaveYear, CancellationToken ct = default) =>
        // HrmLeaveLedger .NET entity does not have LeaveYear; filter by TransDate within the year instead.
        await _db.HrmLeaveLedgers.AsNoTracking()
            .Where(x => x.EmployeeNo == employeeNo && x.LeaveTypeNo == leaveTypeNo
                && x.TransDate.Year == leaveYear && x.IsDeleted == 0)
            .OrderBy(x => x.LedgerNo).ToListAsync(ct);

    public void Add(HrmLeaveLedger entity) => _db.HrmLeaveLedgers.Add(entity);
}

public class HrmLeavePolicySetupRepository : IHrmLeavePolicySetupRepository
{
    private readonly IHrmDbContext _db;
    public HrmLeavePolicySetupRepository(IHrmDbContext db) => _db = db;

    public async Task<List<HrmLeavePolicySetup>> FindAllAsync(CancellationToken ct = default) =>
        await _db.HrmLeavePolicySetups.AsNoTracking().Where(x => x.IsDeleted == 0).ToListAsync(ct);

    public async Task<List<HrmLeavePolicySetup>> FindByBranchAsync(long branchNo, CancellationToken ct = default) =>
        await _db.HrmLeavePolicySetups.AsNoTracking().Where(x => x.BranchNo == branchNo && x.IsDeleted == 0).ToListAsync(ct);

    public async Task<HrmLeavePolicySetup?> FindByTypeAndGroupAsync(long leaveTypeNo, long employeeGroupNo, CancellationToken ct = default) =>
        await _db.HrmLeavePolicySetups.AsNoTracking().FirstOrDefaultAsync(x => x.LeaveTypeNo == leaveTypeNo && x.EmployeeGroupNo == employeeGroupNo && x.IsDeleted == 0, ct);

    public void Add(HrmLeavePolicySetup entity) => _db.HrmLeavePolicySetups.Add(entity);
    public void Update(HrmLeavePolicySetup entity) => _db.HrmLeavePolicySetups.Update(entity);
}

public class HrmLeaveApplicationRuleRepository : IHrmLeaveApplicationRuleRepository
{
    private readonly IHrmDbContext _db;
    public HrmLeaveApplicationRuleRepository(IHrmDbContext db) => _db = db;

    public async Task<HrmLeaveApplicationRule?> FindByPolicyAsync(long policyNo, CancellationToken ct = default) =>
        await _db.HrmLeaveApplicationRules.AsNoTracking().FirstOrDefaultAsync(x => x.PolicyNo == policyNo && x.IsDeleted == 0, ct);

    public void Add(HrmLeaveApplicationRule entity) => _db.HrmLeaveApplicationRules.Add(entity);
    public void Update(HrmLeaveApplicationRule entity) => _db.HrmLeaveApplicationRules.Update(entity);
}

// ── Recruitment (1400-series) ────────────────────────────────────────────

public class HrmJobRequisitionRepository : IHrmJobRequisitionRepository
{
    private readonly IHrmDbContext _db;
    public HrmJobRequisitionRepository(IHrmDbContext db) => _db = db;

    public async Task<List<HrmJobRequisition>> FindAllAsync(CancellationToken ct = default) =>
        await _db.HrmJobRequisitions.AsNoTracking().Where(x => x.IsDeleted == 0).OrderByDescending(x => x.RequisitionNo).ToListAsync(ct);

    public async Task<List<HrmJobRequisition>> FindAllPaginatedAsync(int skip, int take, CancellationToken ct = default) =>
        await _db.HrmJobRequisitions.AsNoTracking().Where(x => x.IsDeleted == 0).OrderByDescending(x => x.RequisitionNo).Skip(skip).Take(take).ToListAsync(ct);

    public async Task<HrmJobRequisition?> FindByIdAsync(long requisitionNo, CancellationToken ct = default) =>
        await _db.HrmJobRequisitions.AsNoTracking().FirstOrDefaultAsync(x => x.RequisitionNo == requisitionNo && x.IsDeleted == 0, ct);

    public async Task<List<HrmJobRequisition>> FindByBranchAsync(long branchNo, CancellationToken ct = default) =>
        await _db.HrmJobRequisitions.AsNoTracking().Where(x => x.BranchNo == branchNo && x.IsDeleted == 0).OrderByDescending(x => x.RequisitionNo).ToListAsync(ct);

    public async Task<bool> ExistsByCodeAsync(string requisitionId, long branchNo, CancellationToken ct = default) =>
        await _db.HrmJobRequisitions.AnyAsync(x => x.RequisitionId == requisitionId && x.BranchNo == branchNo && x.IsDeleted == 0, ct);

    public void Add(HrmJobRequisition entity) => _db.HrmJobRequisitions.Add(entity);
    public void Update(HrmJobRequisition entity) => _db.HrmJobRequisitions.Update(entity);
}

public class HrmCandidateRepository : IHrmCandidateRepository
{
    private readonly IHrmDbContext _db;
    public HrmCandidateRepository(IHrmDbContext db) => _db = db;

    public async Task<List<HrmCandidate>> FindAllAsync(CancellationToken ct = default) =>
        await _db.HrmCandidates.AsNoTracking().Where(x => x.IsDeleted == 0).OrderByDescending(x => x.CandidateNo).ToListAsync(ct);

    public async Task<List<HrmCandidate>> FindAllPaginatedAsync(int skip, int take, CancellationToken ct = default) =>
        await _db.HrmCandidates.AsNoTracking().Where(x => x.IsDeleted == 0).OrderByDescending(x => x.CandidateNo).Skip(skip).Take(take).ToListAsync(ct);

    public async Task<HrmCandidate?> FindByIdAsync(long candidateNo, CancellationToken ct = default) =>
        await _db.HrmCandidates.AsNoTracking().FirstOrDefaultAsync(x => x.CandidateNo == candidateNo && x.IsDeleted == 0, ct);

    public async Task<List<HrmCandidate>> FindByBranchAsync(long branchNo, CancellationToken ct = default) =>
        await _db.HrmCandidates.AsNoTracking().Where(x => x.BranchNo == branchNo && x.IsDeleted == 0).OrderByDescending(x => x.CandidateNo).ToListAsync(ct);

    public async Task<List<HrmCandidate>> FindByStatusInAsync(List<short> statuses, CancellationToken ct = default) =>
        await _db.HrmCandidates.AsNoTracking().Where(x => statuses.Contains(x.ApplicationStatus) && x.IsDeleted == 0).OrderByDescending(x => x.CandidateNo).ToListAsync(ct);

    public async Task<bool> ExistsByCodeAsync(string candidateId, long branchNo, CancellationToken ct = default) =>
        await _db.HrmCandidates.AnyAsync(x => x.CandidateId == candidateId && x.BranchNo == branchNo && x.IsDeleted == 0, ct);

    public void Add(HrmCandidate entity) => _db.HrmCandidates.Add(entity);
    public void Update(HrmCandidate entity) => _db.HrmCandidates.Update(entity);
}

public class HrmOfferRepository : IHrmOfferRepository
{
    private readonly IHrmDbContext _db;
    public HrmOfferRepository(IHrmDbContext db) => _db = db;

    public async Task<List<HrmOffer>> FindAllAsync(CancellationToken ct = default) =>
        await _db.HrmOffers.AsNoTracking().Where(x => x.IsDeleted == 0).OrderByDescending(x => x.OfferNo).ToListAsync(ct);

    public async Task<List<HrmOffer>> FindAllPaginatedAsync(int skip, int take, CancellationToken ct = default) =>
        await _db.HrmOffers.AsNoTracking().Where(x => x.IsDeleted == 0).OrderByDescending(x => x.OfferNo).Skip(skip).Take(take).ToListAsync(ct);

    public async Task<HrmOffer?> FindByIdAsync(long offerNo, CancellationToken ct = default) =>
        await _db.HrmOffers.AsNoTracking().FirstOrDefaultAsync(x => x.OfferNo == offerNo && x.IsDeleted == 0, ct);

    public async Task<bool> ExistsByCodeAsync(string offerId, long branchNo, CancellationToken ct = default) =>
        await _db.HrmOffers.AnyAsync(x => x.OfferId == offerId && x.BranchNo == branchNo && x.IsDeleted == 0, ct);

    public void Add(HrmOffer entity) => _db.HrmOffers.Add(entity);
    public void Update(HrmOffer entity) => _db.HrmOffers.Update(entity);
}
