using Microsoft.AspNetCore.Mvc;
using AidlyErp.Sal.Application.Dto;
using AidlyErp.Sal.Application.Services;

namespace AidlyErp.Api.Controllers.Sal;

[Route("api/sal/1005")]
[Route("api/v1/sal/forms/sal1005")]
public class Sal1005Controller : ApiControllerBase
{
    private readonly ISal1005Service _service;
    public Sal1005Controller(ISal1005Service service) => _service = service;

    [HttpGet("lookups")]
    public async Task<IActionResult> GetLookups() => OkResponse(await _service.GetLookupsAsync());

    [HttpGet("terminals")]
    public async Task<IActionResult> GetList() => OkResponse(await _service.GetListAsync());

    [HttpPost("terminals")]
    public async Task<IActionResult> Save([FromBody] Sal1005TerminalDto dto)
        => OkResponse(await _service.SaveAsync(dto), dto.TerminalNo.HasValue ? "Terminal updated" : "Terminal saved");

    [HttpPut("terminals/{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] Sal1005TerminalDto dto)
    {
        dto.TerminalNo = id;
        return OkResponse(await _service.SaveAsync(dto), "Terminal updated");
    }

    [HttpDelete("terminals/{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        await _service.DeleteAsync(id);
        return OkResponse<object?>(null, "Terminal deleted");
    }
}
