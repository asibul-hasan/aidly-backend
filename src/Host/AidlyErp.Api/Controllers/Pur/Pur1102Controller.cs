using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using AidlyErp.Pur.Application.Dto;
using AidlyErp.Pur.Application.Services;

namespace AidlyErp.Api.Controllers.Pur;

[Route("api/pur/1102")]
[Route("api/v1/pur/forms/pur1102")]
public class Pur1102Controller : ApiControllerBase
{
    private readonly IPur1102Service _service;
    private readonly IPurLookupService _lookups;

    public Pur1102Controller(IPur1102Service service, IPurLookupService lookups)
    {
        _service = service;
        _lookups = lookups;
    }

    [HttpGet("lookups")]
    public async Task<IActionResult> GetLookups() => OkResponse(await _lookups.GetLookupsAsync());

    [HttpGet("invoices")]
    public async Task<IActionResult> GetList() => OkResponse(await _service.GetListAsync());

    [HttpGet("invoices/{id:long}")]
    public async Task<IActionResult> GetDetail(long id) => OkResponse(await _service.GetDetailAsync(id));

    [HttpPost("invoices")]
    public async Task<IActionResult> Save([FromBody] Pur1102InvoiceDto dto)
        => OkResponse(await _service.SaveAsync(dto), dto.InvoiceNo.HasValue ? "Invoice updated" : "Invoice saved");

    [HttpPut("invoices/{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] Pur1102InvoiceDto dto)
    {
        dto.InvoiceNo = id;
        return OkResponse(await _service.SaveAsync(dto), "Invoice updated");
    }

    [HttpPost("invoices/{id:long}/submit")]
    public async Task<IActionResult> Submit(long id) => OkResponse(await _service.SubmitAsync(id), "Invoice submitted");

    [HttpPost("invoices/{id:long}/reject")]
    public async Task<IActionResult> Reject(long id) => OkResponse(await _service.RejectAsync(id), "Invoice rejected");

    [HttpPost("invoices/{id:long}/cancel")]
    public async Task<IActionResult> Cancel(long id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ReasonRequest? body)
        => OkResponse(await _service.CancelAsync(id, body?.Reason), "Invoice cancelled");

    [HttpDelete("invoices/{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        await _service.DeleteAsync(id);
        return OkResponse<object?>(null, "Invoice deleted");
    }
}
