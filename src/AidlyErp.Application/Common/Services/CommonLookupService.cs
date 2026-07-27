using Microsoft.EntityFrameworkCore;
using AidlyErp.Application.Common.Dto;
using AidlyErp.Application.Common.Interfaces;
using AidlyErp.Application.Common.Security;

namespace AidlyErp.Application.Common.Services;

/// <summary>
/// Cross-cutting read-only lookup service — port of Java <c>common.service.CommonLookupService</c>.
/// Provides lean DTO rows for dropdown pickers, properly tenant-scoped.
/// </summary>
public interface ICommonLookupService
{
    Task<List<BranchLookupDto>> BranchesAsync(CancellationToken ct = default);
    Task<List<CurrencyLookupDto>> CurrenciesAsync(CancellationToken ct = default);
    Task<List<DepartmentLookupDto>> DepartmentsAsync(CancellationToken ct = default);
    Task<List<DesignationLookupDto>> DesignationsAsync(CancellationToken ct = default);
    Task<List<EmployeeLookupDto>> EmployeesAsync(CancellationToken ct = default);
    Task<List<GradeLookupDto>> GradesAsync(CancellationToken ct = default);
    Task<List<GradeStepLookupDto>> GradeStepsAsync(long? gradeNo, CancellationToken ct = default);
    Task<List<LookupDto>> WarehousesAsync(CancellationToken ct = default);
}

public class CommonLookupService : ICommonLookupService
{
    private readonly IApplicationDbContext _db;
    private readonly ICompanyBranchContext _ctx;

    public CommonLookupService(IApplicationDbContext db, ICompanyBranchContext ctx)
    {
        _db = db;
        _ctx = ctx;
    }

    public async Task<List<BranchLookupDto>> BranchesAsync(CancellationToken ct = default)
    {
        var companyNo = _ctx.CompanyNo;
        return await _db.Branches.AsNoTracking()
            .Where(b => b.IsDeleted == 0 && (companyNo == null || b.CompanyNo == companyNo))
            .OrderBy(b => b.BranchNo)
            .Select(b => new BranchLookupDto
            {
                BranchNo = b.BranchNo,
                BranchId = b.BranchId,
                BranchName = b.BranchName
            })
            .ToListAsync(ct);
    }

    public async Task<List<CurrencyLookupDto>> CurrenciesAsync(CancellationToken ct = default)
    {
        var companyNo = _ctx.CompanyNo;
        var branchNo = _ctx.BranchNo;
        return await _db.Currencies.AsNoTracking()
            .Where(c => c.IsDeleted == 0 && (companyNo == null || c.CompanyNo == companyNo))
            .OrderBy(c => c.CurrencyNo)
            .Select(c => new CurrencyLookupDto
            {
                CurrencyNo = c.CurrencyNo,
                CurrencyCode = c.CurrencyCode,
                CurrencyName = c.CurrencyName,
                Symbol = c.CurrencySymbol,
                DecimalPlaces = c.DecimalPlaces,
                ExchangeRate = c.ExchangeRate,
                IsBaseCurrency = c.IsBaseCurrency
            })
            .ToListAsync(ct);
    }

    public async Task<List<DepartmentLookupDto>> DepartmentsAsync(CancellationToken ct = default)
    {
        var branchNo = _ctx.BranchNo;
        return await _db.HrmDepartments.AsNoTracking()
            .Where(d => d.IsDeleted == 0 && (branchNo == null || d.BranchNo == branchNo))
            .OrderBy(d => d.DepartmentNo)
            .Select(d => new DepartmentLookupDto
            {
                DepartmentNo = d.DepartmentNo,
                DepartmentId = d.DepartmentId,
                DepartmentName = d.DepartmentName
            })
            .ToListAsync(ct);
    }

    public async Task<List<DesignationLookupDto>> DesignationsAsync(CancellationToken ct = default)
    {
        var branchNo = _ctx.BranchNo;
        return await _db.HrmDesignations.AsNoTracking()
            .Where(d => d.IsDeleted == 0 && (branchNo == null || d.BranchNo == branchNo))
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
            .ToListAsync(ct);
    }

