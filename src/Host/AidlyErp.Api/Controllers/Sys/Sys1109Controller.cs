using AidlyErp.Sys.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace AidlyErp.Api.Controllers.Sys;

/// <summary>
/// Dedicated controller for the SYS1109 Session / Login Monitor.
///
/// <code>
/// GET  /sessions?limit=                 – sessions for the active company, live ones first
/// GET  /login-attempts?limit=           – recent login attempts
/// POST /sessions/{sessionNo}/revoke     – admin force-logout of a single session
/// </code>
/// </summary>
[ApiController]
[Route("api/v1/sys/forms/sys1109")]
public class Sys1109Controller : ApiControllerBase
{
    private readonly ISys1109Service _service;

    public Sys1109Controller(ISys1109Service service) => _service = service;

    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions([FromQuery] int? limit, CancellationToken cancellationToken) =>
        OkResponse(await _service.GetSessionsAsync(limit, cancellationToken));

    [HttpGet("login-attempts")]
    public async Task<IActionResult> GetLoginAttempts([FromQuery] int? limit, CancellationToken cancellationToken) =>
        OkResponse(await _service.GetLoginAttemptsAsync(limit, cancellationToken));

    [HttpPost("sessions/{sessionNo:long}/revoke")]
    public async Task<IActionResult> RevokeSession(long sessionNo, CancellationToken cancellationToken)
    {
        await _service.RevokeSessionAsync(sessionNo, cancellationToken);
        return OkResponse<object?>(null, "Session revoked");
    }
}
