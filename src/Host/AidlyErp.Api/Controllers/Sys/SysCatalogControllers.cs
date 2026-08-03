using AidlyErp.Sys.Application.Dto;
using AidlyErp.Sys.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace AidlyErp.Api.Controllers.Sys;

/// <summary>Generic module CRUD — <c>/api/v1/sys/modules</c>.</summary>
[ApiController]
[Route("api/v1/sys/modules")]
public class SysModuleController : ApiControllerBase
{
    private readonly ISysModuleService _service;

    public SysModuleController(ISysModuleService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken) =>
        OkResponse(await _service.GetListAsync(cancellationToken));

    [HttpGet("{moduleNo:long}")]
    public async Task<IActionResult> GetDtl(long moduleNo, CancellationToken cancellationToken) =>
        OkResponse(await _service.GetDtlAsync(moduleNo, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] SysModuleDto dto, CancellationToken cancellationToken) =>
        OkResponse(await _service.InsertAsync(dto, cancellationToken), "Module created successfully");

    [HttpPut("{moduleNo:long}")]
    public async Task<IActionResult> Update(long moduleNo, [FromBody] SysModuleDto dto,
                                            CancellationToken cancellationToken) =>
        OkResponse(await _service.UpdateAsync(moduleNo, dto, cancellationToken), "Module updated successfully");

    [HttpDelete("{moduleNo:long}")]
    public async Task<IActionResult> Delete(long moduleNo, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(moduleNo, cancellationToken);
        return OkResponse<object?>(null, "Module deleted successfully");
    }
}

/// <summary>Generic submodule CRUD — <c>/api/v1/sys/submodules</c>.</summary>
[ApiController]
[Route("api/v1/sys/submodules")]
public class SysSubmoduleController : ApiControllerBase
{
    private readonly ISysSubmoduleService _service;

    public SysSubmoduleController(ISysSubmoduleService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery(Name = "module_no")] long? moduleNo,
                                             CancellationToken cancellationToken) =>
        OkResponse(moduleNo == null
            ? await _service.GetListAsync(cancellationToken)
            : await _service.GetListByModuleAsync(moduleNo.Value, cancellationToken));

    [HttpGet("{submoduleNo:long}")]
    public async Task<IActionResult> GetDtl(long submoduleNo, CancellationToken cancellationToken) =>
        OkResponse(await _service.GetDtlAsync(submoduleNo, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] SysSubmoduleDto dto, CancellationToken cancellationToken) =>
        OkResponse(await _service.InsertAsync(dto, cancellationToken), "Submodule created successfully");

    [HttpPut("{submoduleNo:long}")]
    public async Task<IActionResult> Update(long submoduleNo, [FromBody] SysSubmoduleDto dto,
                                            CancellationToken cancellationToken) =>
        OkResponse(await _service.UpdateAsync(submoduleNo, dto, cancellationToken), "Submodule updated successfully");

    [HttpDelete("{submoduleNo:long}")]
    public async Task<IActionResult> Delete(long submoduleNo, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(submoduleNo, cancellationToken);
        return OkResponse<object?>(null, "Submodule deleted successfully");
    }
}

/// <summary>Generic menu CRUD — <c>/api/v1/sys/menus</c>.</summary>
[ApiController]
[Route("api/v1/sys/menus")]
public class MenuController : ApiControllerBase
{
    private readonly IMenuService _service;

    public MenuController(IMenuService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery(Name = "submodule_no")] long? submoduleNo,
                                             CancellationToken cancellationToken) =>
        OkResponse(submoduleNo == null
            ? await _service.GetListAsync(cancellationToken)
            : await _service.GetListBySubmoduleAsync(submoduleNo.Value, cancellationToken));

    [HttpGet("{menuNo:long}")]
    public async Task<IActionResult> GetDtl(long menuNo, CancellationToken cancellationToken) =>
        OkResponse(await _service.GetDtlAsync(menuNo, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] MenuDto dto, CancellationToken cancellationToken) =>
        OkResponse(await _service.InsertAsync(dto, cancellationToken), "Menu created successfully");

    [HttpPut("{menuNo:long}")]
    public async Task<IActionResult> Update(long menuNo, [FromBody] MenuDto dto,
                                            CancellationToken cancellationToken) =>
        OkResponse(await _service.UpdateAsync(menuNo, dto, cancellationToken), "Menu updated successfully");

    [HttpDelete("{menuNo:long}")]
    public async Task<IActionResult> Delete(long menuNo, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(menuNo, cancellationToken);
        return OkResponse<object?>(null, "Menu deleted successfully");
    }
}
