using Microsoft.AspNetCore.Mvc;
using AidlyErp.Sal.Application.Dto;
using AidlyErp.Sal.Application.Services;

namespace AidlyErp.Api.Controllers.Sal;

/// <summary>
/// The till's own surface. Sits under SAL_1001 because a POS sale is a <c>sal_invoice</c> — the
/// same document the credit-invoice form writes, rung up a different way.
/// </summary>
[Route("api/sal/pos")]
[Route("api/v1/sal/forms/sal1001")]
public class SalPosController : ApiControllerBase
{
    private readonly ISalPosService _pos;
    private readonly ISalPricingService _pricing;
    private readonly ISal1001Service _sales;

    public SalPosController(ISalPosService pos, ISalPricingService pricing, ISal1001Service sales)
    {
        _pos = pos;
        _pricing = pricing;
        _sales = sales;
    }

    // ── session ──────────────────────────────────────────────────────────────

    [HttpPost("sessions/open")]
    public async Task<IActionResult> OpenSession([FromBody] SalOpenSessionRequestDto body)
        => OkResponse(await _pos.OpenAsync(body.TerminalNo, body.OpeningFloat), "Session opened");

    /// <summary>
    /// The open drawer for a terminal, or null. The till calls this on load and prompts the cashier
    /// to open a session when nothing comes back.
    /// </summary>
    [HttpGet("sessions/current")]
    public async Task<IActionResult> CurrentSession([FromQuery] long terminalNo)
        => OkResponse(await _pos.CurrentAsync(terminalNo));

    /// <summary>Sessions for this branch; pass <c>status=1</c> for the ones still open.</summary>
    [HttpGet("sessions")]
    public async Task<IActionResult> ListSessions([FromQuery] short? status)
        => OkResponse(await _pos.GetListAsync(status));

    /// <summary>The Z-report body: takings by tender, expected drawer, sales and returns.</summary>
    [HttpGet("sessions/{id:long}/summary")]
    public async Task<IActionResult> SessionSummary(long id) => OkResponse(await _pos.SummaryAsync(id));

    [HttpPost("sessions/{id:long}/close")]
    public async Task<IActionResult> CloseSession(long id, [FromBody] SalCloseSessionRequestDto body)
    {
        body.SessionNo = id;
        return OkResponse(await _pos.CloseAsync(body), "Session closed");
    }

    // ── selling ──────────────────────────────────────────────────────────────

    /// <summary>Prices a cart. The screen displays what this returns and computes no money itself.</summary>
    [HttpPost("sales/quote-price")]
    public async Task<IActionResult> QuotePrice([FromBody] SalQuotePriceRequestDto body)
        => OkResponse(await _pricing.QuoteAsync(body));

    /// <summary>
    /// Rings up the sale. Safe to retry with the same <c>client_uuid</c> — a replay returns the
    /// original sale instead of ringing up a second one.
    /// </summary>
    [HttpPost("sales")]
    public async Task<IActionResult> ConfirmSale([FromBody] SalPosSaleRequestDto body)
        => OkResponse(await _sales.ConfirmPosSaleAsync(body), "Sale completed");
}
