using AidlyErp.Api.Middleware;
using AidlyErp.Application.Sys.Dto;
using AidlyErp.Application.Sys.Services;
using Microsoft.AspNetCore.Mvc;

namespace AidlyErp.Api.Controllers.Sys;

/// <summary>
/// Bridges the application layer to the RBAC middleware's URL→form cache, which lives in the API
/// layer. Registered as the <see cref="IPathFormCacheInvalidator"/> implementation.
/// </summary>
public sealed class PathFormCacheInvalidator : IPathFormCacheInvalidator
{
    public void EvictPathFormCache() => RbacAuthorizationMiddleware.EvictPathFormCache();
}

/// <summary>
/// Dedicated controller for the SYS1201 Menu Builder (Module → Submodule → Menu).
///
/// <code>
/// GET    /modules                       POST /modules      DELETE /modules/{moduleNo}
/// GET    /module-options
/// GET    /submodules                    POST /submodules   DELETE /submodules/{submoduleNo}
/// GET    /submodule-options
/// GET    /menus                         POST /menus        DELETE /menus/{menuNo}
/// </code>
/// </summary>
[ApiController]
[Route("api/v1/sys/forms/sys1201")]
public class Sys1201Controller : ApiControllerBase
{
    private readonly ISys1201Service _service;

    public Sys1201Controller(ISys1201Service service) => _service = service;

    // ─── Modules ─────────────────────────────────────────────────────────────

    [HttpGet("modules")]
    public async Task<IActionResult> ListModules(CancellationToken cancellationToken) =>
        OkResponse(await _service.ListModulesAsync(cancellationToken));

    [HttpPost("modules")]
    public async Task<IActionResult> SaveModule([FromBody] Sys1201ModuleDto dto, CancellationToken cancellationToken) =>
        OkResponse(await _service.SaveModuleAsync(dto, cancellationToken), "Module saved");

    [HttpDelete("modules/{moduleNo:long}")]
    public async Task<IActionResult> DeleteModule(long moduleNo, CancellationToken cancellationToken)
    {
        await _service.DeleteModuleAsync(moduleNo, cancellationToken);
        return OkResponse<object?>(null, "Module deleted");
    }

    [HttpGet("module-options")]
    public async Task<IActionResult> GetModuleOptions(CancellationToken cancellationToken) =>
        OkResponse(await _service.GetModuleOptionsAsync(cancellationToken));

    // ─── Submodules ──────────────────────────────────────────────────────────

    [HttpGet("submodules")]
    public async Task<IActionResult> ListSubmodules(CancellationToken cancellationToken) =>
        OkResponse(await _service.ListSubmodulesAsync(cancellationToken));

    [HttpPost("submodules")]
    public async Task<IActionResult> SaveSubmodule([FromBody] Sys1201SubmoduleDto dto,
                                                   CancellationToken cancellationToken) =>
        OkResponse(await _service.SaveSubmoduleAsync(dto, cancellationToken), "Submodule saved");

    [HttpDelete("submodules/{submoduleNo:long}")]
    public async Task<IActionResult> DeleteSubmodule(long submoduleNo, CancellationToken cancellationToken)
    {
        await _service.DeleteSubmoduleAsync(submoduleNo, cancellationToken);
        return OkResponse<object?>(null, "Submodule deleted");
    }

    [HttpGet("submodule-options")]
    public async Task<IActionResult> GetSubmoduleOptions(CancellationToken cancellationToken) =>
        OkResponse(await _service.GetSubmoduleOptionsAsync(cancellationToken));

    // ─── Menus ───────────────────────────────────────────────────────────────

    [HttpGet("menus")]
    public async Task<IActionResult> ListMenus(CancellationToken cancellationToken) =>
        OkResponse(await _service.ListMenusAsync(cancellationToken));

    [HttpPost("menus")]
    public async Task<IActionResult> SaveMenu([FromBody] Sys1201MenuDto dto, CancellationToken cancellationToken) =>
        OkResponse(await _service.SaveMenuAsync(dto, cancellationToken), "Menu saved");

    [HttpDelete("menus/{menuNo:long}")]
    public async Task<IActionResult> DeleteMenu(long menuNo, CancellationToken cancellationToken)
    {
        await _service.DeleteMenuAsync(menuNo, cancellationToken);
        return OkResponse<object?>(null, "Menu deleted");
    }
}
