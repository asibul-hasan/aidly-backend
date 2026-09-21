using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using AidlyErp.Pur.Application.Dto;
using AidlyErp.Pur.Application.Services;

namespace AidlyErp.Api.Controllers.Pur;

[Route("api/pur/1105")]
[Route("api/v1/pur/forms/pur1105")]
public class Pur1105Controller : ApiControllerBase
{
    private readonly IPur1105Service _service;
    public Pur1105Controller(IPur1105Service service) => _service = service;

    [HttpGet("receipts")]
    public async Task<IActionResult> GetList() => OkResponse(await _service.GetListAsync());

    [HttpGet("receipts/{id:long}")]
    public async Task<IActionResult> GetDetail(long id) => OkResponse(await _service.GetDetailAsync(id));

    [HttpPost("receipts")]
    public async Task<IActionResult> Save([FromBody] Pur1105ReceiptDto dto)
        => OkResponse(await _service.SaveAsync(dto), dto.ReceiptNo.HasValue ? "Receipt updated" : "Receipt saved");

    [HttpPut("receipts/{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] Pur1105ReceiptDto dto)
    {
        dto.ReceiptNo = id;
        return OkResponse(await _service.SaveAsync(dto), "Receipt updated");
    }

    [HttpPost("receipts/{id:long}/post")]
    public async Task<IActionResult> Post(long id) => OkResponse(await _service.PostAsync(id), "Receipt posted");

    [HttpPost("receipts/{id:long}/cancel")]
    public async Task<IActionResult> Cancel(long id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ReasonRequest? body)
        => OkResponse(await _service.CancelAsync(id, body?.Reason), "Receipt cancelled");

    [HttpDelete("receipts/{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        await _service.DeleteAsync(id);
        return OkResponse<object?>(null, "Receipt deleted");
    }
}
