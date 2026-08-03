using AidlyErp.Sys.Application.Dto;
using AidlyErp.Sys.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace AidlyErp.Api.Controllers.Sys;

/// <summary>
/// Dedicated controller for the SYS1002 Branch Setup form.
///
/// <code>
/// GET    /branches             – list branches for the current company
/// GET    /branches/{branchNo}  – load branch detail
/// POST   /branches             – create or update (upsert on branch_no)
/// DELETE /branches/{branchNo}  – soft-delete branch
/// GET    /employees            – active employees, for the manager picker
/// </code>
/// </summary>
[ApiController]
[Route("api/v1/sys/forms/sys1002")]
public class Sys1002Controller : ApiControllerBase
{
    private readonly ISys1002Service _service;

    public Sys1002Controller(ISys1002Service service) => _service = service;

    [HttpGet("branches")]
    public async Task<IActionResult> GetBranchList(CancellationToken cancellationToken) =>
        OkResponse(await _service.GetBranchListAsync(cancellationToken));

    [HttpGet("branches/{branchNo:long}")]
    public async Task<IActionResult> GetBranchDetail(long branchNo, CancellationToken cancellationToken) =>
        OkResponse(await _service.GetBranchDetailAsync(branchNo, cancellationToken));

    [HttpPost("branches")]
    public async Task<IActionResult> Save([FromBody] Sys1002BranchDto dto, CancellationToken cancellationToken) =>
        OkResponse(await _service.SaveAsync(dto, cancellationToken), "Branch saved");

    [HttpDelete("branches/{branchNo:long}")]
    public async Task<IActionResult> Delete(long branchNo, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(branchNo, cancellationToken);
        return OkResponse<object?>(null, "Branch deleted");
    }

    [HttpGet("employees")]
    public async Task<IActionResult> GetEmployees(CancellationToken cancellationToken) =>
        OkResponse(await _service.GetEmployeesAsync(cancellationToken));
}
