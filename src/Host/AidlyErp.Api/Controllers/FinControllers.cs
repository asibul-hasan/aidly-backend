using Microsoft.AspNetCore.Mvc;
using AidlyErp.Fin.Application.Dto;
using AidlyErp.Fin.Application.Services;

namespace AidlyErp.Api.Controllers;

[Route("api/fin/1001")]
public class Fin1001Controller : ApiControllerBase
{
    private readonly IFin1001Service _service;
    public Fin1001Controller(IFin1001Service service) => _service = service;

    [HttpGet("accounts")]
    public async Task<IActionResult> GetAccounts() => OkResponse(await _service.GetListAsync());

    [HttpGet("accounts/{id:long}")]
    public async Task<IActionResult> GetDetail(long id) => OkResponse(await _service.GetDetailAsync(id));

    [HttpPost("accounts")]
    public async Task<IActionResult> Save([FromBody] Fin1001AccountDto dto) => OkResponse(await _service.SaveAsync(dto));

    [HttpDelete("accounts/{id:long}")]
    public async Task<IActionResult> Delete(long id) { await _service.DeleteAsync(id); return OkResponse("Account deleted successfully"); }
}

[Route("api/fin/1002")]
public class Fin1002Controller : ApiControllerBase
{
    private readonly IFin1002Service _service;
    public Fin1002Controller(IFin1002Service service) => _service = service;

    [HttpGet("account-groups")]
    public async Task<IActionResult> GetGroups() => OkResponse(await _service.GetListAsync());

    [HttpGet("account-groups/{id:long}")]
    public async Task<IActionResult> GetDetail(long id) => OkResponse(await _service.GetDetailAsync(id));

    [HttpPost("account-groups")]
    public async Task<IActionResult> Save([FromBody] Fin1002AccountGroupDto dto) => OkResponse(await _service.SaveAsync(dto));

    [HttpDelete("account-groups/{id:long}")]
    public async Task<IActionResult> Delete(long id) { await _service.DeleteAsync(id); return OkResponse("Account group deleted successfully"); }
}

[Route("api/fin/1003")]
public class Fin1003Controller : ApiControllerBase
{
    private readonly IFin1003Service _service;
    public Fin1003Controller(IFin1003Service service) => _service = service;

    [HttpGet("voucher-types")]
    public async Task<IActionResult> GetTypes() => OkResponse(await _service.GetListAsync());

    [HttpGet("voucher-types/{id:long}")]
    public async Task<IActionResult> GetDetail(long id) => OkResponse(await _service.GetDetailAsync(id));

    [HttpPost("voucher-types")]
    public async Task<IActionResult> Save([FromBody] Fin1003VoucherTypeDto dto) => OkResponse(await _service.SaveAsync(dto));

    [HttpDelete("voucher-types/{id:long}")]
    public async Task<IActionResult> Delete(long id) { await _service.DeleteAsync(id); return OkResponse("Voucher type deleted successfully"); }
}

[Route("api/fin/1004")]
public class Fin1004Controller : ApiControllerBase
{
    private readonly IFin1004Service _service;
    public Fin1004Controller(IFin1004Service service) => _service = service;

    [HttpGet("opening-balances")]
    public async Task<IActionResult> GetAccounts() => OkResponse(await _service.GetAccountsWithOpeningBalanceAsync());

    [HttpPost("opening-balances")]
    public async Task<IActionResult> SaveOpening([FromBody] Fin1004OpeningBalanceDto dto) => OkResponse(await _service.SaveOpeningBalancesAsync(dto));
}

[Route("api/fin/1005")]
public class Fin1005Controller : ApiControllerBase
{
    private readonly IFin1005Service _service;
    public Fin1005Controller(IFin1005Service service) => _service = service;

    [HttpGet("bank-accounts")]
    public async Task<IActionResult> GetBanks() => OkResponse(await _service.GetListAsync());

    [HttpGet("bank-accounts/{id:long}")]
    public async Task<IActionResult> GetDetail(long id) => OkResponse(await _service.GetDetailAsync(id));

    [HttpPost("bank-accounts")]
    public async Task<IActionResult> Save([FromBody] Fin1005BankAccountDto dto) => OkResponse(await _service.SaveAsync(dto));