    public async Task<List<EmployeeLookupDto>> EmployeesAsync(CancellationToken ct = default)
    {
        var branchNo = _ctx.BranchNo;
        var employees = await _db.HrmEmployees.AsNoTracking()
            .Where(e => e.IsDeleted == 0 && e.IsActive == 1 && (branchNo == null || e.BranchNo == branchNo))
            .OrderBy(e => e.EmployeeNo)
            .Select(e => new EmployeeLookupDto
            {
                EmployeeNo = e.EmployeeNo,
                EmployeeId = e.EmployeeId,
                DepartmentNo = e.DepartmentNo,
                DesignationNo = e.DesignationNo,
                BranchNo = e.BranchNo
            })
            .ToListAsync(ct);

        // Enrich with department/designation names and full name.
        var deptNos = employees.Where(e => e.DepartmentNo > 0).Select(e => e.DepartmentNo!.Value).Distinct().ToList();
        var desigNos = employees.Where(e => e.DesignationNo > 0).Select(e => e.DesignationNo!.Value).Distinct().ToList();

        var depts = await _db.HrmDepartments.AsNoTracking()
            .Where(d => deptNos.Contains(d.DepartmentNo) && d.IsDeleted == 0)
            .ToDictionaryAsync(d => d.DepartmentNo, d => d.DepartmentName, ct);

        var desigs = await _db.HrmDesignations.AsNoTracking()
            .Where(d => desigNos.Contains(d.DesignationNo) && d.IsDeleted == 0)
            .ToDictionaryAsync(d => d.DesignationNo, d => d.DesignationName, ct);

        // Also get user_no mapping.
        var empNos = employees.Select(e => e.EmployeeNo).ToList();
        var userMap = await _db.Users.AsNoTracking()
            .Where(u => empNos.Contains(u.EmployeeNo) && u.IsDeleted == 0)
            .ToDictionaryAsync(u => u.EmployeeNo, u => u.UserNo, ct);

        foreach (var emp in employees)
        {
            if (emp.DepartmentNo.HasValue && depts.TryGetValue(emp.DepartmentNo.Value, out var deptName))
                emp.DepartmentName = deptName;
            if (emp.DesignationNo.HasValue && desigs.TryGetValue(emp.DesignationNo.Value, out var desigName))
                emp.DesignationName = desigName;
            if (userMap.TryGetValue(emp.EmployeeNo, out var userNo))
                emp.UserNo = userNo;

            // Build full name from the entity (we need to load first/middle/last separately).
            emp.FullName = emp.EmployeeId; // Fallback — will be enriched below.
            emp.FullNameWithId = emp.EmployeeId;
        }

        // Load name parts for full name construction.
        var empEntities = await _db.HrmEmployees.AsNoTracking()
            .Where(e => empNos.Contains(e.EmployeeNo) && e.IsDeleted == 0)
            .Select(e => new { e.EmployeeNo, e.FirstName, e.MiddleName, e.LastName, e.EmployeeId })
            .ToListAsync(ct);

        var nameMap = empEntities.ToDictionary(
            e => e.EmployeeNo,
            e => $"{e.FirstName}{(string.IsNullOrWhiteSpace(e.MiddleName) ? "" : " " + e.MiddleName)}{(string.IsNullOrWhiteSpace(e.LastName) ? "" : " " + e.LastName)}".Trim());

        foreach (var emp in employees)
        {
            if (nameMap.TryGetValue(emp.EmployeeNo, out var fullName))
            {
                emp.FullName = fullName;
                emp.FullNameWithId = $"{emp.EmployeeId} - {fullName}";
            }
        }

        return employees;
    }

    public async Task<List<GradeLookupDto>> GradesAsync(CancellationToken ct = default)
    {
        var branchNo = _ctx.BranchNo;
        return await _db.HrmGrades.AsNoTracking()
            .Where(g => g.IsDeleted == 0 && (branchNo == null || g.BranchNo == branchNo))
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
            .ToListAsync(ct);
    }

    public async Task<List<GradeStepLookupDto>> GradeStepsAsync(long? gradeNo, CancellationToken ct = default)
    {
        var query = _db.HrmGradeSteps.AsNoTracking().Where(s => s.IsDeleted == 0);
        if (gradeNo.HasValue) query = query.Where(s => s.GradeNo == gradeNo.Value);
        return await query.OrderBy(s => s.GradeStepNo)
            .Select(s => new GradeStepLookupDto
            {
                GradeStepNo = s.GradeStepNo,
                GradeNo = s.GradeNo,
                Step = s.StepName,
                Amount = s.BasicSalary
            })
            .ToListAsync(ct);
    }

    public async Task<List<LookupDto>> WarehousesAsync(CancellationToken ct = default)
    {
        var branchNo = _ctx.BranchNo;
        return await _db.InvWarehouses.AsNoTracking()
            .Where(w => w.IsDeleted == 0 && (branchNo == null || w.BranchNo == branchNo))
            .OrderBy(w => w.WarehouseNo)
            .Select(w => new LookupDto
            {
                No = w.WarehouseNo,
                Code = w.WarehouseCode,
                Name = w.WarehouseName
            })
            .ToListAsync(ct);
    }
}
