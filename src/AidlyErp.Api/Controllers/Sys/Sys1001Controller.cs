using AidlyErp.Application.Sys.Dto;
using AidlyErp.Application.Sys.Services;
using Microsoft.AspNetCore.Mvc;

namespace AidlyErp.Api.Controllers.Sys;

/// <summary>
/// Dedicated controller for the SYS1001 Company Setup form.
///
/// <para>Logo upload / removal / streaming is handled by the shared core file API at
/// <c>/api/v1/sys/files</c> (entity_type = 1 = CompanyLogo). After a logo operation the frontend
/// passes the updated <c>logo_path</c> (or <c>null</c>) in the regular PUT save payload.</para>
///
/// <code>
/// GET    /{companyNo}       – load company
/// POST   /                  – create new company
/// PUT    /{companyNo}       – update existing company
/// DELETE /{companyNo}       – soft-delete company
/// </code>
/// </summary>
[ApiController]
[Route("api/v1/sys/forms/sys1001")]
public class Sys1001Controller : ApiControllerBase
{
    private readonly ISys1001Service _service;

    public Sys1001Controller(ISys1001Service service) => _service = service;

    /// <summary>Load company detail.</summary>
    [HttpGet("{companyNo:long}")]
    public async Task<IActionResult> GetDetail(long companyNo, CancellationToken cancellationToken) =>
        OkResponse(await _service.GetDetailAsync(companyNo, cancellationToken));

    /// <summary>Create new company.</summary>
    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] Sys1001CompanyDto dto, CancellationToken cancellationToken) =>
        OkResponse(await _service.InsertAsync(dto, cancellationToken), "Company created");

    /// <summary>Update existing company.</summary>
    [HttpPut("{companyNo:long}")]
    public async Task<IActionResult> Update(long companyNo, [FromBody] Sys1001CompanyDto dto,
                                            CancellationToken cancellationToken) =>
        OkResponse(await _service.UpdateAsync(companyNo, dto, cancellationToken), "Company updated");

    /// <summary>Soft-delete company.</summary>
    [HttpDelete("{companyNo:long}")]
    public async Task<IActionResult> Delete(long companyNo, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(companyNo, cancellationToken);
        return OkResponse<object?>(null, "Company deleted");
    }
}
