using AidlyErp.Application.Sys.Dto;
using AidlyErp.Application.Sys.Services;
using Microsoft.AspNetCore.Mvc;

namespace AidlyErp.Api.Controllers.Sys;

/// <summary>
/// Dedicated controller for the SYS_1005 VAT / Tax Setup form.
///
/// <code>
/// GET    /vat-taxes                    – list taxes in scope
/// GET    /vat-taxes/page               – paginated list
/// GET    /vat-taxes/branch/{branchNo}  – list for a branch
/// GET    /vat-taxes/{vatTaxNo}         – tax detail
/// POST   /vat-taxes                    – create or update (upsert on vat_tax_no)
/// DELETE /vat-taxes/{vatTaxNo}         – soft-delete tax
/// </code>
/// </summary>
[ApiController]
[Route("api/v1/sys/forms/sys1005")]
public class Sys1005Controller : ApiControllerBase
{
    private readonly ISys1005Service _service;

    public Sys1005Controller(ISys1005Service service) => _service = service;

    [HttpGet("vat-taxes")]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken) =>
        OkResponse(await _service.GetListAsync(cancellationToken));

    [HttpGet("vat-taxes/page")]
    public async Task<IActionResult> GetListPaginated([FromQuery] int page = 0, [FromQuery] int size = 20,
                                                      CancellationToken cancellationToken = default)
    {
        var all = await _service.GetListAsync(cancellationToken);

        var start = Math.Max(page, 0) * Math.Max(size, 1);
        var content = start < all.Count
            ? all.Skip(start).Take(Math.Max(size, 1)).ToList()
            : new List<Sys1005VatTaxDto>();

        return OkResponse(new
        {
            content,
            number = page,
            size,
            total_elements = all.Count,
            total_pages = size > 0 ? (int)Math.Ceiling(all.Count / (double)size) : 0
        });
    }

    [HttpGet("vat-taxes/branch/{branchNo:long}")]
    public async Task<IActionResult> GetListByBranch(long branchNo, CancellationToken cancellationToken) =>
        OkResponse(await _service.GetListByBranchAsync(branchNo, cancellationToken));

    [HttpGet("vat-taxes/{vatTaxNo:long}")]
    public async Task<IActionResult> GetDetail(long vatTaxNo, CancellationToken cancellationToken) =>
        OkResponse(await _service.GetDetailAsync(vatTaxNo, cancellationToken));

    [HttpPost("vat-taxes")]
    public async Task<IActionResult> Save([FromBody] Sys1005VatTaxDto dto, CancellationToken cancellationToken) =>
        OkResponse(await _service.SaveAsync(dto, cancellationToken), "VAT/Tax saved");

    [HttpDelete("vat-taxes/{vatTaxNo:long}")]
    public async Task<IActionResult> Delete(long vatTaxNo, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(vatTaxNo, cancellationToken);
        return OkResponse<object?>(null, "VAT/Tax deleted");
    }
}
