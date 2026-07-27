using Microsoft.AspNetCore.Mvc;
using AidlyErp.Application.Sal.Services;

namespace AidlyErp.Api.Controllers.Sal;

[Route("api/sal/ar-ledger")]
[Route("api/v1/sal/reports/ar-ledger")]
public class SalArLedgerController : ApiControllerBase
{
    private readonly ISalArLedgerService _service;
    public SalArLedgerController(ISalArLedgerService service) => _service = service;

    [HttpGet("statement/{customerNo:long}")]
    public async Task<IActionResult> GetStatement(long customerNo) => OkResponse(await _service.GetStatementAsync(customerNo));
}
