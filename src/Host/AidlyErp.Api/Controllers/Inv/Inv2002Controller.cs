using Microsoft.AspNetCore.Mvc;
using AidlyErp.Inv.Application.Dto;
using AidlyErp.Inv.Application.Services;

namespace AidlyErp.Api.Controllers.Inv;

[Route("api/inv/2002")]
[Route("api/v1/inv/forms/inv2002")]
public class Inv2002Controller : ApiControllerBase
{
    private readonly IInv2002Service _service;
    private readonly IInvLookupService _lookups;

    public Inv2002Controller(IInv2002Service service, IInvLookupService lookups)
    {
        _service = service;
        _lookups = lookups;
    }

    [HttpGet("warehouse-options")]
    public async Task<IActionResult> GetWarehouseOptions() => OkResponse(await _lookups.GetWarehouseOptionsAsync());

    [HttpGet("racks")]
    public async Task<IActionResult> GetList([FromQuery] long warehouseNo)
        => OkResponse(await _service.GetListAsync(warehouseNo));

    [HttpPost("racks")]
    public async Task<IActionResult> Save([FromBody] Inv2002RackDto dto)
        => OkResponse(await _service.SaveAsync(dto), dto.RackNo.HasValue ? "Rack updated" : "Rack saved");

    [HttpDelete("racks/{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        await _service.DeleteAsync(id);
        return OkResponse<object?>(null, "Rack deleted");
    }
}
