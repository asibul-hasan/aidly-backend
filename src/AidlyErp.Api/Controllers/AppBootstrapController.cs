using AidlyErp.Application.Bootstrap;
using Microsoft.AspNetCore.Mvc;

namespace AidlyErp.Api.Controllers;

/// <summary>
/// First-paint context for the frontend shell — <c>GET /api/v1/app/bootstrap</c>.
/// </summary>
[ApiController]
[Route("api/v1/app")]
public class AppBootstrapController : ApiControllerBase
{
    private readonly IAppBootstrapService _service;

    public AppBootstrapController(IAppBootstrapService service) => _service = service;

    [HttpGet("bootstrap")]
    public async Task<IActionResult> Bootstrap(CancellationToken cancellationToken) =>
        OkResponse(await _service.GetBootstrapAsync(cancellationToken));
}
