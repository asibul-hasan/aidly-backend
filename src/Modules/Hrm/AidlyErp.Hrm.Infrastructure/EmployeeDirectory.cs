using AidlyErp.Hrm.Application.Interfaces;
using AidlyErp.Hrm.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AidlyErp.Hrm.Infrastructure;

/// <summary>
/// HRM's side of <see cref="IEmployeeDirectory"/>. Lives in Infrastructure because it owns the
/// queries; consumers only ever see the interface from <c>Hrm.Contracts</c>.
/// </summary>
internal sealed class EmployeeDirectory : IEmployeeDirectory
{
    private const short Deleted = 0;

    private readonly IHrmDbContext _db;

    public EmployeeDirectory(IHrmDbContext db) => _db = db;

    /// <summary>
    /// Department and designation names are resolved by left join in one round trip, matching the
    /// Java <c>findAllWithFilters</c> query.
    /// </summary>
    public async Task<IReadOnlyList<EmployeeInfo>> ListAsync(short? isActive,
                                                             CancellationToken cancellationToken = default) =>
        await (from e in _db.HrmEmployees.AsNoTracking()
               where e.IsDeleted == Deleted && (isActive == null || e.IsActive == isActive)
               join d in _db.HrmDepartments.AsNoTracking() on e.DepartmentNo equals d.DepartmentNo into depts
               from d in depts.DefaultIfEmpty()
               join g in _db.HrmDesignations.AsNoTracking() on e.DesignationNo equals g.DesignationNo into desigs
               from g in desigs.DefaultIfEmpty()
               orderby e.EmployeeId
               select new EmployeeInfo
               {
                   EmployeeNo = e.EmployeeNo,
                   EmployeeId = e.EmployeeId,
                   FirstName = e.FirstName,
                   MiddleName = e.MiddleName,
                   LastName = e.LastName,
                   DepartmentNo = e.DepartmentNo,
                   DepartmentName = d == null ? null : d.DepartmentName,
                   DesignationNo = e.DesignationNo,
                   DesignationName = g == null ? null : g.DesignationName,
                   IsActive = e.IsActive,
                   IsCreateUser = e.IsCreateUser,
                   BranchNo = e.BranchNo,
                   JoiningDate = e.JoiningDate,
                   OfficialEmail = e.OfficialEmail,
                   MobileNumber = e.MobileNumber,
               })
            .ToListAsync(cancellationToken);

    public async Task<EmployeeInfo?> FindAsync(long employeeNo, CancellationToken cancellationToken = default)
    {
        var emp = await _db.HrmEmployees
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.EmployeeNo == employeeNo && e.IsDeleted == Deleted, cancellationToken);

        if (emp == null) return null;

        // Direct indexed PK lookups — never scan the employee table just to read two names.
        var deptName = emp.DepartmentNo == null
            ? null
            : await _db.HrmDepartments.AsNoTracking()
                .Where(d => d.DepartmentNo == emp.DepartmentNo && d.IsDeleted == Deleted)
                .Select(d => d.DepartmentName)
                .FirstOrDefaultAsync(cancellationToken);

        var desigName = emp.DesignationNo == null
            ? null
            : await _db.HrmDesignations.AsNoTracking()
                .Where(g => g.DesignationNo == emp.DesignationNo && g.IsDeleted == Deleted)
                .Select(g => g.DesignationName)
                .FirstOrDefaultAsync(cancellationToken);

        return new EmployeeInfo
        {
            EmployeeNo = emp.EmployeeNo,
            EmployeeId = emp.EmployeeId,
            FirstName = emp.FirstName,
            MiddleName = emp.MiddleName,
            LastName = emp.LastName,
            DepartmentNo = emp.DepartmentNo,
            DepartmentName = deptName,
            DesignationNo = emp.DesignationNo,
            DesignationName = desigName,
            IsActive = emp.IsActive,
            IsCreateUser = emp.IsCreateUser,
            BranchNo = emp.BranchNo,
            JoiningDate = emp.JoiningDate,
            OfficialEmail = emp.OfficialEmail,
            MobileNumber = emp.MobileNumber,
        };
    }

    public async Task<IReadOnlyDictionary<long, string>> GetNamesAsync(IReadOnlyCollection<long> employeeNos,
                                                                       CancellationToken cancellationToken = default)
    {
        if (employeeNos.Count == 0) return new Dictionary<long, string>();

        return await _db.HrmEmployees
            .AsNoTracking()
            .Where(e => employeeNos.Contains(e.EmployeeNo) && e.IsDeleted == Deleted)
            .Select(e => new { e.EmployeeNo, e.FirstName, e.LastName })
            .ToDictionaryAsync(
                e => e.EmployeeNo,
                e => (e.FirstName + " " + (e.LastName ?? string.Empty)).Trim(),
                cancellationToken);
    }

    public async Task MarkHasUserAsync(long employeeNo, CancellationToken cancellationToken = default)
    {
        var emp = await _db.HrmEmployees
            .FirstOrDefaultAsync(e => e.EmployeeNo == employeeNo && e.IsDeleted == Deleted, cancellationToken);

        if (emp == null || emp.IsCreateUser == 1) return;

        emp.IsCreateUser = 1;
        await _db.SaveChangesAsync(cancellationToken);
    }
}