    [HttpDelete("bank-accounts/{id:long}")]
    public async Task<IActionResult> Delete(long id) { await _service.DeleteAsync(id); return OkResponse("Bank account deleted successfully"); }
}

[Route("api/fin/1006")]
public class Fin1006Controller : ApiControllerBase
{
    private readonly IFin1006Service _service;
    public Fin1006Controller(IFin1006Service service) => _service = service;

    [HttpGet("gl-mappings")]
    public async Task<IActionResult> GetMaps() => OkResponse(await _service.GetListAsync());

    [HttpPost("gl-mappings")]
    public async Task<IActionResult> Save([FromBody] Fin1006GlMapDto dto) => OkResponse(await _service.SaveAsync(dto));

    [HttpDelete("gl-mappings/{id:long}")]
    public async Task<IActionResult> Delete(long id) { await _service.DeleteAsync(id); return OkResponse("GL map deleted successfully"); }
}

[Route("api/fin/1101")]
public class Fin1101Controller : ApiControllerBase
{
    private readonly IFin1101Service _service;
    public Fin1101Controller(IFin1101Service service) => _service = service;

    [HttpGet("vouchers")]
    public async Task<IActionResult> GetVouchers([FromQuery] long? type, [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate, [FromQuery] string? search) =>
        OkResponse(await _service.GetListAsync(type, fromDate, toDate, search));

    [HttpGet("vouchers/{id:long}")]
    public async Task<IActionResult> GetDetail(long id) => OkResponse(await _service.GetDetailAsync(id));

    [HttpPost("vouchers")]
    public async Task<IActionResult> Save([FromBody] Fin1101VoucherDto dto) => OkResponse(await _service.SaveAsync(dto));

    [HttpPost("vouchers/{id:long}/submit")]
    public async Task<IActionResult> Submit(long id) => OkResponse(await _service.SubmitAsync(id));

    [HttpPost("vouchers/{id:long}/reject")]
    public async Task<IActionResult> Reject(long id) => OkResponse(await _service.RejectAsync(id));

    [HttpPost("vouchers/{id:long}/cancel")]
    public async Task<IActionResult> Cancel(long id) => OkResponse(await _service.CancelAsync(id));

    [HttpDelete("vouchers/{id:long}")]
    public async Task<IActionResult> Delete(long id) { await _service.DeleteAsync(id); return OkResponse("Voucher deleted successfully"); }
}

[Route("api/fin/1102")]
public class Fin1102Controller : ApiControllerBase
{
    private readonly IFin1102Service _service;
    public Fin1102Controller(IFin1102Service service) => _service = service;

    [HttpGet("worksheet")]
    public async Task<IActionResult> GetWorksheet([FromQuery] long accountNo, [FromQuery] DateTime statementDate) =>
        OkResponse(await _service.GetWorksheetAsync(accountNo, statementDate));

    [HttpPost("reconcile")]
    public async Task<IActionResult> SaveReconciliation([FromBody] Fin1102SaveDto dto) => OkResponse(await _service.SaveReconciliationAsync(dto));

    [HttpGet("history/{accountNo:long}")]
    public async Task<IActionResult> GetHistory(long accountNo) => OkResponse(await _service.GetReconHistoryAsync(accountNo));
}

[Route("api/fin/1201")]
public class Fin1201Controller : ApiControllerBase
{
    private readonly IFin1201Service _service;
    public Fin1201Controller(IFin1201Service service) => _service = service;

    [HttpGet("posting-events")]
    public async Task<IActionResult> GetEvents([FromQuery] short? status) => OkResponse(await _service.GetOutboxEventsAsync(status));

    [HttpPost("redrive/{eventNo:long}")]
    public async Task<IActionResult> Redrive(long eventNo) { await _service.RedriveAsync(eventNo); return OkResponse("Event redriven successfully"); }
}

[Route("api/fin/1301")]
public class Fin1301Controller : ApiControllerBase
{
    private readonly IFinReportService _service;
    public Fin1301Controller(IFinReportService service) => _service = service;

