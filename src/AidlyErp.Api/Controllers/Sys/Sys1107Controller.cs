using AidlyErp.Application.Sys.Dto;
using AidlyErp.Application.Sys.Services;
using Microsoft.AspNetCore.Mvc;

namespace AidlyErp.Api.Controllers.Sys;

/// <summary>
/// Dedicated controller for the SYS1107 User-Company Mapping form.
///
/// <code>
/// GET  /users                    – users of the active company
/// GET  /companies                – every company in the group
/// GET  /users/{userNo}/mappings  – the user's company grants
/// POST /users/{userNo}/mappings  – replace the user's company grants
/// </code>
/// </summary>
[ApiController]
[Route("api/v1/sys/forms/sys1107")]
public class Sys1107Controller : ApiControllerBase
{
    private readonly ISys1107Service _service;

    public Sys1107Controller(ISys1107Service service) => _service = service;

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers(CancellationToken cancellationToken) =>
        OkResponse(await _service.GetUsersAsync(cancellationToken));

    [HttpGet("companies")]
    public async Task<IActionResult> GetCompanies(CancellationToken cancellationToken) =>
        OkResponse(await _service.GetCompaniesAsync(cancellationToken));

    [HttpGet("users/{userNo:long}/mappings")]
    public async Task<IActionResult> GetMappings(long userNo, CancellationToken cancellationToken) =>
        OkResponse(await _service.GetMappingsAsync(userNo, cancellationToken));

    [HttpPost("users/{userNo:long}/mappings")]
    public async Task<IActionResult> SaveMappings(long userNo, [FromBody] List<Sys1107MappingDto>? rows,
                                                  CancellationToken cancellationToken) =>
        OkResponse(await _service.SaveMappingsAsync(userNo, rows, cancellationToken), "Mappings saved");
}
