using Microsoft.AspNetCore.Mvc;
using AidlyErp.Sal.Application.Services;

namespace AidlyErp.Api.Controllers.Sal;

[Route("api/sal/1301")]
[Route("api/v1/sal/reports/sal1301")]
public class Sal1301Controller : ApiControllerBase
{
    private readonly ISalReportService _service;
    public Sal1301Controller(ISalReportService service) => _service = service;

    /// <summary>
    /// Sales over a date range: daily totals, and breakdowns by product, cashier and hour.
    /// Confirmed sales only — drafts and cancelled documents never appear.
    /// </summary>
    [HttpGet("sales")]
    public async Task<IActionResult> GetSales([FromQuery] DateTime from, [FromQuery] DateTime to)
        => OkResponse(await _service.GetSalesAsync(from, to));
}