    [HttpGet("trial-balance")]
    public async Task<IActionResult> GetTrialBalance([FromQuery] DateTime? asOfDate) => OkResponse(await _service.GetTrialBalanceAsync(asOfDate));
}

[Route("api/fin/1302")]
public class Fin1302Controller : ApiControllerBase
{
    private readonly IFinReportService _service;
    public Fin1302Controller(IFinReportService service) => _service = service;

    [HttpGet("general-ledger")]
    public async Task<IActionResult> GetLedger([FromQuery] long accountNo, [FromQuery] DateTime fromDate, [FromQuery] DateTime toDate) =>
        OkResponse(await _service.GetGeneralLedgerAsync(accountNo, fromDate, toDate));
}

[Route("api/fin/1303")]
public class Fin1303Controller : ApiControllerBase
{
    private readonly IFinReportService _service;
    public Fin1303Controller(IFinReportService service) => _service = service;

    [HttpGet("day-book")]
    public async Task<IActionResult> GetDayBook([FromQuery] DateTime fromDate, [FromQuery] DateTime toDate) =>
        OkResponse(await _service.GetDayBookAsync(fromDate, toDate));
}

[Route("api/fin/1304")]
public class Fin1304Controller : ApiControllerBase
{
    private readonly IFinReportService _service;
    public Fin1304Controller(IFinReportService service) => _service = service;

    [HttpGet("profit-and-loss")]
    public async Task<IActionResult> GetPnl([FromQuery] DateTime fromDate, [FromQuery] DateTime toDate) =>
        OkResponse(await _service.GetPnlAsync(fromDate, toDate));
}

[Route("api/fin/1305")]
public class Fin1305Controller : ApiControllerBase
{
    private readonly IFinReportService _service;
    public Fin1305Controller(IFinReportService service) => _service = service;

    [HttpGet("balance-sheet")]
    public async Task<IActionResult> GetBalanceSheet([FromQuery] DateTime asOfDate) =>
        OkResponse(await _service.GetBalanceSheetAsync(asOfDate));
}

[Route("api/fin/1306")]
public class Fin1306Controller : ApiControllerBase
{
    private readonly IFinReportService _service;
    public Fin1306Controller(IFinReportService service) => _service = service;

    [HttpGet("cash-flow")]
    public async Task<IActionResult> GetCashFlow([FromQuery] DateTime fromDate, [FromQuery] DateTime toDate) =>
        OkResponse(await _service.GetCashFlowAsync(fromDate, toDate));
}

[Route("api/fin/1307")]
public class Fin1307Controller : ApiControllerBase
{
    private readonly IFinReportService _service;
    public Fin1307Controller(IFinReportService service) => _service = service;

    [HttpGet("aging-analysis")]
    public async Task<IActionResult> GetAging([FromQuery] short partyType, [FromQuery] DateTime asOfDate) =>
        OkResponse(await _service.GetAgingAsync(partyType, asOfDate));
}

[Route("api/fin/1308")]
public class Fin1308ChartOfAccountsPdfController : ApiControllerBase
{
    private readonly IFin1308ChartOfAccountsPdfService _service;
    public Fin1308ChartOfAccountsPdfController(IFin1308ChartOfAccountsPdfService service) => _service = service;

    [HttpGet("chart-of-accounts-rows")]
    public async Task<IActionResult> GetRows() => OkResponse(await _service.GetCoaExportRowsAsync());
}

[Route("api/fin/1401")]
public class Fin1401Controller : ApiControllerBase
{
    private readonly IFin1401Service _service;
    public Fin1401Controller(IFin1401Service service) => _service = service;

    [HttpGet("years")]
    public async Task<IActionResult> GetYears() => OkResponse(await _service.GetYearsAndPeriodsAsync());

    [HttpPost("close-period/{periodNo:long}")]
    public async Task<IActionResult> ClosePeriod(long periodNo) { await _service.ClosePeriodAsync(periodNo); return OkResponse("Period closed successfully"); }

    [HttpPost("close-year")]
    public async Task<IActionResult> CloseYear([FromBody] Fin1401CloseRequestDto dto) => OkResponse(await _service.CloseFiscalYearAsync(dto));
}
