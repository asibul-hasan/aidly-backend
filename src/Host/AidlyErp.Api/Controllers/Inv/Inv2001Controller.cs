using Microsoft.AspNetCore.Mvc;
using AidlyErp.Inv.Application.Dto;
using AidlyErp.Inv.Application.Services;

namespace AidlyErp.Api.Controllers.Inv;

[Route("api/inv/2001")]
[Route("api/v1/inv/forms/inv2001")]
public class Inv2001Controller : ApiControllerBase
{
    private readonly IInv2001Service _service;
    public Inv2001Controller(IInv2001Service service) => _service = service;

    [HttpGet("warehouses")]
    public async Task<IActionResult> GetList() => OkResponse(await _service.GetListAsync());

    [HttpPost("warehouses")]
    public async Task<IActionResult> Save([FromBody] Inv2001WarehouseDto dto)
        => OkResponse(await _service.SaveAsync(dto), dto.WarehouseNo.HasValue ? "Warehouse updated" : "Warehouse saved");

    [HttpDelete("warehouses/{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        await _service.DeleteAsync(id);
        return OkResponse<object?>(null, "Warehouse deleted");
    }
}
