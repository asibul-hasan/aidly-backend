using Microsoft.AspNetCore.Mvc;
using AidlyErp.Fin.Application.Dto;
using AidlyErp.Fin.Application.Services;

namespace AidlyErp.Api.Controllers;

[Route("api/fin/1001")]
[Route("api/v1/fin/forms/fin1001")]
public class Fin1001Controller : ApiControllerBase
{
    private readonly IFin1001Service _service;
    private readonly IFin1002Service _groupService;
    public Fin1001Controller(IFin1001Service service, IFin1002Service groupService) { _service = service; _groupService = groupService; }

    [HttpGet("accounts")]
    public async Task<IActionResult> GetAccounts() => OkResponse(await _service.GetListAsync());

    [HttpGet("account-groups")]
    public async Task<IActionResult> GetAccountGroups() => OkResponse(await _groupService.GetListAsync());

    [HttpGet("accounts/{id:long}")]
    public async Task<IActionResult> GetDetail(long id) => OkResponse(await _service.GetDetailAsync(id));

    [HttpPost("accounts")]
    public async Task<IActionResult> Save([FromBody] Fin1001AccountDto dto) => OkResponse(await _service.SaveAsync(dto));

    [HttpPut("accounts/{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] Fin1001AccountDto dto)
    {
        dto.AccountNo = id;
        return OkResponse(await _service.SaveAsync(dto));
    }

    [HttpDelete("accounts/{id:long}")]
    public async Task<IActionResult> Delete(long id) { await _service.DeleteAsync(id); return OkResponse("Account deleted successfully"); }
}

[Route("api/fin/1002")]
[Route("api/v1/fin/forms/fin1002")]
public class Fin1002Controller : ApiControllerBase
{
    private readonly IFin1002Service _service;
    public Fin1002Controller(IFin1002Service service) => _service = service;

    [HttpGet("account-groups")]
    [HttpGet("groups")]
    public async Task<IActionResult> GetGroups() => OkResponse(await _service.GetListAsync());

    [HttpGet("account-groups/{id:long}")]
    [HttpGet("groups/{id:long}")]
    public async Task<IActionResult> GetDetail(long id) => OkResponse(await _service.GetDetailAsync(id));

    [HttpPost("account-groups")]
    [HttpPost("groups")]
    public async Task<IActionResult> Save([FromBody] Fin1002AccountGroupDto dto) => OkResponse(await _service.SaveAsync(dto));

    [HttpPut("account-groups/{id:long}")]
    [HttpPut("groups/{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] Fin1002AccountGroupDto dto)
    {
        dto.AccountGroupNo = id;
        return OkResponse(await _service.SaveAsync(dto));
    }

    [HttpDelete("account-groups/{id:long}")]
    [HttpDelete("groups/{id:long}")]
    public async Task<IActionResult> Delete(long id) { await _service.DeleteAsync(id); return OkResponse("Account group deleted successfully"); }
}

[Route("api/fin/1003")]
[Route("api/v1/fin/forms/fin1003")]
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

    [HttpPut("voucher-types/{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] Fin1003VoucherTypeDto dto)
    {
        dto.VoucherTypeNo = id;
        return OkResponse(await _service.SaveAsync(dto));
    }

    [HttpDelete("voucher-types/{id:long}")]
    public async Task<IActionResult> Delete(long id) { await _service.DeleteAsync(id); return OkResponse("Voucher type deleted successfully"); }
}

[Route("api/fin/1004")]
[Route("api/v1/fin/forms/fin1004")]
public class Fin1004Controller : ApiControllerBase
{
    private readonly IFin1004Service _service;
    public Fin1004Controller(IFin1004Service service) => _service = service;

    [HttpGet("opening-balances")]
    [HttpGet("accounts")]
    public async Task<IActionResult> GetAccounts() => OkResponse(await _service.GetAccountsWithOpeningBalanceAsync());

    [HttpPost("opening-balances")]
    [HttpPost("post")]
    public async Task<IActionResult> SaveOpening([FromBody] Fin1004OpeningBalanceDto dto) => OkResponse(await _service.SaveOpeningBalancesAsync(dto));
}

[Route("api/fin/1005")]
[Route("api/v1/fin/forms/fin1005")]
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

    [HttpPut("bank-accounts/{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] Fin1005BankAccountDto dto)
    {
        dto.BankAccountNo = id;
        return OkResponse(await _service.SaveAsync(dto));
    }

    [HttpDelete("bank-accounts/{id:long}")]
    public async Task<IActionResult> Delete(long id) { await _service.DeleteAsync(id); return OkResponse("Bank account deleted successfully"); }
}

[Route("api/fin/1006")]
[Route("api/v1/fin/forms/fin1006")]
public class Fin1006Controller : ApiControllerBase
{
    private readonly IFin1006Service _service;
    public Fin1006Controller(IFin1006Service service) => _service = service;

    [HttpGet("gl-mappings")]
    [HttpGet("maps")]
    public async Task<IActionResult> GetMaps() => OkResponse(await _service.GetListAsync());

    [HttpGet("gl-mappings/{id:long}")]
    [HttpGet("maps/{id:long}")]
    public async Task<IActionResult> GetDetail(long id) => OkResponse(await _service.GetDetailAsync(id));

    [HttpPost("gl-mappings")]
    [HttpPost("maps")]
    public async Task<IActionResult> Save([FromBody] Fin1006GlMapDto dto) => OkResponse(await _service.SaveAsync(dto));

    [HttpPut("gl-mappings/{id:long}")]
    [HttpPut("maps/{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] Fin1006GlMapDto dto)
    {
        dto.MapNo = id;
        return OkResponse(await _service.SaveAsync(dto));
    }

    [HttpDelete("gl-mappings/{id:long}")]
    [HttpDelete("maps/{id:long}")]
    public async Task<IActionResult> Delete(long id) { await _service.DeleteAsync(id); return OkResponse("GL map deleted successfully"); }
}

[Route("api/fin/1101")]
[Route("api/v1/fin/forms/fin1101")]
public class Fin1101Controller : ApiControllerBase
{
    private readonly IFin1101Service _service;
    public Fin1101Controller(IFin1101Service service) => _service = service;

    [HttpGet("vouchers")]
    public async Task<IActionResult> GetVouchers([FromQuery] long? type, [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate, [FromQuery] string? search, [FromQuery] short? status) =>
        OkResponse(await _service.GetListAsync(type, fromDate, toDate, search, status));

    [HttpGet("vouchers/{id:long}")]
    public async Task<IActionResult> GetDetail(long id) => OkResponse(await _service.GetDetailAsync(id));

    [HttpGet("lookups")]
    public async Task<IActionResult> GetLookups() => OkResponse(await _service.GetLookupsAsync());

    [HttpPost("vouchers")]
    public async Task<IActionResult> Save([FromBody] Fin1101VoucherDto dto) => OkResponse(await _service.SaveAsync(dto));

    [HttpPut("vouchers/{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] Fin1101VoucherDto dto)
    {
        dto.VoucherNo = id;
        return OkResponse(await _service.SaveAsync(dto));
    }

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
[Route("api/v1/fin/forms/fin1102")]
public class Fin1102Controller : ApiControllerBase
{
    private readonly IFin1102Service _service;
    public Fin1102Controller(IFin1102Service service) => _service = service;

    [HttpGet("bank-accounts")]
    public async Task<IActionResult> GetBankAccounts() => OkResponse(await _service.GetBankAccountsAsync());

    [HttpGet("worksheet")]
    public async Task<IActionResult> GetWorksheet([FromQuery] long accountNo, [FromQuery] DateTime statementDate) =>
        OkResponse(await _service.GetWorksheetAsync(accountNo, statementDate));

    [HttpGet("reconciliations")]
    [HttpGet("recons")]
    public async Task<IActionResult> GetList() => OkResponse(await _service.GetListAsync());

    [HttpGet("reconciliations/{id:long}")]
    [HttpGet("recons/{id:long}")]
    public async Task<IActionResult> GetDetail(long id) => OkResponse(await _service.GetDetailAsync(id));

    [HttpPost("reconcile")]
    [HttpPost("recons")]
    public async Task<IActionResult> SaveReconciliation([FromBody] Fin1102SaveDto dto) => OkResponse(await _service.SaveReconciliationAsync(dto));

    [HttpGet("history/{accountNo:long}")]
    public async Task<IActionResult> GetHistory(long accountNo) => OkResponse(await _service.GetReconHistoryAsync(accountNo));

    [HttpDelete("reconciliations/{id:long}")]
    [HttpDelete("recons/{id:long}")]
    public async Task<IActionResult> Delete(long id) { await _service.DeleteAsync(id); return OkResponse("Reconciliation deleted successfully"); }
}

[Route("api/fin/1201")]
[Route("api/v1/fin/forms/fin1201")]
public class Fin1201Controller : ApiControllerBase
{
    private readonly IFin1201Service _service;
    private readonly IFinPostingService _postingService;
    public Fin1201Controller(IFin1201Service service, IFinPostingService postingService) { _service = service; _postingService = postingService; }

    [HttpGet("posting-events")]
    [HttpGet("events")]
    public async Task<IActionResult> GetEvents([FromQuery] short? status) => OkResponse(await _service.GetOutboxEventsAsync(status));

    [HttpPost("redrive/{eventNo:long}")]
    [HttpPost("events/{eventNo:long}/redrive")]
    public async Task<IActionResult> Redrive(long eventNo) { await _service.RedriveAsync(eventNo); return OkResponse("Event redriven successfully"); }

    [HttpPost("drain")]
    public async Task<IActionResult> Drain() => OkResponse(new { handled = await _postingService.DrainOnceAsync() });
}

[Route("api/fin/1301")]
[Route("api/v1/fin/forms/fin1301")]
public class Fin1301Controller : ApiControllerBase
{
    private readonly IFinReportService _service;
    public Fin1301Controller(IFinReportService service) => _service = service;

    [HttpGet("trial-balance")]
    public async Task<IActionResult> GetTrialBalance(
        [FromQuery] DateTime? asOfDate,
        [FromQuery(Name = "asOf")] DateTime? asOf,
        [FromQuery] long? branchNo)
        => OkResponse(await _service.GetTrialBalanceAsync(asOfDate ?? asOf, branchNo));
}

[Route("api/fin/1302")]
[Route("api/v1/fin/forms/fin1302")]
public class Fin1302Controller : ApiControllerBase
{
    private readonly IFinReportService _service;
    public Fin1302Controller(IFinReportService service) => _service = service;

    [HttpGet("general-ledger")]
    [HttpGet("ledger")]
    public async Task<IActionResult> GetLedger(
        [FromQuery] long accountNo,
        [FromQuery] DateTime fromDate,
        [FromQuery(Name = "from")] DateTime from,
        [FromQuery] DateTime toDate,
        [FromQuery(Name = "to")] DateTime to)
    {
        var fd = fromDate != default ? fromDate : (from != default ? from : DateTime.UtcNow.Date.AddMonths(-1));
        var td = toDate != default ? toDate : (to != default ? to : DateTime.UtcNow.Date);
        return OkResponse(await _service.GetGeneralLedgerAsync(accountNo, fd, td));
    }
}

[Route("api/fin/1303")]
[Route("api/v1/fin/forms/fin1303")]
public class Fin1303Controller : ApiControllerBase
{
    private readonly IFinReportService _service;
    public Fin1303Controller(IFinReportService service) => _service = service;

    [HttpGet("day-book")]
    public async Task<IActionResult> GetDayBook(
        [FromQuery] DateTime fromDate,
        [FromQuery(Name = "from")] DateTime from,
        [FromQuery] DateTime toDate,
        [FromQuery(Name = "to")] DateTime to,
        [FromQuery] long? branchNo)
    {
        var fd = fromDate != default ? fromDate : (from != default ? from : DateTime.UtcNow.Date.AddMonths(-1));
        var td = toDate != default ? toDate : (to != default ? to : DateTime.UtcNow.Date);
        return OkResponse(await _service.GetDayBookAsync(fd, td, branchNo));
    }
}

[Route("api/fin/1304")]
[Route("api/v1/fin/forms/fin1304")]
public class Fin1304Controller : ApiControllerBase
{
    private readonly IFinReportService _service;
    public Fin1304Controller(IFinReportService service) => _service = service;

    [HttpGet("profit-and-loss")]
    [HttpGet("profit-loss")]
    public async Task<IActionResult> GetPnl(
        [FromQuery] DateTime fromDate,
        [FromQuery(Name = "from")] DateTime from,
        [FromQuery] DateTime toDate,
        [FromQuery(Name = "to")] DateTime to,
        [FromQuery] long? branchNo)
    {
        var fd = fromDate != default ? fromDate : (from != default ? from : DateTime.UtcNow.Date.AddMonths(-1));
        var td = toDate != default ? toDate : (to != default ? to : DateTime.UtcNow.Date);
        return OkResponse(await _service.GetPnlAsync(fd, td, branchNo));
    }
}

[Route("api/fin/1305")]
[Route("api/v1/fin/forms/fin1305")]
public class Fin1305Controller : ApiControllerBase
{
    private readonly IFinReportService _service;
    public Fin1305Controller(IFinReportService service) => _service = service;

    [HttpGet("balance-sheet")]
    public async Task<IActionResult> GetBalanceSheet(
        [FromQuery] DateTime asOfDate,
        [FromQuery(Name = "asOf")] DateTime asOf,
        [FromQuery] long? branchNo)
        => OkResponse(await _service.GetBalanceSheetAsync(asOfDate != default ? asOfDate : (asOf != default ? asOf : DateTime.UtcNow.Date), branchNo));
}

[Route("api/fin/1306")]
[Route("api/v1/fin/forms/fin1306")]
public class Fin1306Controller : ApiControllerBase
{
    private readonly IFinReportService _service;
    public Fin1306Controller(IFinReportService service) => _service = service;

    [HttpGet("cash-flow")]
    public async Task<IActionResult> GetCashFlow(
        [FromQuery] DateTime fromDate,
        [FromQuery(Name = "from")] DateTime from,
        [FromQuery] DateTime toDate,
        [FromQuery(Name = "to")] DateTime to,
        [FromQuery] long? branchNo)
    {
        var fd = fromDate != default ? fromDate : (from != default ? from : DateTime.UtcNow.Date.AddMonths(-1));
        var td = toDate != default ? toDate : (to != default ? to : DateTime.UtcNow.Date);
        return OkResponse(await _service.GetCashFlowAsync(fd, td, branchNo));
    }
}

[Route("api/fin/1307")]
[Route("api/v1/fin/forms/fin1307")]
public class Fin1307Controller : ApiControllerBase
{
    private readonly IFinReportService _service;
    public Fin1307Controller(IFinReportService service) => _service = service;

    [HttpGet("aging-analysis")]
    [HttpGet("aging")]
    public async Task<IActionResult> GetAging(
        [FromQuery] short partyType,
        [FromQuery] DateTime asOfDate,
        [FromQuery(Name = "asOf")] DateTime asOf)
        => OkResponse(await _service.GetAgingAsync(partyType, asOfDate != default ? asOfDate : (asOf != default ? asOf : DateTime.UtcNow.Date)));
}

[Route("api/fin/1308")]
[Route("api/v1/fin/forms/fin1308")]
public class Fin1308ChartOfAccountsPdfController : ApiControllerBase
{
    private readonly IFin1308ChartOfAccountsPdfService _service;
    public Fin1308ChartOfAccountsPdfController(IFin1308ChartOfAccountsPdfService service) => _service = service;

    [HttpGet("chart-of-accounts-rows")]
    public async Task<IActionResult> GetRows() => OkResponse(await _service.GetCoaExportRowsAsync());

    [HttpGet("pdf")]
    public async Task<IActionResult> ExportPdf()
    {
        var bytes = await _service.ExportPdfAsync();
        return File(bytes, "application/pdf", "chart-of-accounts.pdf");
    }

    [HttpGet("excel")]
    public async Task<IActionResult> ExportExcel()
    {
        var bytes = await _service.ExportExcelAsync();
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "chart-of-accounts.xlsx");
    }
}

[Route("api/fin/1401")]
[Route("api/v1/fin/forms/fin1401")]
public class Fin1401Controller : ApiControllerBase
{
    private readonly IFin1401Service _service;
    public Fin1401Controller(IFin1401Service service) => _service = service;

    [HttpGet("years")]
    public async Task<IActionResult> GetYears() => OkResponse(await _service.GetYearsAndPeriodsAsync());

    [HttpGet("periods")]
    public async Task<IActionResult> GetPeriods([FromQuery] long finYearNo) => OkResponse(await _service.GetPeriodsAsync(finYearNo));

    [HttpPut("periods/{periodNo:long}/status")]
    public async Task<IActionResult> SetPeriodStatus(long periodNo, [FromQuery] short status, [FromQuery] string? reason)
    {
        await _service.SetPeriodStatusAsync(periodNo, status, reason);
        return OkResponse("Period status updated successfully");
    }

    [HttpPost("close-year")]
    [HttpPost("year-end-close")]
    public async Task<IActionResult> CloseYear([FromBody] Fin1401CloseRequestDto dto) => OkResponse(await _service.CloseFiscalYearAsync(dto));
}

// ═══════════════════════════════════════════════════════════════════════════
// FIN_1202 / FIN_1203 — sub-ledger vs GL control reconciliation
//
// One service, two controllers: AR and AP are the same report with the sign
// flipped, but they are separate forms so each gets its own permission.
// ═══════════════════════════════════════════════════════════════════════════

[Route("api/fin/1202")]
[Route("api/v1/fin/forms/fin1202")]
public class Fin1202Controller : ApiControllerBase
{
    private const short Supplier = 2;

    private readonly IFinSubLedgerService _service;
    public Fin1202Controller(IFinSubLedgerService service) => _service = service;

    /// <summary>Payables per supplier, PUR's sub-ledger against the AP control account.</summary>
    [HttpGet("reconciliation")]
    public async Task<IActionResult> GetReconciliation([FromQuery] DateTime? asOf)
        => OkResponse(await _service.GetReconciliationAsync(Supplier, asOf ?? DateTime.UtcNow.Date));

    /// <summary>One supplier's movements over a window, with a running balance.</summary>
    [HttpGet("statement")]
    public async Task<IActionResult> GetStatement([FromQuery] long partyNo, [FromQuery] DateTime from,
                                                  [FromQuery] DateTime to)
        => OkResponse(await _service.GetStatementAsync(Supplier, partyNo, from, to));
}

[Route("api/fin/1203")]
[Route("api/v1/fin/forms/fin1203")]
public class Fin1203Controller : ApiControllerBase
{
    private const short Customer = 1;

    private readonly IFinSubLedgerService _service;
    public Fin1203Controller(IFinSubLedgerService service) => _service = service;

    /// <summary>Receivables per customer, SAL's sub-ledger against the AR control account.</summary>
    [HttpGet("reconciliation")]
    public async Task<IActionResult> GetReconciliation([FromQuery] DateTime? asOf)
        => OkResponse(await _service.GetReconciliationAsync(Customer, asOf ?? DateTime.UtcNow.Date));

    /// <summary>One customer's movements over a window, with a running balance.</summary>
    [HttpGet("statement")]
    public async Task<IActionResult> GetStatement([FromQuery] long partyNo, [FromQuery] DateTime from,
                                                  [FromQuery] DateTime to)
        => OkResponse(await _service.GetStatementAsync(Customer, partyNo, from, to));
}
