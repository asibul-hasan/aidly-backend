using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using AidlyErp.Sal.Application.Dto;
using AidlyErp.Sal.Application.Services;

namespace AidlyErp.Api.Controllers.Sal;

[Route("api/sal/1001")]
[Route("api/v1/sal/forms/sal1001")]
public class Sal1001Controller : ApiControllerBase
{
    private readonly ISal1001Service _service;
    public Sal1001Controller(ISal1001Service service) => _service = service;

    [HttpGet("lookups")]
    public async Task<IActionResult> GetLookups() => OkResponse(await _service.GetLookupsAsync());

    [HttpGet("invoices")]
    public async Task<IActionResult> GetList() => OkResponse(await _service.GetListAsync());

    [HttpGet("invoices/{id:long}")]
    public async Task<IActionResult> GetDetail(long id) => OkResponse(await _service.GetDetailAsync(id));

    [HttpPost("invoices")]
    public async Task<IActionResult> Save([FromBody] Sal1001InvoiceDto dto) => OkResponse(await _service.SaveAsync(dto), "Sale saved");

    [HttpPut("invoices/{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] Sal1001InvoiceDto dto)
    {
        dto.InvoiceNo = id;
        return OkResponse(await _service.SaveAsync(dto), "Sale updated");
    }

    [HttpPost("invoices/{id:long}/submit")]
    public async Task<IActionResult> Submit(long id) => OkResponse(await _service.SubmitAsync(id), "Sale submitted");

    [HttpPost("invoices/{id:long}/post")]
    public async Task<IActionResult> Post(long id) => OkResponse(await _service.PostAsync(id), "Sale posted");

    [HttpPost("invoices/{id:long}/reject")]
    public async Task<IActionResult> Reject(long id) => OkResponse(await _service.RejectAsync(id), "Sale rejected");

    [HttpPost("invoices/{id:long}/cancel")]
    public async Task<IActionResult> Cancel(long id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ReasonRequest? body)
        => OkResponse(await _service.CancelAsync(id, body?.Reason), "Sale cancelled");

    [HttpDelete("invoices/{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        await _service.DeleteAsync(id);
        return OkResponse<object?>(null, "Sale deleted");
    }
}
