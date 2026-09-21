using Microsoft.AspNetCore.Mvc;
using AidlyErp.Inv.Application.Services;

namespace AidlyErp.Api.Controllers.Inv;

[Route("api/inv/2004")]
[Route("api/v1/inv/forms/inv2004")]
public class Inv2004Controller : ApiControllerBase
{
    private readonly IInv2004Service _service;
    public Inv2004Controller(IInv2004Service service) => _service = service;

    /// <summary>
    /// Expiry dashboard. All filters are optional; <c>status</c> is one of
    /// <c>expired</c>/<c>near_expiry</c>/<c>ok</c>/<c>no_expiry</c> and <c>nearDays</c> sets what
    /// counts as near (default 30).
    /// </summary>
    [HttpGet("batches")]
    public async Task<IActionResult> GetBatches([FromQuery] long? warehouseNo, [FromQuery] long? productNo,
                                                [FromQuery] string? status, [FromQuery] int? nearDays)
        => OkResponse(await _service.GetBatchesAsync(warehouseNo, productNo, status, nearDays));
}
