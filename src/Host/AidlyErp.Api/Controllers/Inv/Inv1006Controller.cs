using Microsoft.AspNetCore.Mvc;
using AidlyErp.Inv.Application.Dto;
using AidlyErp.Inv.Application.Services;

namespace AidlyErp.Api.Controllers.Inv;

[Route("api/inv/1006")]
[Route("api/v1/inv/forms/inv1006")]
public class Inv1006Controller : ApiControllerBase
{
    private readonly IInv1006Service _service;
    public Inv1006Controller(IInv1006Service service) => _service = service;

    [HttpGet("attributes")]
    public async Task<IActionResult> GetList() => OkResponse(await _service.GetListAsync());

    [HttpPost("attributes")]
    public async Task<IActionResult> Save([FromBody] Inv1006AttributeDto dto)
        => OkResponse(await _service.SaveAsync(dto), dto.AttributeNo.HasValue ? "Attribute updated" : "Attribute saved");

    [HttpDelete("attributes/{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        await _service.DeleteAsync(id);
        return OkResponse<object?>(null, "Attribute deleted");
    }
}
