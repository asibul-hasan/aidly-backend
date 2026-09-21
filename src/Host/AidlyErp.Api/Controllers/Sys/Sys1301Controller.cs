using AidlyErp.Sys.Application.Dto;
using AidlyErp.Sys.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace AidlyErp.Api.Controllers.Sys;

/// <summary>
/// SYS_1301 ID Generator setup — configures the document-number series that
/// <c>DocSequenceGenerator</c> issues from. Nothing here hands out a number.
/// </summary>
[Route("api/sys/1301")]
[Route("api/v1/sys/forms/sys1301")]
public class Sys1301Controller : ApiControllerBase
{
    private readonly ISys1301Service _service;
    public Sys1301Controller(ISys1301Service service) => _service = service;

    /// <summary>Configured series, optionally narrowed to one form.</summary>
    [HttpGet("configs")]
    public async Task<IActionResult> GetConfigs([FromQuery(Name = "menu_no")] long? menuNo)
        => OkResponse(await _service.GetListAsync(menuNo));

    /// <summary>Forms a series can be attached to.</summary>
    [HttpGet("menu-options")]
    public async Task<IActionResult> GetMenuOptions() => OkResponse(await _service.GetMenuOptionsAsync());

    [HttpGet("fin-year-options")]
    public async Task<IActionResult> GetFinYearOptions() => OkResponse(await _service.GetFinYearOptionsAsync());

    /// <summary>Every token a pattern may use, for the form's help text.</summary>
    [HttpGet("tokens")]
    public IActionResult GetTokens() => OkResponse(_service.GetSupportedTokens());

    /// <summary>Renders a pattern against sample context, so the shape is visible before saving.</summary>
    [HttpPost("preview")]
    public IActionResult Preview([FromBody] Sys1301PreviewRequestDto request)
        => OkResponse(_service.Preview(request));

    [HttpPost("configs")]
    public async Task<IActionResult> Save([FromBody] Sys1301IdGeneratorDto dto)
        => OkResponse(await _service.SaveAsync(dto), "ID generator configuration saved");

    [HttpDelete("configs/{docSequenceNo:long}")]
    public async Task<IActionResult> Delete(long docSequenceNo)
    {
        await _service.DeleteAsync(docSequenceNo);
        return OkResponse("ID generator configuration deleted");
    }
}
