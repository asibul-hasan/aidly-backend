using Microsoft.AspNetCore.Mvc;
using AidlyErp.Application.Pur.Services;

namespace AidlyErp.Api.Controllers.Pur;

[Route("api/pur/ap-ledger")]
[Route("api/v1/pur/reports/ap-ledger")]
public class PurApLedgerController : ApiControllerBase
{
    private readonly IPurApLedgerService _service;
    public PurApLedgerController(IPurApLedgerService service) => _service = service;

    [HttpGet("statement/{supplierNo:long}")]
    public async Task<IActionResult> GetStatement(long supplierNo) => OkResponse(await _service.GetStatementAsync(supplierNo));
}
