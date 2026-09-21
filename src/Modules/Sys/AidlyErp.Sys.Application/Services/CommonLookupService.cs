using AidlyErp.Inv.Contracts;
using Microsoft.EntityFrameworkCore;
using AidlyErp.Shared.Core.Dto;
using AidlyErp.Shared.Core.Abstractions;
using AidlyErp.Shared.Contracts;
using AidlyErp.Sys.Contracts;
using AidlyErp.Sys.Application.Interfaces;
using AidlyErp.Hrm.Contracts;
using AidlyErp.Shared.Core.Security;

namespace AidlyErp.Sys.Application.Services;

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
    private readonly ISysDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    private readonly IHrmLookups _hrm;
    private readonly IInvCatalog _inv;

    public CommonLookupService(ISysDbContext db, ICompanyBranchContext ctx, IHrmLookups hrm, IInvCatalog inv)
    {
        _db = db;
        _ctx = ctx;
        _hrm = hrm;
        _inv = inv;
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

    public Task<List<DepartmentLookupDto>> DepartmentsAsync(CancellationToken ct = default) =>
        _hrm.DepartmentsAsync(_ctx.BranchNo, ct);

    public Task<List<DesignationLookupDto>> DesignationsAsync(CancellationToken ct = default) =>
        _hrm.DesignationsAsync(_ctx.BranchNo, ct);

    public async Task<List<EmployeeLookupDto>> EmployeesAsync(CancellationToken ct = default)
    {
        // HRM owns the employee rows (including department/designation names and the composed full
        // name); SYS only adds the user mapping, which is its own data.
        var employees = await _hrm.EmployeesAsync(_ctx.BranchNo, ct);

        var empNos = employees.Select(e => e.EmployeeNo).ToList();
        var userMap = await _db.Users.AsNoTracking()
            .Where(u => u.EmployeeNo != null && empNos.Contains(u.EmployeeNo.Value) && u.IsDeleted == 0)
            .ToDictionaryAsync(u => u.EmployeeNo, u => u.UserNo, ct);

        foreach (var emp in employees)
        {
            if (userMap.TryGetValue(emp.EmployeeNo, out var userNo)) emp.UserNo = userNo;
        }

        return employees;
    }

    public Task<List<GradeLookupDto>> GradesAsync(CancellationToken ct = default) =>
        _hrm.GradesAsync(_ctx.BranchNo, ct);

    public Task<List<GradeStepLookupDto>> GradeStepsAsync(long? gradeNo, CancellationToken ct = default) =>
        _hrm.GradeStepsAsync(gradeNo, ct);

    public async Task<List<LookupDto>> WarehousesAsync(CancellationToken ct = default)
    {
        var warehouses = await _inv.ListWarehousesAsync(branchNo: _ctx.BranchNo, cancellationToken: ct);

        return warehouses
            .Select(w => new LookupDto { No = w.WarehouseNo, Code = w.WarehouseId, Name = w.WarehouseName })
            .ToList();
    }
}
