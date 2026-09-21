using Microsoft.AspNetCore.Mvc;
using AidlyErp.Inv.Application.Dto;
using AidlyErp.Inv.Application.Services;

namespace AidlyErp.Api.Controllers.Inv;

[Route("api/inv/1102")]
[Route("api/v1/inv/forms/inv1102")]
public class Inv1102Controller : ApiControllerBase
{
    private readonly IInv1102Service _service;
    public Inv1102Controller(IInv1102Service service) => _service = service;

    [HttpGet("lookups")]
    public async Task<IActionResult> GetLookups() => OkResponse(await _service.GetLookupsAsync());

    [HttpGet("adjustments")]
    public async Task<IActionResult> GetList() => OkResponse(await _service.GetListAsync());

    [HttpGet("adjustments/{id:long}")]
    public async Task<IActionResult> GetDetail(long id) => OkResponse(await _service.GetDetailAsync(id));

    [HttpPost("adjustments")]
    public async Task<IActionResult> Save([FromBody] Inv1102AdjustmentDto dto)
        => OkResponse(await _service.SaveAsync(dto), dto.AdjustmentNo.HasValue ? "Adjustment updated" : "Adjustment saved");

    [HttpPut("adjustments/{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] Inv1102AdjustmentDto dto)
    {
        dto.AdjustmentNo = id;
        return OkResponse(await _service.SaveAsync(dto), "Adjustment updated");
    }

    [HttpPost("adjustments/{id:long}/submit")]
    public async Task<IActionResult> Submit(long id) => OkResponse(await _service.SubmitAsync(id), "Adjustment submitted");

    [HttpPost("adjustments/{id:long}/approve")]
    public async Task<IActionResult> Approve(long id) => OkResponse(await _service.ApproveAsync(id), "Adjustment approved and posted");

    [HttpPost("adjustments/{id:long}/cancel")]
    public async Task<IActionResult> Cancel(long id) => OkResponse(await _service.CancelAsync(id), "Adjustment cancelled");

    [HttpDelete("adjustments/{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        await _service.DeleteAsync(id);
        return OkResponse<object?>(null, "Adjustment deleted");
    }
}
