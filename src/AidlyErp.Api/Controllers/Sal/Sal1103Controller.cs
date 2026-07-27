using Microsoft.AspNetCore.Mvc;
using AidlyErp.Application.Sal.Dto;
using AidlyErp.Application.Sal.Services;

namespace AidlyErp.Api.Controllers.Sal;

[Route("api/sal/1103")]
[Route("api/v1/sal/forms/sal1103")]
public class Sal1103Controller : ApiControllerBase
{
    private readonly ISal1103Service _service;
    public Sal1103Controller(ISal1103Service service) => _service = service;

    [HttpGet("returns")]
    public async Task<IActionResult> GetList() => OkResponse(await _service.GetListAsync());

    [HttpGet("returns/{id:long}")]
    public async Task<IActionResult> GetDetail(long id) => OkResponse(await _service.GetDetailAsync(id));

    [HttpPost("returns")]
    public async Task<IActionResult> Save([FromBody] Sal1103ReturnDto dto) => OkResponse(await _service.SaveAsync(dto), dto.ReturnNo.HasValue ? "Return updated" : "Return saved");

    [HttpPost("returns/{id:long}/post")]
    public async Task<IActionResult> Post(long id) => OkResponse(await _service.PostAsync(id), "Return posted");

    [HttpDelete("returns/{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        await _service.DeleteAsync(id);
        return OkResponse<object?>(null, "Return deleted");
    }
}
