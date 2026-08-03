using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using AidlyErp.Shared.Core.Response;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Sys.Application.Interfaces;
using AidlyErp.Hrm.Application.Interfaces;
using AidlyErp.Sal.Application.Interfaces;
using AidlyErp.Pur.Application.Interfaces;
using AidlyErp.Fin.Application.Interfaces;
using AidlyErp.Inv.Application.Interfaces;
namespace AidlyErp.Api.Controllers;
[Route("api/v1/common/lookups")]
public class CommonLookupController : ApiControllerBase, IActionFilter
{
    private readonly ISysDbContext _sys;
    private readonly IHrmDbContext _hrm;
    private readonly IInvDbContext _inv;
    private readonly IFinDbContext _fin;
    private readonly IPurDbContext _pur;
    private readonly ISalDbContext _sal;
    private readonly IMemoryCache _cache;
    private readonly ICompanyBranchContext _ctx;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Company from the auth context. Safe to dereference: <see cref="OnActionExecuting"/>
    /// rejects the request before any action runs when there is no tenant.
    /// </summary>
    private long TenantCompany => _ctx.CompanyNo!.Value;

    /// <summary>
    /// Every lookup here is company-scoped reference data, so a request without a tenant
    /// must FAIL, not fall back to unfiltered rows.
    ///
    /// <para>This is the guard that closes a cross-company leak: the queries carried a
    /// <c>companyNo == null || …</c> escape and the global query filter has the same
    /// escape, so an expired or missing token (context null) silently turned every lookup
    /// into "all companies". Access tokens live ~5 minutes, so that was reachable in normal
    /// use — the Branch dropdown listed other companies' branches. 401 is the correct
    /// answer; the SPA's auth interceptor refreshes the token and retries.</para>
    /// </summary>
    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (_ctx.CompanyNo == null)
        {
            context.Result = new ObjectResult(
                ApiResponse<object>.Error(StatusCodes.Status401Unauthorized,
                    "No active company context. Please sign in again."))
            { StatusCode = StatusCodes.Status401Unauthorized };
        }
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
    public CommonLookupController(ISysDbContext sys, IHrmDbContext hrm, IInvDbContext inv,
                                  IFinDbContext fin, IPurDbContext pur, ISalDbContext sal,
                                  IMemoryCache cache, ICompanyBranchContext ctx)
    {
        _sys = sys;
        _hrm = hrm;
        _inv = inv;
        _fin = fin;
        _pur = pur;
        _sal = sal;
        _cache = cache;
        _ctx = ctx;
    }
    [HttpGet("clear-cache")]
    [HttpPost("clear-cache")]
    public IActionResult ClearCache()
    {
        if (_cache is MemoryCache mc)
        {
            mc.Compact(1.0);
        }
        return OkResponse(new { message = "Memory cache cleared successfully." });
    }
    [HttpGet("sync-sequences")]
    [HttpPost("sync-sequences")]
    public async Task<IActionResult> SyncSequences()
    {
        try
        {
            var seq = await _hrm.Database.SqlQueryRaw<string>("SELECT pg_get_serial_sequence('hrm_employee', 'employee_no')").FirstOrDefaultAsync();
            var max = await _hrm.Database.SqlQueryRaw<long>("SELECT COALESCE(MAX(employee_no), 1) FROM hrm_employee").FirstOrDefaultAsync();
            
            if (!string.IsNullOrEmpty(seq))
            {
                await _hrm.Database.ExecuteSqlRawAsync("SELECT setval({0}, {1})", seq, max);
            }
            return OkResponse(new { message = $"HRM sequences synced. Sequence: {seq}, Max: {max}" });
        }
        catch (Exception ex)
        {
            return OkResponse(new { message = "Failed to sync sequences: " + ex.Message });
        }
    }
    [HttpGet("bundle")]
    public async Task<IActionResult> GetBundle()
    {
        var branchNo = _ctx.BranchNo;
        var companyNo = _ctx.CompanyNo;
        string cacheKey = $"lookup_bundle_{TenantCompany}_{branchNo ?? 0}";
        if (_cache.TryGetValue(cacheKey, out object? cachedBundle) && cachedBundle != null)
        {
            return OkResponse(cachedBundle);
        }
        try
        {
            var departments = await _hrm.HrmDepartments.AsNoTracking()
                .Where(x => x.IsDeleted == 0 && (branchNo == null || x.BranchNo == null || x.BranchNo == branchNo))
                .Select(x => new {
                    id = x.DepartmentNo,
                    department_no = x.DepartmentNo,
                    code = x.DepartmentId,
                    department_id = x.DepartmentId,
                    name = x.DepartmentName,
                    department_name = x.DepartmentName,
                    is_active = x.IsActive
                }).ToListAsync();
            var designations = await _hrm.HrmDesignations.AsNoTracking()
                .Where(x => x.IsDeleted == 0 && (branchNo == null || x.BranchNo == null || x.BranchNo == branchNo))
                .Select(x => new {
                    id = x.DesignationNo,
                    designation_no = x.DesignationNo,
                    code = x.DesignationId,
                    designation_id = x.DesignationId,
                    name = x.DesignationName,
                    designation_name = x.DesignationName,
                    department_no = x.DepartmentNo,
                    grade_no = x.GradeNo,
                    is_active = x.IsActive
                }).ToListAsync();
            var grades = await _hrm.HrmGrades.AsNoTracking()
                .Where(x => x.IsDeleted == 0 && (branchNo == null || x.BranchNo == null || x.BranchNo == branchNo))
                .Select(x => new {
                    id = x.GradeNo,
                    grade_no = x.GradeNo,
                    code = x.GradeNo.ToString(),
                    grade_id = x.GradeNo.ToString(),
                    name = x.GradeName,
                    grade_name = x.GradeName
                }).ToListAsync();
            var employees = await _hrm.HrmEmployees.AsNoTracking()
                .Where(x => x.IsDeleted == 0 && (branchNo == null || x.BranchNo == null || x.BranchNo == branchNo))
                .Select(x => new {
                    id = x.EmployeeNo,
                    employee_no = x.EmployeeNo,
                    code = x.EmployeeId,
                    employee_id = x.EmployeeId,
                    name = (x.FirstName + " " + (x.LastName ?? "")).Trim(),
                    first_name = x.FirstName,
                    last_name = x.LastName,
                    full_name = (x.FirstName + " " + (x.LastName ?? "")).Trim(),
                    full_name_with_id = x.EmployeeId + " - " + (x.FirstName + " " + (x.LastName ?? "")).Trim(),
                    employee_name_id = x.EmployeeId + ": " + (x.FirstName + " " + (x.LastName ?? "")).Trim(),
                    department_no = x.DepartmentNo,
                    designation_no = x.DesignationNo,
                    is_active = x.IsActive
                }).ToListAsync();
            // See GetBranches: Branch's tenant BranchNo is its own PK, so the global
            // filter would leave only the active branch. Company scope, explicitly.
            var branches = await _sys.Branches.AsNoTracking()
                .IgnoreQueryFilters()
                .Where(x => x.IsDeleted == 0 && x.CompanyNo == TenantCompany)
                .Select(x => new {
                    id = x.BranchNo,
                    branch_no = x.BranchNo,
                    code = x.BranchId,
                    branch_id = x.BranchId,
                    name = x.BranchName,
                    branch_name = x.BranchName
                }).ToListAsync();
            var bundle = new { departments, designations, grades, employees, branches };
            _cache.Set(cacheKey, bundle, CacheDuration);
            return OkResponse(bundle);
        }
        catch
        {
            return OkResponse(new { departments = new object[0], designations = new object[0], grades = new object[0], employees = new object[0], branches = new object[0] });
        }
    }
    [HttpGet("employees")]
    public async Task<IActionResult> GetEmployees()
    {
        var branchNo = _ctx.BranchNo;
        string cacheKey = $"lookup_employees_{TenantCompany}_{branchNo ?? 0}";
        if (_cache.TryGetValue(cacheKey, out object? cached) && cached != null)
        {
            return OkResponse(cached);
        }
        try
        {
            var data = await _hrm.HrmEmployees
                .AsNoTracking()
                .Where(x => x.IsDeleted == 0 && (branchNo == null || x.BranchNo == null || x.BranchNo == branchNo))
                .Select(x => new {
                    id = x.EmployeeNo,
                    employee_no = x.EmployeeNo,
                    code = x.EmployeeId,
                    employee_id = x.EmployeeId,
                    name = (x.FirstName + " " + (x.LastName ?? "")).Trim(),
                    first_name = x.FirstName,
                    last_name = x.LastName,
                    full_name = (x.FirstName + " " + (x.LastName ?? "")).Trim(),
                    full_name_with_id = x.EmployeeId + " - " + (x.FirstName + " " + (x.LastName ?? "")).Trim(),
                    employee_name_id = x.EmployeeId + ": " + (x.FirstName + " " + (x.LastName ?? "")).Trim(),
                    department_no = x.DepartmentNo,
                    designation_no = x.DesignationNo,
                    is_active = x.IsActive
                })
                .ToListAsync();
            _cache.Set(cacheKey, data, CacheDuration);
            return OkResponse(data);
        }
        catch
        {
            return OkResponse(new List<object>());
        }
    }
    [HttpGet("departments")]
    public async Task<IActionResult> GetDepartments()
    {
        var branchNo = _ctx.BranchNo;
        string cacheKey = $"lookup_departments_{TenantCompany}_{branchNo ?? 0}";
        if (_cache.TryGetValue(cacheKey, out object? cached) && cached != null)
        {
            return OkResponse(cached);
        }
        try
        {
            var data = await _hrm.HrmDepartments
                .AsNoTracking()
                .Where(x => x.IsDeleted == 0 && (branchNo == null || x.BranchNo == null || x.BranchNo == branchNo))
                .Select(x => new {
                    id = x.DepartmentNo,
                    department_no = x.DepartmentNo,
                    code = x.DepartmentId,
                    department_id = x.DepartmentId,
                    name = x.DepartmentName,
                    department_name = x.DepartmentName,
                    is_active = x.IsActive
                })
                .ToListAsync();
            _cache.Set(cacheKey, data, CacheDuration);
            return OkResponse(data);
        }
        catch
        {
            return OkResponse(new List<object>());
        }
    }
    [HttpGet("designations")]
    public async Task<IActionResult> GetDesignations([FromQuery] long? departmentNo)
    {
        var branchNo = _ctx.BranchNo;
        string cacheKey = $"lookup_designations_{TenantCompany}_{branchNo ?? 0}_{departmentNo ?? 0}";
        if (_cache.TryGetValue(cacheKey, out object? cached) && cached != null)
        {
            return OkResponse(cached);
        }
        try
        {
            var query = _hrm.HrmDesignations
                .AsNoTracking()
                .Where(x => x.IsDeleted == 0 && (branchNo == null || x.BranchNo == null || x.BranchNo == branchNo));
            if (departmentNo.HasValue && departmentNo.Value > 0)
            {
                query = query.Where(x => x.DepartmentNo == departmentNo.Value);
            }
            var data = await query
                .Select(x => new {
                    id = x.DesignationNo,
                    designation_no = x.DesignationNo,
                    code = x.DesignationId,
                    designation_id = x.DesignationId,
                    name = x.DesignationName,
                    designation_name = x.DesignationName,
                    department_no = x.DepartmentNo,
                    grade_no = x.GradeNo,
                    is_active = x.IsActive
                })
                .ToListAsync();
            _cache.Set(cacheKey, data, CacheDuration);
            return OkResponse(data);
        }
        catch
        {
            return OkResponse(new List<object>());
        }
    }
    [HttpGet("grades")]
    public async Task<IActionResult> GetGrades()
    {
        var branchNo = _ctx.BranchNo;
        string cacheKey = $"lookup_grades_{TenantCompany}_{branchNo ?? 0}";
        if (_cache.TryGetValue(cacheKey, out object? cached) && cached != null)
        {
            return OkResponse(cached);
        }
        try
        {
            var data = await _hrm.HrmGrades
                .AsNoTracking()
                .Where(x => x.IsDeleted == 0 && (branchNo == null || x.BranchNo == null || x.BranchNo == branchNo))
                .Select(x => new {
                    id = x.GradeNo,
                    grade_no = x.GradeNo,
                    code = x.GradeNo.ToString(),
                    grade_id = x.GradeNo.ToString(),
                    name = x.GradeName,
                    grade_name = x.GradeName
                })
                .ToListAsync();
            _cache.Set(cacheKey, data, CacheDuration);
            return OkResponse(data);
        }
        catch
        {
            return OkResponse(new List<object>());
        }
    }
    [HttpGet("grades/{gradeNo}/steps")]
    public async Task<IActionResult> GetGradeSteps(long gradeNo)
    {
        string cacheKey = $"lookup_gradesteps_{TenantCompany}_{gradeNo}";
        if (_cache.TryGetValue(cacheKey, out object? cached) && cached != null)
        {
            return OkResponse(cached);
        }
        try
        {
            var data = await _hrm.HrmGradeSteps
                .AsNoTracking()
                .Where(x => x.IsDeleted == 0 && x.GradeNo == gradeNo)
                .Select(x => new { id = x.GradeStepNo,  grade_step_no = x.GradeStepNo, name = x.StepName, amount = x.BasicSalary })
                .ToListAsync();
            _cache.Set(cacheKey, data, CacheDuration);
            return OkResponse(data);
        }
        catch
        {
            return OkResponse(new List<object>());
        }
    }
    [HttpGet("branches")]
    public async Task<IActionResult> GetBranches()
    {
        var companyNo = TenantCompany;
        string cacheKey = $"lookup_branches_{companyNo}";
        if (_cache.TryGetValue(cacheKey, out object? cached) && cached != null)
        {
            return OkResponse(cached);
        }
        try
        {
            // COMPANY-scoped only, and deliberately bypassing the global filter.
            // Branch maps IMultiTenantEntity.BranchNo to its own PK, so the global
            // tenant predicate would narrow this to the single active branch and the
            // branch selector could never offer another one. A branch picker must list
            // every branch of the caller's company — and nothing beyond it.
            var data = await _sys.Branches
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(x => x.IsDeleted == 0 && x.CompanyNo == companyNo)
                .Select(x => new {
                    id = x.BranchNo,
                    branch_no = x.BranchNo,
                    code = x.BranchId,
                    branch_id = x.BranchId,
                    name = x.BranchName,
                    branch_name = x.BranchName
                })
                .ToListAsync();
            _cache.Set(cacheKey, data, CacheDuration);
            return OkResponse(data);
        }
        catch
        {
            return OkResponse(new List<object>());
        }
    }
    [HttpGet("currencies")]
    public async Task<IActionResult> GetCurrencies()
    {
        string cacheKey = $"lookup_currencies_{TenantCompany}";
        if (_cache.TryGetValue(cacheKey, out object? cached) && cached != null)
        {
            return OkResponse(cached);
        }
        try
        {
            var data = await _sys.Currencies
                .AsNoTracking()
                .Where(x => x.IsDeleted == 0)
                .Select(x => new {
                    id = x.CurrencyNo,
                    currency_no = x.CurrencyNo,
                    code = x.CurrencyCode,
                    currency_code = x.CurrencyCode,
                    name = x.CurrencyName,
                    currency_name = x.CurrencyName,
                    symbol = x.CurrencySymbol,
                    currency_symbol = x.CurrencySymbol
                })
                .ToListAsync();
            _cache.Set(cacheKey, data, CacheDuration);
            return OkResponse(data);
        }
        catch
        {
            return OkResponse(new List<object>());
        }
    }
    [HttpGet("warehouses")]
    public async Task<IActionResult> GetWarehouses()
    {
        try
        {
            var data = await _inv.InvWarehouses.AsNoTracking().Where(x => x.IsDeleted == 0)
                .Select(x => new { id = x.WarehouseNo,  warehouse_no = x.WarehouseNo, code = x.WarehouseId, name = x.WarehouseName }).ToListAsync();
            return OkResponse(data);
        }
        catch { return OkResponse(new List<object>()); }
    }
    [HttpGet("products")]
    public async Task<IActionResult> GetProducts()
    {
        try
        {
            var data = await _inv.InvProducts.AsNoTracking().Where(x => x.IsDeleted == 0)
                .Select(x => new { id = x.ProductNo,  product_no = x.ProductNo, code = x.ProductId, name = x.ProductName }).ToListAsync();
            return OkResponse(data);
        }
        catch { return OkResponse(new List<object>()); }
    }
    [HttpGet("uoms")]
    public async Task<IActionResult> GetUoms()
    {
        try
        {
            var data = await _inv.InvUoms.AsNoTracking().Where(x => x.IsDeleted == 0)
                .Select(x => new { id = x.UomNo,  uom_no = x.UomNo, code = x.UomId, name = x.UomName }).ToListAsync();
            return OkResponse(data);
        }
        catch { return OkResponse(new List<object>()); }
    }
    [HttpGet("accounts")]
    public async Task<IActionResult> GetAccounts()
    {
        try
        {
            var data = await _fin.FinAccounts.AsNoTracking().Where(x => x.IsDeleted == 0)
                .Select(x => new { id = x.AccountNo,  account_no = x.AccountNo, code = x.AccountCode, name = x.AccountName }).ToListAsync();
            return OkResponse(data);
        }
        catch { return OkResponse(new List<object>()); }
    }
    [HttpGet("suppliers")]
    public async Task<IActionResult> GetSuppliers()
    {
        try
        {
            var data = await _pur.PurSuppliers.AsNoTracking().Where(x => x.IsDeleted == 0)
                .Select(x => new { id = x.SupplierNo,  supplier_no = x.SupplierNo, code = x.SupplierId, name = x.SupplierName }).ToListAsync();
            return OkResponse(data);
        }
        catch { return OkResponse(new List<object>()); }
    }
    [HttpGet("customers")]
    public async Task<IActionResult> GetCustomers()
    {
        try
        {
            var data = await _sal.SalCustomers.AsNoTracking().Where(x => x.IsDeleted == 0)
                .Select(x => new { id = x.CustomerNo,  customer_no = x.CustomerNo, code = x.CustomerId, name = x.CustomerName }).ToListAsync();
            return OkResponse(data);
        }
        catch { return OkResponse(new List<object>()); }
    }
}
