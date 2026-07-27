using AidlyErp.Application.Sys.Dto;
using AidlyErp.Application.Sys.Services;
using Microsoft.AspNetCore.Mvc;

namespace AidlyErp.Api.Controllers.Sys;

/// <summary>
/// Dedicated controller for the SYS_1008 System Settings form.
///
/// <code>
/// GET    /settings               – list settings in scope
/// POST   /settings               – create or update (upsert on setting_no)
/// DELETE /settings/{settingNo}   – soft-delete setting
/// </code>
/// </summary>
[ApiController]
[Route("api/v1/sys/forms/sys1008")]
public class Sys1008Controller : ApiControllerBase
{
    private readonly ISys1008Service _service;

    public Sys1008Controller(ISys1008Service service) => _service = service;

    [HttpGet("settings")]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken) =>
        OkResponse(await _service.GetListAsync(cancellationToken));

    [HttpPost("settings")]
    public async Task<IActionResult> Save([FromBody] Sys1008SettingDto dto, CancellationToken cancellationToken) =>
        OkResponse(await _service.SaveAsync(dto, cancellationToken), "Setting saved");

    [HttpDelete("settings/{settingNo:long}")]
    public async Task<IActionResult> Delete(long settingNo, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(settingNo, cancellationToken);
        return OkResponse<object?>(null, "Setting deleted");
    }
}
