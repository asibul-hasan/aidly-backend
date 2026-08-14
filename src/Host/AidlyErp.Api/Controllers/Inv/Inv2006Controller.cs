using Microsoft.AspNetCore.Mvc;
using AidlyErp.Inv.Application.Dto;
using AidlyErp.Inv.Application.Services;

namespace AidlyErp.Api.Controllers.Inv;

[Route("api/inv/2006")]
[Route("api/v1/inv/forms/inv2006")]
public class Inv2006Controller : ApiControllerBase
{
    private readonly IInv2006Service _service;
    private readonly IInvLookupService _lookups;

    public Inv2006Controller(IInv2006Service service, IInvLookupService lookups)
    {
        _service = service;
        _lookups = lookups;
    }

    [HttpGet("warehouse-options")]
    public async Task<IActionResult> GetWarehouseOptions() => OkResponse(await _lookups.GetWarehouseOptionsAsync());

    [HttpGet("product-options")]
    public async Task<IActionResult> GetProductOptions() => OkResponse(await _lookups.GetProductOptionsAsync());

    [HttpGet("counts")]
    public async Task<IActionResult> GetList() => OkResponse(await _service.GetListAsync());

    [HttpGet("counts/{id:long}")]
    public async Task<IActionResult> GetDetail(long id) => OkResponse(await _service.GetDetailAsync(id));

    /// <summary>Opens a sheet by snapshotting what the warehouse currently holds.</summary>
    [HttpPost("counts/start")]
    public async Task<IActionResult> Start([FromBody] Inv2006StartCountDto dto)
        => OkResponse(await _service.StartAsync(dto), "Count sheet opened");

    [HttpPut("counts/{id:long}/lines")]
    public async Task<IActionResult> SaveCounts(long id, [FromBody] List<Inv2006CountLineDto> lines)
        => OkResponse(await _service.SaveCountsAsync(id, lines), "Counts saved");

    /// <summary>Posts the variance as a Count adjustment through the stock engine.</summary>
    [HttpPost("counts/{id:long}/post")]
    public async Task<IActionResult> Post(long id) => OkResponse(await _service.PostAsync(id), "Count posted");

    [HttpPost("counts/{id:long}/cancel")]
    public async Task<IActionResult> Cancel(long id) => OkResponse(await _service.CancelAsync(id), "Count cancelled");

    [HttpDelete("counts/{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        await _service.DeleteAsync(id);
        return OkResponse<object?>(null, "Count deleted");
    }
}
