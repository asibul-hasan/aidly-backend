using AidlyErp.Sys.Application.Dto;
using AidlyErp.Sys.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace AidlyErp.Api.Controllers.Sys;

/// <summary>
/// Dedicated controller for the SYS1103 Role Permission Matrix.
///
/// <code>
/// GET  /roles                   – roles of the active company
/// GET  /roles/{roleNo}/matrix   – every purchased form with this role's grants
/// POST /roles/{roleNo}/matrix   – save grants (evicts the RBAC permission cache)
/// </code>
/// </summary>
[ApiController]
[Route("api/v1/sys/forms/sys1103")]
public class Sys1103Controller : ApiControllerBase
{
    private readonly ISys1103Service _service;

    public Sys1103Controller(ISys1103Service service) => _service = service;

    [HttpGet("roles")]
    public async Task<IActionResult> GetRoles(CancellationToken cancellationToken) =>
        OkResponse(await _service.GetRolesAsync(cancellationToken));

    [HttpGet("roles/{roleNo:long}/matrix")]
    public async Task<IActionResult> GetMatrix(long roleNo, CancellationToken cancellationToken) =>
        OkResponse(await _service.GetMatrixAsync(roleNo, cancellationToken));

    [HttpPost("roles/{roleNo:long}/matrix")]
    public async Task<IActionResult> SaveMatrix(long roleNo, [FromBody] List<Sys1103PermissionRowDto>? rows,
                                                CancellationToken cancellationToken) =>
        OkResponse(await _service.SaveMatrixAsync(roleNo, rows, cancellationToken), "Permissions saved");
}
