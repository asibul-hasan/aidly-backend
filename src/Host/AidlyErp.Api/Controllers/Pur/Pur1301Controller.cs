using Microsoft.AspNetCore.Mvc;
using AidlyErp.Pur.Application.Services;

namespace AidlyErp.Api.Controllers.Pur;

[Route("api/pur/1301")]
[Route("api/v1/pur/reports/pur1301")]
public class Pur1301Controller : ApiControllerBase
{
    private readonly IPurReportService _service;
    public Pur1301Controller(IPurReportService service) => _service = service;

    /// <summary>What is owed to whom, bucketed by how overdue it is. Posted bills only.</summary>
    [HttpGet("aging")]
    public async Task<IActionResult> GetAging([FromQuery] DateTime? asOf)
        => OkResponse(await _service.GetAgingAsync(asOf ?? DateTime.UtcNow.Date));

    /// <summary>Spend by supplier and by category over a range, measured before tax.</summary>
    [HttpGet("spend")]
    public async Task<IActionResult> GetSpend([FromQuery] DateTime from, [FromQuery] DateTime to)
        => OkResponse(await _service.GetSpendAsync(from, to));
}
