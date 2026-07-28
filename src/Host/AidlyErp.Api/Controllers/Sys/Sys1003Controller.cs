using AidlyErp.Sys.Application.Dto;
using AidlyErp.Sys.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace AidlyErp.Api.Controllers.Sys;

/// <summary>
/// Dedicated controller for the SYS_1003 Financial Year Setup form.
///
/// <code>
/// GET    /financial-years                      – list years in scope
/// GET    /financial-years/page                 – paginated list
/// GET    /financial-years/branch/{branchNo}    – list for a branch
/// GET    /financial-years/{finYearNo}          – master + periods
/// GET    /financial-years/{finYearNo}/periods  – periods only
/// POST   /financial-years                      – create or update (upsert on fin_year_no)
/// DELETE /financial-years/{finYearNo}          – soft-delete with cascade to periods
/// </code>
/// </summary>
[ApiController]
[Route("api/v1/sys/forms/sys1003")]
public class Sys1003Controller : ApiControllerBase
{
    private readonly ISys1003Service _service;

    public Sys1003Controller(ISys1003Service service) => _service = service;

    [HttpGet("financial-years")]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken) =>
        OkResponse(await _service.GetListAsync(cancellationToken));

    [HttpGet("financial-years/page")]
    public async Task<IActionResult> GetListPaginated([FromQuery] int page = 0, [FromQuery] int size = 20,
                                                      CancellationToken cancellationToken = default)
    {
        var all = await _service.GetListAsync(cancellationToken);

        var start = Math.Max(page, 0) * Math.Max(size, 1);
        var content = start < all.Count
            ? all.Skip(start).Take(Math.Max(size, 1)).ToList()
            : new List<Sys1003FinYearDto>();

        return OkResponse(new
        {
            content,
            number = page,
            size,
            total_elements = all.Count,
            total_pages = size > 0 ? (int)Math.Ceiling(all.Count / (double)size) : 0
        });
    }

    [HttpGet("financial-years/branch/{branchNo:long}")]
    public async Task<IActionResult> GetListByBranch(long branchNo, CancellationToken cancellationToken) =>
        OkResponse(await _service.GetListByBranchAsync(branchNo, cancellationToken));

    [HttpGet("financial-years/{finYearNo:long}")]
    public async Task<IActionResult> GetDetail(long finYearNo, CancellationToken cancellationToken) =>
        OkResponse(await _service.GetDetailAsync(finYearNo, cancellationToken));

    [HttpGet("financial-years/{finYearNo:long}/periods")]
    public async Task<IActionResult> GetPeriods(long finYearNo, CancellationToken cancellationToken) =>
        OkResponse(await _service.GetPeriodsAsync(finYearNo, cancellationToken));

    [HttpPost("financial-years")]
    public async Task<IActionResult> Save([FromBody] Sys1003FinYearDto dto, CancellationToken cancellationToken) =>
        OkResponse(await _service.SaveAsync(dto, cancellationToken), "Financial year saved");

    [HttpDelete("financial-years/{finYearNo:long}")]
    public async Task<IActionResult> Delete(long finYearNo, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(finYearNo, cancellationToken);
        return OkResponse<object?>(null, "Financial year deleted");
    }
}
