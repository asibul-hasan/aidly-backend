using AidlyErp.Application.Sys.Dto;
using AidlyErp.Application.Sys.Services;
using Microsoft.AspNetCore.Mvc;

namespace AidlyErp.Api.Controllers.Sys;

/// <summary>
/// Dedicated controller for the SYS1108 Approval Workflow Setup form.
///
/// <code>
/// GET    /workflows            – approval scopes with their steps and approvers
/// GET    /menus                – menus the company is enrolled in
/// POST   /workflows            – create or update a scope (steps are replaced wholesale)
/// DELETE /workflows/{scopeNo}  – soft-delete a scope
/// </code>
/// </summary>
[ApiController]
[Route("api/v1/sys/forms/sys1108")]
public class Sys1108Controller : ApiControllerBase
{
    private readonly ISys1108Service _service;

    public Sys1108Controller(ISys1108Service service) => _service = service;

    [HttpGet("workflows")]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken) =>
        OkResponse(await _service.GetListAsync(cancellationToken));

    [HttpGet("menus")]
    public async Task<IActionResult> GetEnrolledMenus(CancellationToken cancellationToken) =>
        OkResponse(await _service.GetEnrolledMenuOptionsAsync(cancellationToken));

    [HttpPost("workflows")]
    public async Task<IActionResult> Save([FromBody] Sys1108ScopeDto dto, CancellationToken cancellationToken) =>
        OkResponse(await _service.SaveAsync(dto, cancellationToken), "Workflow saved");

    [HttpDelete("workflows/{scopeNo:long}")]
    public async Task<IActionResult> Delete(long scopeNo, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(scopeNo, cancellationToken);
        return OkResponse<object?>(null, "Workflow deleted");
    }
}
