using AidlyErp.Sys.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace AidlyErp.Api.Controllers.Sys;

/// <summary>
/// Dedicated controller for the SYS1105 Activity Log Viewer (read-only).
///
/// <code>
/// GET /logs?table_name=&amp;action_type=&amp;user_no=&amp;from_at=&amp;to_at=&amp;limit=
/// </code>
/// </summary>
[ApiController]
[Route("api/v1/sys/forms/sys1105")]
public class Sys1105Controller : ApiControllerBase
{
    private readonly ISys1105Service _service;

    public Sys1105Controller(ISys1105Service service) => _service = service;

    [HttpGet("logs")]
    public async Task<IActionResult> Search([FromQuery(Name = "table_name")] string? tableName,
                                            [FromQuery(Name = "action_type")] string? actionType,
                                            [FromQuery(Name = "user_no")] long? userNo,
                                            [FromQuery(Name = "from_at")] DateTime? fromAt,
                                            [FromQuery(Name = "to_at")] DateTime? toAt,
                                            [FromQuery(Name = "limit")] int? limit,
                                            CancellationToken cancellationToken) =>
        OkResponse(await _service.SearchAsync(tableName, actionType, userNo, fromAt, toAt, limit, cancellationToken));
}
