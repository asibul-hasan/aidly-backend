using AidlyErp.Application.Sys.Dto;
using AidlyErp.Application.Sys.Services;
using Microsoft.AspNetCore.Mvc;

namespace AidlyErp.Api.Controllers.Sys;

/// <summary>
/// Dedicated controller for the SYS1104 User-Branch Mapping form.
///
/// <code>
/// GET  /users                        – users of the active company
/// GET  /branches                     – branches of the active company
/// GET  /roles                        – roles, with the branches each is published to
/// GET  /users/{userNo}/mappings      – the user's branch/role memberships
/// POST /users/{userNo}/mappings      – replace the user's memberships
/// POST /mappings/bulk                – apply the same rows to many users
/// </code>
/// </summary>
[ApiController]
[Route("api/v1/sys/forms/sys1104")]
public class Sys1104Controller : ApiControllerBase
{
    private readonly ISys1104Service _service;

    public Sys1104Controller(ISys1104Service service) => _service = service;

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers(CancellationToken cancellationToken) =>
        OkResponse(await _service.GetUsersAsync(cancellationToken));

    [HttpGet("branches")]
    public async Task<IActionResult> GetBranches(CancellationToken cancellationToken) =>
        OkResponse(await _service.GetBranchesAsync(cancellationToken));

    [HttpGet("roles")]
    public async Task<IActionResult> GetRoles(CancellationToken cancellationToken) =>
        OkResponse(await _service.GetRolesAsync(cancellationToken));

    [HttpGet("users/{userNo:long}/mappings")]
    public async Task<IActionResult> GetMappings(long userNo, CancellationToken cancellationToken) =>
        OkResponse(await _service.GetMappingsAsync(userNo, cancellationToken));

    [HttpPost("users/{userNo:long}/mappings")]
    public async Task<IActionResult> SaveMappings(long userNo, [FromBody] List<Sys1104MappingDto>? rows,
                                                  CancellationToken cancellationToken) =>
        OkResponse(await _service.SaveMappingsAsync(userNo, rows, cancellationToken), "Mappings saved");

    [HttpPost("mappings/bulk")]
    public async Task<IActionResult> BulkSaveMappings([FromBody] Sys1104BulkSaveDto request,
                                                      CancellationToken cancellationToken)
    {
        var count = await _service.BulkSaveMappingsAsync(request.UserNos, request.Rows, cancellationToken);
        return OkResponse(count, $"Mappings saved for {count} user(s)");
    }
}
