using AidlyErp.Sys.Application.Dto;
using AidlyErp.Sys.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace AidlyErp.Api.Controllers.Sys;

/// <summary>
/// Dedicated controller for the SYS_1006 Exchange Rate Setup form.
///
/// <code>
/// GET    /currencies                          – selectable currencies for the active branch
/// GET    /exchange-rates                      – rate history for the active company
/// POST   /exchange-rates                      – create or update (upsert on exchange_rate_no)
/// DELETE /exchange-rates/{exchangeRateNo}     – soft-delete a rate row
/// </code>
/// </summary>
[ApiController]
[Route("api/v1/sys/forms/sys1006")]
public class Sys1006Controller : ApiControllerBase
{
    private readonly ISys1006Service _service;

    public Sys1006Controller(ISys1006Service service) => _service = service;

    [HttpGet("currencies")]
    public async Task<IActionResult> GetCurrencyOptions(CancellationToken cancellationToken) =>
        OkResponse(await _service.GetCurrencyOptionsAsync(cancellationToken));

    [HttpGet("exchange-rates")]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken) =>
        OkResponse(await _service.GetListAsync(cancellationToken));

    [HttpPost("exchange-rates")]
    public async Task<IActionResult> Save([FromBody] Sys1006ExchangeRateDto dto, CancellationToken cancellationToken) =>
        OkResponse(await _service.SaveAsync(dto, cancellationToken), "Exchange rate saved");

    [HttpDelete("exchange-rates/{exchangeRateNo:long}")]
    public async Task<IActionResult> Delete(long exchangeRateNo, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(exchangeRateNo, cancellationToken);
        return OkResponse<object?>(null, "Exchange rate deleted");
    }
}
