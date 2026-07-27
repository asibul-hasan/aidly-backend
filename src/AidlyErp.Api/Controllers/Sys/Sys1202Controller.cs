using AidlyErp.Application.Sys.Dto;
using AidlyErp.Application.Sys.Services;
using Microsoft.AspNetCore.Mvc;

namespace AidlyErp.Api.Controllers.Sys;

/// <summary>
/// Dedicated controller for the SYS1202 Menu Enrollment form.
///
/// <code>
/// GET  /companies                      – every company in the group
/// GET  /companies/{companyNo}/menus    – full menu catalogue with this company's enrolments
/// POST /companies/{companyNo}/menus    – reconcile enrolments (unticked rows are un-enrolled)
/// </code>
/// </summary>
[ApiController]
[Route("api/v1/sys/forms/sys1202")]
public class Sys1202Controller : ApiControllerBase
{
    private readonly ISys1202Service _service;

    public Sys1202Controller(ISys1202Service service) => _service = service;

    [HttpGet("companies")]
    public async Task<IActionResult> GetCompanies(CancellationToken cancellationToken) =>
        OkResponse(await _service.GetCompaniesAsync(cancellationToken));

    [HttpGet("companies/{companyNo:long}/menus")]
    public async Task<IActionResult> GetMenus(long companyNo, CancellationToken cancellationToken) =>
        OkResponse(await _service.GetMenusAsync(companyNo, cancellationToken));

    [HttpPost("companies/{companyNo:long}/menus")]
    public async Task<IActionResult> SaveEnrollments(long companyNo, [FromBody] List<Sys1202MenuRow>? rows,
                                                     CancellationToken cancellationToken) =>
        OkResponse(await _service.SaveEnrollmentsAsync(companyNo, rows, cancellationToken), "Enrollments saved");
}
