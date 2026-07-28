using AidlyErp.Sys.Application.Dto;
using AidlyErp.Sys.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace AidlyErp.Api.Controllers.Sys;

/// <summary>Generic company CRUD — <c>/api/v1/sys/companies</c>.</summary>
[ApiController]
[Route("api/v1/sys/companies")]
public class SysCompanyController : ApiControllerBase
{
    private readonly ICompanyService _service;

    public SysCompanyController(ICompanyService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken) =>
        OkResponse(await _service.GetListAsync(cancellationToken));

    [HttpGet("{companyNo:long}")]
    public async Task<IActionResult> GetDtl(long companyNo, CancellationToken cancellationToken) =>
        OkResponse(await _service.GetDtlAsync(companyNo, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] CompanyDto dto, CancellationToken cancellationToken) =>
        OkResponse(await _service.InsertAsync(dto, cancellationToken), "Company created successfully");

    [HttpPut("{companyNo:long}")]
    public async Task<IActionResult> Update(long companyNo, [FromBody] CompanyDto dto,
                                            CancellationToken cancellationToken) =>
        OkResponse(await _service.UpdateAsync(companyNo, dto, cancellationToken), "Company updated successfully");

    [HttpDelete("{companyNo:long}")]
    public async Task<IActionResult> Delete(long companyNo, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(companyNo, cancellationToken);
        return OkResponse<object?>(null, "Company deleted successfully");
    }
}

/// <summary>Generic branch CRUD — <c>/api/v1/sys/branches</c>.</summary>
[ApiController]
[Route("api/v1/sys/branches")]
public class SysBranchController : ApiControllerBase
{
    private readonly IBranchService _service;

    public SysBranchController(IBranchService service) => _service = service;

    /// <summary>Without <c>company_no</c> this returns only the caller's own branches.</summary>
    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery(Name = "company_no")] long? companyNo,
                                             CancellationToken cancellationToken) =>
        OkResponse(companyNo == null
            ? await _service.GetListAsync(cancellationToken)
            : await _service.GetListByCompanyAsync(companyNo.Value, cancellationToken));

    [HttpGet("{branchNo:long}")]
    public async Task<IActionResult> GetDtl(long branchNo, CancellationToken cancellationToken) =>
        OkResponse(await _service.GetDtlAsync(branchNo, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] BranchDto dto, CancellationToken cancellationToken) =>
        OkResponse(await _service.InsertAsync(dto, cancellationToken), "Branch created successfully");

    [HttpPut("{branchNo:long}")]
    public async Task<IActionResult> Update(long branchNo, [FromBody] BranchDto dto,
                                            CancellationToken cancellationToken) =>
        OkResponse(await _service.UpdateAsync(branchNo, dto, cancellationToken), "Branch updated successfully");

    [HttpDelete("{branchNo:long}")]
    public async Task<IActionResult> Delete(long branchNo, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(branchNo, cancellationToken);
        return OkResponse<object?>(null, "Branch deleted successfully");
    }
}
