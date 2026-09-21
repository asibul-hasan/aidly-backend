using Microsoft.AspNetCore.Mvc;
using AidlyErp.Inv.Application.Dto;
using AidlyErp.Inv.Application.Services;

namespace AidlyErp.Api.Controllers.Inv;

[Route("api/inv/2005")]
[Route("api/v1/inv/forms/inv2005")]
public class Inv2005Controller : ApiControllerBase
{
    private readonly IInv2005Service _service;
    private readonly IInvLookupService _lookups;

    public Inv2005Controller(IInv2005Service service, IInvLookupService lookups)
    {
        _service = service;
        _lookups = lookups;
    }

    [HttpGet("warehouse-options")]
    public async Task<IActionResult> GetWarehouseOptions() => OkResponse(await _lookups.GetWarehouseOptionsAsync());

    [HttpGet("product-options")]
    public async Task<IActionResult> GetProductOptions() => OkResponse(await _lookups.GetProductOptionsAsync());

    [HttpGet("reorders")]
    public async Task<IActionResult> GetList([FromQuery] long warehouseNo)
        => OkResponse(await _service.GetListAsync(warehouseNo));

    /// <summary>The form edits the whole warehouse's policy grid, so it saves in one call.</summary>
    [HttpPost("reorders/bulk")]
    public async Task<IActionResult> SaveBulk([FromQuery] long warehouseNo, [FromBody] List<Inv2005ReorderDto> rows)
        => OkResponse(await _service.SaveBulkAsync(warehouseNo, rows), "Reorder levels saved");

    [HttpDelete("reorders/{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        await _service.DeleteAsync(id);
        return OkResponse<object?>(null, "Reorder policy deleted");
    }
}
