using Microsoft.AspNetCore.Mvc;
using AidlyErp.Pur.Application.Dto;
using AidlyErp.Pur.Application.Services;

namespace AidlyErp.Api.Controllers.Pur;

[Route("api/pur/1102")]
[Route("api/v1/pur/forms/pur1102")]
public class Pur1102Controller : ApiControllerBase
{
    private readonly IPur1102Service _service;
    public Pur1102Controller(IPur1102Service service) => _service = service;

    [HttpGet("invoices")]
    public async Task<IActionResult> GetList() => OkResponse(await _service.GetListAsync());

    [HttpGet("invoices/{id:long}")]
    public async Task<IActionResult> GetDetail(long id) => OkResponse(await _service.GetDetailAsync(id));

    [HttpPost("invoices")]
    public async Task<IActionResult> Save([FromBody] Pur1102InvoiceDto dto) => OkResponse(await _service.SaveAsync(dto), dto.InvoiceNo.HasValue ? "Invoice updated" : "Invoice saved");

    [HttpPost("invoices/{id:long}/post")]
    public async Task<IActionResult> Post(long id) => OkResponse(await _service.PostAsync(id), "Invoice posted");

    [HttpDelete("invoices/{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        await _service.DeleteAsync(id);
        return OkResponse<object?>(null, "Invoice deleted");
    }
}
