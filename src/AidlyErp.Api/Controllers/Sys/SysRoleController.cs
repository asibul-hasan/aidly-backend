using AidlyErp.Application.Sys.Dto;
using AidlyErp.Application.Sys.Services;
using Microsoft.AspNetCore.Mvc;

namespace AidlyErp.Api.Controllers.Sys;

/// <summary>
/// Generic role CRUD — <c>/api/v1/sys/roles</c>.
///
/// <code>
/// GET    /                  – roles of the active company, with their branch publication
/// GET    /lookup            – compact options for dropdowns
/// GET    /{roleNo}          – detail
/// POST   /                  – create
/// PUT    /{roleNo}          – update (system roles rejected)
/// DELETE /{roleNo}          – soft-delete (system roles rejected)
/// </code>
/// </summary>
[ApiController]
[Route("api/v1/sys/roles")]
public class SysRoleController : ApiControllerBase
{
    private readonly IRoleService _service;

    public SysRoleController(IRoleService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken) =>
        OkResponse(await _service.GetListAsync(cancellationToken));

    [HttpGet("lookup")]
    public async Task<IActionResult> GetLookupList(CancellationToken cancellationToken) =>
        OkResponse(await _service.GetRoleLookupListAsync(cancellationToken));

    [HttpGet("{roleNo:long}")]
    public async Task<IActionResult> GetDtl(long roleNo, CancellationToken cancellationToken) =>
        OkResponse(await _service.GetDtlAsync(roleNo, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] RoleDto dto, CancellationToken cancellationToken) =>
        OkResponse(await _service.InsertAsync(dto, cancellationToken), "Role created successfully");

    [HttpPut("{roleNo:long}")]
    public async Task<IActionResult> Update(long roleNo, [FromBody] RoleDto dto,
                                            CancellationToken cancellationToken) =>
        OkResponse(await _service.UpdateAsync(roleNo, dto, cancellationToken), "Role updated successfully");

    [HttpDelete("{roleNo:long}")]
    public async Task<IActionResult> Delete(long roleNo, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(roleNo, cancellationToken);
        return OkResponse<object?>(null, "Role deleted successfully");
    }
}
