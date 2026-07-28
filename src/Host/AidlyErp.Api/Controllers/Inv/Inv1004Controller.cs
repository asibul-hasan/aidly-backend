using Microsoft.AspNetCore.Mvc;
using AidlyErp.Inv.Application.Dto;
using AidlyErp.Inv.Application.Services;

namespace AidlyErp.Api.Controllers.Inv;

[Route("api/inv/1004")]
[Route("api/v1/inv/forms/inv1004")]
public class Inv1004Controller : ApiControllerBase
{
    private readonly IInv1004Service _service;
    public Inv1004Controller(IInv1004Service service) => _service = service;

    [HttpGet("brands")]
    public async Task<IActionResult> GetList() => OkResponse(await _service.GetListAsync());

    [HttpPost("brands")]
    public async Task<IActionResult> Save([FromBody] Inv1004BrandDto dto) => OkResponse(await _service.SaveAsync(dto), dto.BrandNo.HasValue ? "Brand updated" : "Brand saved");

    [HttpDelete("brands/{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        await _service.DeleteAsync(id);
        return OkResponse<object?>(null, "Brand deleted");
    }
}
