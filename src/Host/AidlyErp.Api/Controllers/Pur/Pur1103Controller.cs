using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using AidlyErp.Pur.Application.Dto;
using AidlyErp.Pur.Application.Services;

namespace AidlyErp.Api.Controllers.Pur;

[Route("api/pur/1103")]
[Route("api/v1/pur/forms/pur1103")]
public class Pur1103Controller : ApiControllerBase
{
    private readonly IPur1103Service _service;
    private readonly IPurLookupService _lookups;

    public Pur1103Controller(IPur1103Service service, IPurLookupService lookups)
    {
        _service = service;
        _lookups = lookups;
    }

    [HttpGet("lookups")]
    public async Task<IActionResult> GetLookups() => OkResponse(await _lookups.GetLookupsAsync());

    [HttpGet("returns")]
    public async Task<IActionResult> GetList() => OkResponse(await _service.GetListAsync());

    [HttpGet("returns/{id:long}")]
    public async Task<IActionResult> GetDetail(long id) => OkResponse(await _service.GetDetailAsync(id));

    [HttpPost("returns")]
    public async Task<IActionResult> Create([FromBody] Pur1103ReturnDto dto) => OkResponse(await _service.CreateAsync(dto), "Return posted");

    [HttpPost("returns/{id:long}/cancel")]
    public async Task<IActionResult> Cancel(long id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ReasonRequest? body)
        => OkResponse(await _service.CancelAsync(id, body?.Reason), "Return cancelled");

    [HttpDelete("returns/{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        await _service.DeleteAsync(id);
        return OkResponse<object?>(null, "Return deleted");
    }
}
