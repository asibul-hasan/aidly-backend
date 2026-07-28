using AidlyErp.Hrm.Application.Interfaces;
using AidlyErp.Hrm.Contracts;
using AidlyErp.Shared.Core.Dto;
using Microsoft.EntityFrameworkCore;

namespace AidlyErp.Hrm.Infrastructure;

/// <summary>HRM's side of <see cref="IHrmLookups"/>. Queries preserved verbatim from the callers.</summary>
internal sealed class HrmLookups : IHrmLookups
{
    private const short Deleted = 0;
    private const short Active = 1;

    private readonly IHrmDbContext _db;

    public HrmLookups(IHrmDbContext db) => _db = db;

    public async Task<List<DepartmentLookupDto>> DepartmentsAsync(long? branchNo,
                                                                  CancellationToken cancellationToken = default) =>
        await _db.HrmDepartments.AsNoTracking()
            .Where(d => d.IsDeleted == Deleted && (branchNo == null || d.BranchNo == branchNo))
            .OrderBy(d => d.DepartmentNo)
            .Select(d => new DepartmentLookupDto
            {
                DepartmentNo = d.DepartmentNo,
                DepartmentId = d.DepartmentId,
                DepartmentName = d.DepartmentName
            })
            .ToListAsync(cancellationToken);

    public async Task<List<DesignationLookupDto>> DesignationsAsync(long? branchNo,
                                                                    CancellationToken cancellationToken = default) =>
        await _db.HrmDesignations.AsNoTracking()
            .Where(d => d.IsDeleted == Deleted && (branchNo == null || d.BranchNo == branchNo))
            .OrderBy(d => d.DesignationNo)
            .Select(d => new DesignationLookupDto
            {
                DesignationNo = d.DesignationNo,
                DesignationId = d.DesignationId,
                DesignationName = d.DesignationName,
                DepartmentNo = d.DepartmentNo,
                GradeNo = d.GradeNo,
                GradeLevel = d.GradeLevel,
                MinSalary = d.MinSalary,
                MaxSalary = d.MaxSalary
            })
            .ToListAsync(cancellationToken);

    public async Task<List<GradeLookupDto>> GradesAsync(long? branchNo,
                                                        CancellationToken cancellationToken = default) =>
        await _db.HrmGrades.AsNoTracking()
            .Where(g => g.IsDeleted == Deleted && (branchNo == null || g.BranchNo == branchNo))
            .OrderBy(g => g.GradeNo)
            .Select(g => new GradeLookupDto
            {
                GradeNo = g.GradeNo,
                GradeId = g.GradeId,
                GradeName = g.GradeName,
                RankOrder = g.RankOrder,
                MinSalary = g.MinSalary,
                MaxSalary = g.MaxSalary
            })
            .ToListAsync(cancellationToken);

    public async Task<List<GradeStepLookupDto>> GradeStepsAsync(long? gradeNo,
                                                                CancellationToken cancellationToken = default)
    {
        var query = _db.HrmGradeSteps.AsNoTracking().Where(s => s.IsDeleted == Deleted);
        if (gradeNo.HasValue) query = query.Where(s => s.GradeNo == gradeNo.Value);

        return await query.OrderBy(s => s.GradeStepNo)
            .Select(s => new GradeStepLookupDto
            {
                GradeStepNo = s.GradeStepNo,
                GradeNo = s.GradeNo,
                Step = s.StepName,
                Amount = s.BasicSalary
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<List<EmployeeLookupDto>> EmployeesAsync(long? branchNo,
                                                              CancellationToken cancellationToken = default)
    {
        var employees = await _db.HrmEmployees.AsNoTracking()
            .Where(e => e.IsDeleted == Deleted && e.IsActive == Active
                        && (branchNo == null || e.BranchNo == branchNo))
            .OrderBy(e => e.EmployeeNo)
            .Select(e => new
            {
                e.EmployeeNo,
                e.EmployeeId,
                e.DepartmentNo,
                e.DesignationNo,
                e.BranchNo,
                e.FirstName,
                e.MiddleName,
                e.LastName
            })
            .ToListAsync(cancellationToken);

        // Department and designation names resolved in one batched query each.
        var deptNos = employees.Where(e => e.DepartmentNo > 0).Select(e => e.DepartmentNo).Distinct().ToList();
        var desigNos = employees.Where(e => e.DesignationNo > 0).Select(e => e.DesignationNo).Distinct().ToList();

        var depts = deptNos.Count == 0
            ? new Dictionary<long, string>()
            : await _db.HrmDepartments.AsNoTracking()
                .Where(d => deptNos.Contains(d.DepartmentNo) && d.IsDeleted == Deleted)
                .ToDictionaryAsync(d => d.DepartmentNo, d => d.DepartmentName, cancellationToken);

        var desigs = desigNos.Count == 0
            ? new Dictionary<long, string>()
            : await _db.HrmDesignations.AsNoTracking()
                .Where(d => desigNos.Contains(d.DesignationNo) && d.IsDeleted == Deleted)
                .ToDictionaryAsync(d => d.DesignationNo, d => d.DesignationName, cancellationToken);

        return employees.Select(e =>
        {
            var fullName = $"{e.FirstName}"
                           + (string.IsNullOrWhiteSpace(e.MiddleName) ? "" : " " + e.MiddleName)
                           + (string.IsNullOrWhiteSpace(e.LastName) ? "" : " " + e.LastName);
            fullName = fullName.Trim();

            var hasName = !string.IsNullOrWhiteSpace(fullName);

            return new EmployeeLookupDto
            {
                EmployeeNo = e.EmployeeNo,
                EmployeeId = e.EmployeeId,
                DepartmentNo = e.DepartmentNo,
                DesignationNo = e.DesignationNo,
                BranchNo = e.BranchNo,
                DepartmentName = depts.TryGetValue(e.DepartmentNo, out var dn) ? dn : null,
                DesignationName = desigs.TryGetValue(e.DesignationNo, out var gn) ? gn : null,
                // Falls back to the employee id when no name parts are set, as before.
                FullName = hasName ? fullName : e.EmployeeId,
                FullNameWithId = hasName ? $"{e.EmployeeId} - {fullName}" : e.EmployeeId
            };
        }).ToList();
    }
}
