using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AidlyErp.Shared.Core.Abstractions;
using AidlyErp.Shared.Contracts;
using AidlyErp.Sys.Contracts;
using AidlyErp.Sys.Application.Interfaces;
using AidlyErp.Hrm.Application.Interfaces;
using AidlyErp.Sal.Application.Interfaces;
using AidlyErp.Pur.Application.Interfaces;
using AidlyErp.Fin.Application.Interfaces;
using AidlyErp.Inv.Application.Interfaces;

namespace AidlyErp.Api.Controllers;

[Route("api/v1/common/lookups")]
public class CommonLookupController : ApiControllerBase
{
    // The Host is the composition root, so it may see every module. Each query still goes through
    // the owning module's context — there is no omniscient DbContext any more.
    private readonly ISysDbContext _sys;
    private readonly IHrmDbContext _hrm;
    private readonly IInvDbContext _inv;
    private readonly IFinDbContext _fin;
    private readonly IPurDbContext _pur;
    private readonly ISalDbContext _sal;

    public CommonLookupController(ISysDbContext sys, IHrmDbContext hrm, IInvDbContext inv,
                                  IFinDbContext fin, IPurDbContext pur, ISalDbContext sal)
    {
        _sys = sys;
        _hrm = hrm;
        _inv = inv;
        _fin = fin;
        _pur = pur;
        _sal = sal;
    }

    [HttpGet("employees")]
    public async Task<IActionResult> GetEmployees()
    {
        var data = await _hrm.HrmEmployees
            .AsNoTracking()
            .Select(x => new { id = x.EmployeeNo, code = x.EmployeeId, name = x.FirstName + " " + x.LastName, departmentNo = x.DepartmentNo, designationNo = x.DesignationNo })
            .ToListAsync();
        return OkResponse(data);
    }

    [HttpGet("departments")]
    public async Task<IActionResult> GetDepartments()
    {
        var data = await _hrm.HrmDepartments
            .AsNoTracking()
            .Select(x => new { id = x.DepartmentNo, code = x.DepartmentId, name = x.DepartmentName })
            .ToListAsync();
        return OkResponse(data);
    }

    [HttpGet("designations")]
    public async Task<IActionResult> GetDesignations([FromQuery] long? departmentNo)
    {
        var query = _hrm.HrmDesignations.AsNoTracking();
        if (departmentNo.HasValue)
        {
            query = query.Where(x => x.DepartmentNo == departmentNo.Value);
        }
        var data = await query.Select(x => new { id = x.DesignationNo, code = x.DesignationId, name = x.DesignationName, departmentNo = x.DepartmentNo }).ToListAsync();
        return OkResponse(data);
    }

    [HttpGet("grades")]
    public async Task<IActionResult> GetGrades()
    {
        var data = await _hrm.HrmGrades
            .AsNoTracking()
            .Select(x => new { id = x.GradeNo, code = x.GradeNo.ToString(), name = x.GradeName })
            .ToListAsync();
        return OkResponse(data);
    }

    [HttpGet("grades/{gradeNo}/steps")]
    public async Task<IActionResult> GetGradeSteps(long gradeNo)
    {
        var data = await _hrm.HrmGradeSteps
            .AsNoTracking()
            .Where(x => x.GradeNo == gradeNo)
            .Select(x => new { id = x.GradeStepNo, name = x.StepName, amount = x.BasicSalary })
            .ToListAsync();
        return OkResponse(data);
    }

    [HttpGet("branches")]
    public async Task<IActionResult> GetBranches()
    {
        var data = await _sys.Branches
            .AsNoTracking()
            .Select(x => new { id = x.BranchNo, code = x.BranchId, name = x.BranchName })
            .ToListAsync();
        return OkResponse(data);
    }

    [HttpGet("currencies")]
    public async Task<IActionResult> GetCurrencies()
    {
        var data = await _sys.Currencies
            .AsNoTracking()
            .Select(x => new { id = x.CurrencyNo, code = x.CurrencyCode, name = x.CurrencyName, symbol = x.CurrencySymbol })
            .ToListAsync();
        return OkResponse(data);
    }

    [HttpGet("warehouses")]
    public async Task<IActionResult> GetWarehouses()
    {
        var data = await _inv.InvWarehouses
            .AsNoTracking()
            .Select(x => new { id = x.WarehouseNo, code = x.WarehouseId, name = x.WarehouseName })
            .ToListAsync();
        return OkResponse(data);
    }

    [HttpGet("products")]
    public async Task<IActionResult> GetProducts()
    {
        var data = await _inv.InvProducts
            .AsNoTracking()
            .Select(x => new { id = x.ProductNo, code = x.ProductId, name = x.ProductName })
            .ToListAsync();
        return OkResponse(data);
    }

    [HttpGet("uoms")]
    public async Task<IActionResult> GetUoms()
    {
        var data = await _inv.InvUoms
            .AsNoTracking()
            .Select(x => new { id = x.UomNo, code = x.UomId, name = x.UomName })
            .ToListAsync();
        return OkResponse(data);
    }

    [HttpGet("accounts")]
    public async Task<IActionResult> GetAccounts()
    {
        var data = await _fin.FinAccounts
            .AsNoTracking()
            .Select(x => new { id = x.AccountNo, code = x.AccountCode, name = x.AccountName })
            .ToListAsync();
        return OkResponse(data);
    }

    [HttpGet("suppliers")]
    public async Task<IActionResult> GetSuppliers()
    {
        var data = await _pur.PurSuppliers
            .AsNoTracking()
            .Select(x => new { id = x.SupplierNo, code = x.SupplierId, name = x.SupplierName })
            .ToListAsync();
        return OkResponse(data);
    }

    [HttpGet("customers")]
    public async Task<IActionResult> GetCustomers()
    {
        var data = await _sal.SalCustomers
            .AsNoTracking()
            .Select(x => new { id = x.CustomerNo, code = x.CustomerId, name = x.CustomerName })
            .ToListAsync();
        return OkResponse(data);
    }
}
