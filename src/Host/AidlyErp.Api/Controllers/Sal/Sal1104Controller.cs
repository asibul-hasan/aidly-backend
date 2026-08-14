using Microsoft.AspNetCore.Mvc;
using AidlyErp.Sal.Application.Dto;
using AidlyErp.Sal.Application.Services;

namespace AidlyErp.Api.Controllers.Sal;

[Route("api/sal/1104")]
[Route("api/v1/sal/forms/sal1104")]
public class Sal1104Controller : ApiControllerBase
{
    private readonly ISal1104Service _service;
    public Sal1104Controller(ISal1104Service service) => _service = service;

    [HttpGet("lookups")]
    public async Task<IActionResult> GetLookups() => OkResponse(await _service.GetLookupsAsync());

    [HttpGet("promotions")]
    public async Task<IActionResult> GetList() => OkResponse(await _service.GetListAsync());

    [HttpGet("promotions/{id:long}")]
    public async Task<IActionResult> GetDetail(long id) => OkResponse(await _service.GetDetailAsync(id));

    [HttpPost("promotions")]
    public async Task<IActionResult> Save([FromBody] Sal1104PromotionDto dto)
        => OkResponse(await _service.SaveAsync(dto), dto.PromotionNo.HasValue ? "Promotion updated" : "Promotion saved");

    [HttpPut("promotions/{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] Sal1104PromotionDto dto)
    {
        dto.PromotionNo = id;
        return OkResponse(await _service.SaveAsync(dto), "Promotion updated");
    }

    [HttpDelete("promotions/{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        await _service.DeleteAsync(id);
        return OkResponse<object?>(null, "Promotion deleted");
    }
}
