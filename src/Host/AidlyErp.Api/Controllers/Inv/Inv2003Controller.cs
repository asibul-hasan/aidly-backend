using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using AidlyErp.Inv.Application.Dto;
using AidlyErp.Inv.Application.Services;

namespace AidlyErp.Api.Controllers.Inv;

[Route("api/inv/2003")]
[Route("api/v1/inv/forms/inv2003")]
public class Inv2003Controller : ApiControllerBase
{
    private readonly IInv2003Service _service;
    public Inv2003Controller(IInv2003Service service) => _service = service;

    [HttpGet("lookups")]
    public async Task<IActionResult> GetLookups() => OkResponse(await _service.GetLookupsAsync());

    [HttpGet("transfers")]
    public async Task<IActionResult> GetList() => OkResponse(await _service.GetListAsync());

    [HttpGet("transfers/{id:long}")]
    public async Task<IActionResult> GetDetail(long id) => OkResponse(await _service.GetDetailAsync(id));

    [HttpPost("transfers")]
    public async Task<IActionResult> Save([FromBody] Inv2003TransferDto dto)
        => OkResponse(await _service.SaveAsync(dto), dto.TransferNo.HasValue ? "Transfer updated" : "Transfer saved");

    [HttpPut("transfers/{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] Inv2003TransferDto dto)
    {
        dto.TransferNo = id;
        return OkResponse(await _service.SaveAsync(dto), "Transfer updated");
    }

    [HttpPost("transfers/{id:long}/submit")]
    public async Task<IActionResult> Submit(long id) => OkResponse(await _service.SubmitAsync(id), "Transfer submitted");

    [HttpPost("transfers/{id:long}/dispatch")]
    public async Task<IActionResult> Dispatch(long id) => OkResponse(await _service.DispatchAsync(id), "Transfer dispatched");

    /// <summary>Body carries the per-line received quantities; omitting it receives everything dispatched.</summary>
    [HttpPost("transfers/{id:long}/receive")]
    public async Task<IActionResult> Receive(long id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] Inv2003TransferDto? payload)
        => OkResponse(await _service.ReceiveAsync(id, payload), "Transfer received");

    [HttpPost("transfers/{id:long}/cancel")]
    public async Task<IActionResult> Cancel(long id) => OkResponse(await _service.CancelAsync(id), "Transfer cancelled");

    [HttpDelete("transfers/{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        await _service.DeleteAsync(id);
        return OkResponse<object?>(null, "Transfer deleted");
    }
}
