using AidlyErp.Sys.Application.Dto;
using AidlyErp.Sys.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace AidlyErp.Api.Controllers.Sys;

/// <summary>SYS_1301 Document ID Generator — how each form numbers its documents.</summary>
[Route("api/sys/1301")]
[Route("api/v1/sys/forms/sys1301")]
public class Sys1301Controller : ApiControllerBase
{
    private readonly ISys1301Service _service;
    public Sys1301Controller(ISys1301Service service) => _service = service;

    /// <summary>Configured series, optionally narrowed to one form.</summary>
    [HttpGet("configs")]
    public async Task<IActionResult> GetConfigs([FromQuery] long? menuNo)
        => OkResponse(await _service.GetListAsync(menuNo));

    /// <summary>Every form a series can be attached to, grouped module → submodule → form.</summary>
    [HttpGet("menu-options")]
    public async Task<IActionResult> GetMenuOptions() => OkResponse(await _service.GetMenuOptionsAsync());

    [HttpGet("fin-year-options")]
    public async Task<IActionResult> GetFinYearOptions() => OkResponse(await _service.GetFinYearOptionsAsync());

    /// <summary>The tokens a pattern may use, for the form's help text.</summary>
    [HttpGet("tokens")]
    public IActionResult GetTokens() => OkResponse(_service.GetTokens());

    /// <summary>Renders a sample number so the pattern can be checked before it is saved.</summary>
    [HttpPost("preview")]
    public async Task<IActionResult> Preview([FromBody] Sys1301PreviewRequestDto request)
        => OkResponse(await _service.PreviewAsync(request));

    [HttpPost("configs")]
    public async Task<IActionResult> Save([FromBody] Sys1301IdGeneratorDto dto)
        => OkResponse(await _service.SaveAsync(dto), "Sequence saved");

    [HttpDelete("configs/{docSequenceNo:long}")]
    public async Task<IActionResult> Delete(long docSequenceNo)
    {
        await _service.DeleteAsync(docSequenceNo);
        return OkResponse("Sequence deleted");
    }
}
