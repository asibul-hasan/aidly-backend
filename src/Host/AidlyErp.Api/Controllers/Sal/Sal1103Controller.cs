using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using AidlyErp.Sal.Application.Dto;
using AidlyErp.Sal.Application.Services;

namespace AidlyErp.Api.Controllers.Sal;

[Route("api/sal/1103")]
[Route("api/v1/sal/forms/sal1103")]
public class Sal1103Controller : ApiControllerBase
{
    private readonly ISal1103Service _service;
    private readonly ISal1001Service _sales;

    public Sal1103Controller(ISal1103Service service, ISal1001Service sales)
    {
        _service = service;
        _sales = sales;
    }

    /// <summary>The return form fills the same four dropdowns as the sale form.</summary>
    [HttpGet("lookups")]
    public async Task<IActionResult> GetLookups() => OkResponse(await _sales.GetLookupsAsync());

    [HttpGet("returns")]
    public async Task<IActionResult> GetList() => OkResponse(await _service.GetListAsync());

    [HttpGet("returns/{id:long}")]
    public async Task<IActionResult> GetDetail(long id) => OkResponse(await _service.GetDetailAsync(id));

    [HttpPost("returns")]
    public async Task<IActionResult> Save([FromBody] Sal1103ReturnDto dto) => OkResponse(await _service.SaveAsync(dto), dto.ReturnNo.HasValue ? "Return updated" : "Return saved");

    [HttpPost("returns/{id:long}/post")]
    public async Task<IActionResult> Post(long id) => OkResponse(await _service.PostAsync(id), "Return posted");

    [HttpPost("returns/{id:long}/cancel")]
    public async Task<IActionResult> Cancel(long id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ReasonRequest? body) =>
        OkResponse(await _service.CancelAsync(id, body?.Reason), "Return cancelled");

    [HttpDelete("returns/{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        await _service.DeleteAsync(id);
        return OkResponse<object?>(null, "Return deleted");
    }
}
