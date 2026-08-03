using Microsoft.AspNetCore.Mvc;
using AidlyErp.Inv.Application.Dto;
using AidlyErp.Inv.Application.Services;

namespace AidlyErp.Api.Controllers.Inv;

[Route("api/inv/1005")]
[Route("api/v1/inv/forms/inv1005")]
public class Inv1005Controller : ApiControllerBase
{
    private readonly IInv1005Service _service;
    public Inv1005Controller(IInv1005Service service) => _service = service;

    [HttpGet("uoms")]
    public async Task<IActionResult> GetList() => OkResponse(await _service.GetListAsync());

    [HttpPost("uoms")]
    public async Task<IActionResult> Save([FromBody] Inv1005UomDto dto) => OkResponse(await _service.SaveAsync(dto), dto.UomNo.HasValue ? "UOM updated" : "UOM saved");

    [HttpDelete("uoms/{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        await _service.DeleteAsync(id);
        return OkResponse<object?>(null, "UOM deleted");
    }
}
