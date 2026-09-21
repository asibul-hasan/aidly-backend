using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using AidlyErp.Sal.Application.Dto;
using AidlyErp.Sal.Application.Services;

namespace AidlyErp.Api.Controllers.Sal;

[Route("api/sal/1102")]
[Route("api/v1/sal/forms/sal1102")]
public class Sal1102Controller : ApiControllerBase
{
    private readonly ISal1102Service _service;
    private readonly ISal1001Service _sales;

    public Sal1102Controller(ISal1102Service service, ISal1001Service sales)
    {
        _service = service;
        _sales = sales;
    }

    /// <summary>Customer dropdown for the collection form — the same list SAL_1001 offers.</summary>
    [HttpGet("customers")]
    public async Task<IActionResult> GetCustomers() => OkResponse((await _sales.GetLookupsAsync()).Customers);

    [HttpGet("receipts")]
    public async Task<IActionResult> GetList() => OkResponse(await _service.GetListAsync());

    [HttpGet("receipts/{id:long}")]
    public async Task<IActionResult> GetDetail(long id) => OkResponse(await _service.GetDetailAsync(id));

    /// <summary>Query-string form the Angular client calls; the path form stays for API consumers.</summary>
    [HttpGet("open-invoices")]
    public async Task<IActionResult> GetOpenInvoicesByQuery([FromQuery] long customerNo)
        => OkResponse(await _service.GetOpenInvoicesAsync(customerNo));

    [HttpGet("open-invoices/{customerNo:long}")]
    public async Task<IActionResult> GetOpenInvoices(long customerNo) => OkResponse(await _service.GetOpenInvoicesAsync(customerNo));

    [HttpPost("receipts")]
    public async Task<IActionResult> Save([FromBody] Sal1102ReceiptDto dto) => OkResponse(await _service.SaveAsync(dto), dto.ReceiptNo.HasValue ? "Receipt updated" : "Receipt saved");

    [HttpPost("receipts/{id:long}/post")]
    public async Task<IActionResult> Post(long id) => OkResponse(await _service.PostAsync(id), "Receipt posted");

    [HttpPost("receipts/{id:long}/cancel")]
    public async Task<IActionResult> Cancel(long id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ReasonRequest? body) =>
        OkResponse(await _service.CancelAsync(id, body?.Reason), "Receipt cancelled");

    [HttpDelete("receipts/{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        await _service.DeleteAsync(id);
        return OkResponse<object?>(null, "Receipt deleted");
    }
}
