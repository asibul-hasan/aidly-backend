using Microsoft.AspNetCore.Mvc;
using AidlyErp.Sal.Application.Dto;
using AidlyErp.Sal.Application.Services;

namespace AidlyErp.Api.Controllers.Sal;

[Route("api/sal/1102")]
[Route("api/v1/sal/forms/sal1102")]
public class Sal1102Controller : ApiControllerBase
{
    private readonly ISal1102Service _service;
    public Sal1102Controller(ISal1102Service service) => _service = service;

    [HttpGet("receipts")]
    public async Task<IActionResult> GetList() => OkResponse(await _service.GetListAsync());

    [HttpGet("receipts/{id:long}")]
    public async Task<IActionResult> GetDetail(long id) => OkResponse(await _service.GetDetailAsync(id));

    [HttpGet("open-invoices/{customerNo:long}")]
    public async Task<IActionResult> GetOpenInvoices(long customerNo) => OkResponse(await _service.GetOpenInvoicesAsync(customerNo));

    [HttpPost("receipts")]
    public async Task<IActionResult> Save([FromBody] Sal1102ReceiptDto dto) => OkResponse(await _service.SaveAsync(dto), dto.ReceiptNo.HasValue ? "Receipt updated" : "Receipt saved");

    [HttpPost("receipts/{id:long}/post")]
    public async Task<IActionResult> Post(long id) => OkResponse(await _service.PostAsync(id), "Receipt posted");

    [HttpDelete("receipts/{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        await _service.DeleteAsync(id);
        return OkResponse<object?>(null, "Receipt deleted");
    }
}
