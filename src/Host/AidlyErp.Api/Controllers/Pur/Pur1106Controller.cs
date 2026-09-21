using Microsoft.AspNetCore.Mvc;
using AidlyErp.Pur.Application.Dto;
using AidlyErp.Pur.Application.Services;

namespace AidlyErp.Api.Controllers.Pur;

[Route("api/pur/1106")]
[Route("api/v1/pur/forms/pur1106")]
public class Pur1106Controller : ApiControllerBase
{
    private readonly IPur1106Service _service;
    public Pur1106Controller(IPur1106Service service) => _service = service;

    [HttpGet("landed-costs")]
    public async Task<IActionResult> GetList() => OkResponse(await _service.GetListAsync());

    [HttpGet("landed-costs/{id:long}")]
    public async Task<IActionResult> GetDetail(long id) => OkResponse(await _service.GetDetailAsync(id));

    [HttpGet("posted-invoices")]
    public async Task<IActionResult> GetPostedInvoices() => OkResponse(await _service.GetPostedInvoicesAsync());

    [HttpGet("invoices/{invoiceNo:long}/preview")]
    public async Task<IActionResult> Preview(long invoiceNo, [FromQuery] decimal amount, [FromQuery] short basis = 1)
        => OkResponse(await _service.PreviewAsync(invoiceNo, amount, basis));

    [HttpPost("landed-costs")]
    public async Task<IActionResult> Save([FromBody] Pur1106LandedCostDto dto)
        => OkResponse(await _service.SaveAsync(dto), dto.LandedCostNo.HasValue ? "Landed cost updated" : "Landed cost saved");

    [HttpPut("landed-costs/{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] Pur1106LandedCostDto dto)
    {
        dto.LandedCostNo = id;
        return OkResponse(await _service.SaveAsync(dto), "Landed cost updated");
    }

    [HttpPost("landed-costs/{id:long}/apply")]
    public async Task<IActionResult> Apply(long id) => OkResponse(await _service.ApplyAsync(id), "Landed cost applied");

    [HttpDelete("landed-costs/{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        await _service.DeleteAsync(id);
        return OkResponse<object?>(null, "Landed cost deleted");
    }
}
