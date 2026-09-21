using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using AidlyErp.Pur.Application.Dto;
using AidlyErp.Pur.Application.Services;

namespace AidlyErp.Api.Controllers.Pur;

[Route("api/pur/1101")]
[Route("api/v1/pur/forms/pur1101")]
public class Pur1101Controller : ApiControllerBase
{
    private readonly IPur1101Service _service;
    public Pur1101Controller(IPur1101Service service) => _service = service;

    [HttpGet("orders")]
    public async Task<IActionResult> GetList() => OkResponse(await _service.GetListAsync());

    [HttpGet("orders/{id:long}")]
    public async Task<IActionResult> GetDetail(long id) => OkResponse(await _service.GetDetailAsync(id));

    [HttpPost("orders")]
    public async Task<IActionResult> Save([FromBody] Pur1101OrderDto dto)
        => OkResponse(await _service.SaveAsync(dto), dto.OrderNo.HasValue ? "Order updated" : "Order created");

    [HttpPut("orders/{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] Pur1101OrderDto dto)
    {
        dto.OrderNo = id;
        return OkResponse(await _service.SaveAsync(dto), "Order updated");
    }

    [HttpPost("orders/{id:long}/submit")]
    public async Task<IActionResult> Submit(long id) => OkResponse(await _service.SubmitAsync(id), "Order submitted");

    [HttpPost("orders/{id:long}/approve")]
    public async Task<IActionResult> Approve(long id) => OkResponse(await _service.ApproveAsync(id), "Order approved");

    [HttpPost("orders/{id:long}/reject")]
    public async Task<IActionResult> Reject(long id) => OkResponse(await _service.RejectAsync(id), "Order rejected");

    [HttpPost("orders/{id:long}/cancel")]
    public async Task<IActionResult> Cancel(long id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ReasonRequest? body)
        => OkResponse(await _service.CancelAsync(id, body?.Reason), "Order cancelled");

    [HttpDelete("orders/{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        await _service.DeleteAsync(id);
        return OkResponse<object?>(null, "Order deleted");
    }
}
