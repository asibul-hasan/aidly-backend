using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AidlyErp.Application.Common.Interfaces;

namespace AidlyErp.Api.Controllers;

[Route("api/v1/common/lookups")]
public class CommonLookupController : ApiControllerBase
{
    private readonly IApplicationDbContext _db;

    public CommonLookupController(IApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet("employees")]
    public async Task<IActionResult> GetEmployees()
    {
        var data = await _db.HrmEmployees
            .AsNoTracking()
            .Select(x => new { id = x.EmployeeNo, code = x.EmployeeId, name = x.FirstName + " " + x.LastName, departmentNo = x.DepartmentNo, designationNo = x.DesignationNo })
            .ToListAsync();
        return OkResponse(data);
    }

    [HttpGet("departments")]
    public async Task<IActionResult> GetDepartments()
    {
        var data = await _db.HrmDepartments
            .AsNoTracking()
            .Select(x => new { id = x.DepartmentNo, code = x.DepartmentId, name = x.DepartmentName })
            .ToListAsync();
        return OkResponse(data);
    }

    [HttpGet("designations")]
    public async Task<IActionResult> GetDesignations([FromQuery] long? departmentNo)
    {
        var query = _db.HrmDesignations.AsNoTracking();
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
        var data = await _db.HrmGrades
            .AsNoTracking()
            .Select(x => new { id = x.GradeNo, code = x.GradeNo.ToString(), name = x.GradeName })
            .ToListAsync();
        return OkResponse(data);
    }

    [HttpGet("grades/{gradeNo}/steps")]
    public async Task<IActionResult> GetGradeSteps(long gradeNo)
    {
        var data = await _db.HrmGradeSteps
            .AsNoTracking()
            .Where(x => x.GradeNo == gradeNo)
            .Select(x => new { id = x.GradeStepNo, name = x.StepName, amount = x.BasicSalary })
            .ToListAsync();
        return OkResponse(data);
    }

    [HttpGet("branches")]
    public async Task<IActionResult> GetBranches()
    {
        var data = await _db.Branches
            .AsNoTracking()
            .Select(x => new { id = x.BranchNo, code = x.BranchId, name = x.BranchName })
            .ToListAsync();
        return OkResponse(data);
    }

    [HttpGet("currencies")]
    public async Task<IActionResult> GetCurrencies()
    {
        var data = await _db.Currencies
            .AsNoTracking()
            .Select(x => new { id = x.CurrencyNo, code = x.CurrencyCode, name = x.CurrencyName, symbol = x.CurrencySymbol })
            .ToListAsync();
        return OkResponse(data);
    }

    [HttpGet("warehouses")]
    public async Task<IActionResult> GetWarehouses()
    {
        var data = await _db.InvWarehouses
            .AsNoTracking()
            .Select(x => new { id = x.WarehouseNo, code = x.WarehouseCode, name = x.WarehouseName })
            .ToListAsync();
        return OkResponse(data);
    }

    [HttpGet("products")]
    public async Task<IActionResult> GetProducts()
    {
        var data = await _db.InvProducts
            .AsNoTracking()
            .Select(x => new { id = x.ProductNo, code = x.ProductCode, name = x.ProductName })
            .ToListAsync();
        return OkResponse(data);
    }

    [HttpGet("uoms")]
    public async Task<IActionResult> GetUoms()
    {
        var data = await _db.InvUoms
            .AsNoTracking()
            .Select(x => new { id = x.UomNo, code = x.UomCode, name = x.UomName })
            .ToListAsync();
        return OkResponse(data);
    }

    [HttpGet("accounts")]
    public async Task<IActionResult> GetAccounts()
    {
        var data = await _db.FinAccounts
            .AsNoTracking()
            .Select(x => new { id = x.AccountNo, code = x.AccountCode, name = x.AccountName })
            .ToListAsync();
        return OkResponse(data);
    }

    [HttpGet("suppliers")]
    public async Task<IActionResult> GetSuppliers()
    {
        var data = await _db.PurSuppliers
            .AsNoTracking()
            .Select(x => new { id = x.SupplierNo, code = x.SupplierCode, name = x.SupplierName })
            .ToListAsync();
        return OkResponse(data);
    }

    [HttpGet("customers")]
    public async Task<IActionResult> GetCustomers()
    {
        var data = await _db.SalCustomers
            .AsNoTracking()
            .Select(x => new { id = x.CustomerNo, code = x.CustomerCode, name = x.CustomerName })
            .ToListAsync();
        return OkResponse(data);
    }
}
