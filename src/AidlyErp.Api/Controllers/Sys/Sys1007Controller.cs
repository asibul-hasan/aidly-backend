using AidlyErp.Application.Sys.Dto;
using AidlyErp.Application.Sys.Services;
using Microsoft.AspNetCore.Mvc;

namespace AidlyErp.Api.Controllers.Sys;

/// <summary>
/// Dedicated controller for the SYS_1007 Cost Center Setup form.
///
/// <code>
/// GET    /cost-centers                  – list cost centres in scope
/// POST   /cost-centers                  – create or update (upsert on cost_center_no)
/// DELETE /cost-centers/{costCenterNo}   – soft-delete (blocked while children exist)
/// </code>
/// </summary>
[ApiController]
[Route("api/v1/sys/forms/sys1007")]
public class Sys1007Controller : ApiControllerBase
{
    private readonly ISys1007Service _service;

    public Sys1007Controller(ISys1007Service service) => _service = service;

    [HttpGet("cost-centers")]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken) =>
        OkResponse(await _service.GetListAsync(cancellationToken));

    [HttpPost("cost-centers")]
    public async Task<IActionResult> Save([FromBody] Sys1007CostCenterDto dto, CancellationToken cancellationToken) =>
        OkResponse(await _service.SaveAsync(dto, cancellationToken), "Cost center saved");

    [HttpDelete("cost-centers/{costCenterNo:long}")]
    public async Task<IActionResult> Delete(long costCenterNo, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(costCenterNo, cancellationToken);
        return OkResponse<object?>(null, "Cost center deleted");
    }
}
