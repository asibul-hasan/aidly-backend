using AidlyErp.Sys.Application.Dto;
using AidlyErp.Sys.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace AidlyErp.Api.Controllers.Sys;

/// <summary>
/// Dedicated controller for the SYS_1004 Currency Setup form.
///
/// <code>
/// GET    /currencies                    – list currencies in scope
/// GET    /currencies/page               – paginated list
/// GET    /currencies/branch/{branchNo}  – list for a branch
/// GET    /currencies/rate-history       – exchange-rate history (?currency_no=)
/// GET    /currencies/{currencyNo}       – currency detail
/// POST   /currencies                    – create or update (upsert on currency_no)
/// DELETE /currencies/{currencyNo}       – soft-delete currency
/// </code>
/// </summary>
[ApiController]
[Route("api/v1/sys/forms/sys1004")]
public class Sys1004Controller : ApiControllerBase
{
    private readonly ISys1004Service _service;

    public Sys1004Controller(ISys1004Service service) => _service = service;

    [HttpGet("currencies")]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken) =>
        OkResponse(await _service.GetListAsync(cancellationToken));

    [HttpGet("currencies/page")]
    public async Task<IActionResult> GetListPaginated([FromQuery] int page = 0, [FromQuery] int size = 20,
                                                      CancellationToken cancellationToken = default)
    {
        var all = await _service.GetListAsync(cancellationToken);

        var start = Math.Max(page, 0) * Math.Max(size, 1);
        var content = start < all.Count
            ? all.Skip(start).Take(Math.Max(size, 1)).ToList()
            : new List<Sys1004CurrencyDto>();

        return OkResponse(new
        {
            content,
            number = page,
            size,
            total_elements = all.Count,
            total_pages = size > 0 ? (int)Math.Ceiling(all.Count / (double)size) : 0
        });
    }

    [HttpGet("currencies/branch/{branchNo:long}")]
    public async Task<IActionResult> GetListByBranch(long branchNo, CancellationToken cancellationToken) =>
        OkResponse(await _service.GetListByBranchAsync(branchNo, cancellationToken));

    /// <summary>Exchange-rate history for a currency. Query parameter name matches the Java contract.</summary>
    [HttpGet("currencies/rate-history")]
    public async Task<IActionResult> GetRateHistory([FromQuery(Name = "currency_no")] long currencyNo,
                                                    CancellationToken cancellationToken) =>
        OkResponse(await _service.GetRateHistoryAsync(currencyNo, cancellationToken));

    [HttpGet("currencies/{currencyNo:long}")]
    public async Task<IActionResult> GetDetail(long currencyNo, CancellationToken cancellationToken) =>
        OkResponse(await _service.GetDetailAsync(currencyNo, cancellationToken));

    [HttpPost("currencies")]
    public async Task<IActionResult> Save([FromBody] Sys1004CurrencyDto dto, CancellationToken cancellationToken) =>
        OkResponse(await _service.SaveAsync(dto, cancellationToken), "Currency saved");

    [HttpDelete("currencies/{currencyNo:long}")]
    public async Task<IActionResult> Delete(long currencyNo, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(currencyNo, cancellationToken);
        return OkResponse<object?>(null, "Currency deleted");
    }
}

/// <summary>
/// Shared currency endpoints consumed ERP-wide (not form-specific).
/// Port of <c>sys.controller.CurrencySettingsController</c>.
/// </summary>
[ApiController]
[Route("api/v1/sys/currency")]
public class CurrencySettingsController : ApiControllerBase
{
    private readonly ISys1004Service _service;

    public CurrencySettingsController(ISys1004Service service) => _service = service;

    /// <summary>
    /// Base-currency formatting settings for the active branch. <c>data</c> is <c>null</c> when no
    /// base currency is configured — the frontend falls back to its built-in defaults.
    /// </summary>
    [HttpGet("base-settings")]
    public async Task<IActionResult> GetBaseSettings(CancellationToken cancellationToken) =>
        OkResponse(await _service.GetBaseSettingsAsync(cancellationToken));

    [HttpGet("list")]
    public async Task<IActionResult> GetCurrencyLookupList(CancellationToken cancellationToken) =>
        OkResponse(await _service.GetCurrencyLookupListAsync(cancellationToken));
}
