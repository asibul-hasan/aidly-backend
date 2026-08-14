using Microsoft.AspNetCore.Mvc;
using AidlyErp.Inv.Application.Dto;
using AidlyErp.Inv.Application.Services;

namespace AidlyErp.Api.Controllers.Inv;

[Route("api/inv/1101")]
[Route("api/v1/inv/forms/inv1101")]
public class Inv1101Controller : ApiControllerBase
{
    private readonly IInv1101Service _service;
    public Inv1101Controller(IInv1101Service service) => _service = service;

    [HttpGet("lookups")]
    public async Task<IActionResult> GetLookups() => OkResponse(await _service.GetLookupsAsync());

    [HttpGet("openings")]
    public async Task<IActionResult> GetList() => OkResponse(await _service.GetListAsync());

    [HttpGet("openings/{id:long}")]
    public async Task<IActionResult> GetDetail(long id) => OkResponse(await _service.GetDetailAsync(id));

    [HttpPost("openings")]
    public async Task<IActionResult> Save([FromBody] Inv1101OpeningDto dto)
        => OkResponse(await _service.SaveAsync(dto), dto.AdjustmentNo.HasValue ? "Opening updated" : "Opening saved");

    [HttpPut("openings/{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] Inv1101OpeningDto dto)
    {
        dto.AdjustmentNo = id;
        return OkResponse(await _service.SaveAsync(dto), "Opening updated");
    }

    [HttpPost("openings/{id:long}/post")]
    public async Task<IActionResult> Post(long id) => OkResponse(await _service.PostAsync(id), "Opening stock posted");

    [HttpDelete("openings/{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        await _service.DeleteAsync(id);
        return OkResponse<object?>(null, "Opening deleted");
    }
}
