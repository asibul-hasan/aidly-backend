using Microsoft.AspNetCore.Mvc;
using AidlyErp.Hrm.Application.Dto;
using AidlyErp.Hrm.Application.Services;
using AidlyErp.Hrm.Domain;

namespace AidlyErp.Api.Controllers;

// ═══════════════════════════════════════════════════════════════════════════
// HRM Controllers — full CRUD + workflow endpoints matching Java 1:1
// ═══════════════════════════════════════════════════════════════════════════

// ── Setup (1000-series) ──────────────────────────────────────────────────

[Route("api/hrm/1003")]
[Route("api/v1/hrm/forms/hrm1003")]
public class Hrm1003Controller : ApiControllerBase
{
    private readonly IHrm1003Service _service;
    public Hrm1003Controller(IHrm1003Service service) => _service = service;

    [HttpGet("shifts")]
    public async Task<IActionResult> GetList() => OkResponse(await _service.GetListAsync());

    [HttpGet("shifts/branch/{branchNo:long}")]
    public async Task<IActionResult> GetByBranch(long branchNo) => OkResponse(await _service.GetListByBranchAsync(branchNo));

    [HttpGet("shifts/{no:long}")]
    public async Task<IActionResult> GetDetail(long no) => OkResponse(await _service.GetDetailAsync(no));

    [HttpPost("shifts")]
    public async Task<IActionResult> Save([FromBody] Hrm1003ShiftDto dto) => OkResponse(await _service.SaveAsync(dto), dto.ShiftNo.HasValue ? "Shift updated" : "Shift created");

    [HttpDelete("shifts/{no:long}")]
    public async Task<IActionResult> Delete(long no) { await _service.DeleteAsync(no); return OkResponse<object?>(null, "Shift deleted"); }
}

[Route("api/hrm/1004")]
[Route("api/v1/hrm/forms/hrm1004")]
public class Hrm1004Controller : ApiControllerBase
{
    private readonly IHrm1004Service _service;
    public Hrm1004Controller(IHrm1004Service service) => _service = service;

    [HttpGet("grades")]
    public async Task<IActionResult> GetList() => OkResponse(await _service.GetListAsync());

    [HttpGet("grades/branch/{branchNo:long}")]
    public async Task<IActionResult> GetByBranch(long branchNo) => OkResponse(await _service.GetListByBranchAsync(branchNo));

    [HttpGet("grades/{no:long}")]
    public async Task<IActionResult> GetDetail(long no) => OkResponse(await _service.GetDetailAsync(no));

    [HttpPost("grades")]
    public async Task<IActionResult> Save([FromBody] Hrm1004GradeDto dto) => OkResponse(await _service.SaveAsync(dto), dto.GradeNo.HasValue ? "Grade updated" : "Grade created");

    [HttpDelete("grades/{no:long}")]
    public async Task<IActionResult> Delete(long no) { await _service.DeleteAsync(no); return OkResponse<object?>(null, "Grade deleted"); }
}

[Route("api/hrm/1005")]
[Route("api/v1/hrm/forms/hrm1005")]
public class Hrm1005Controller : ApiControllerBase
{
    private readonly IHrm1005Service _service;
    public Hrm1005Controller(IHrm1005Service service) { _service = service; }

    [HttpGet("leave-types")]
    public async Task<IActionResult> GetList() => OkResponse(await _service.GetListAsync());

    [HttpGet("leave-types/branch/{branchNo:long}")]
    public async Task<IActionResult> GetByBranch(long branchNo) => OkResponse(await _service.GetListByBranchAsync(branchNo));

    [HttpGet("leave-types/{no:long}")]
    public async Task<IActionResult> GetDetail(long no) => OkResponse(await _service.GetDetailAsync(no));

    [HttpPost("leave-types")]
    public async Task<IActionResult> Save([FromBody] Hrm1005LeaveTypeDto dto) => OkResponse(await _service.SaveAsync(dto), dto.LeaveTypeNo.HasValue && dto.LeaveTypeNo > 0 ? "Leave type updated" : "Leave type created");

    [HttpPost("leave-types/bulk")]
    public async Task<IActionResult> SaveBulk([FromBody] List<Hrm1005LeaveTypeDto> dtoList) => OkResponse(await _service.SaveBulkAsync(dtoList), "Leave types saved successfully");

    [HttpDelete("leave-types/{no:long}")]
    public async Task<IActionResult> Delete(long no) { await _service.DeleteAsync(no); return OkResponse<object?>(null, "Leave type deleted"); }

    // ── Leave Policy Setup (child endpoints) ─────────────────────────────

    [HttpGet("policies")]
    public async Task<IActionResult> GetPolicyList() => OkResponse(await _service.GetPolicyListAsync());

    [HttpGet("policies/{policyNo:long}")]
    public async Task<IActionResult> GetPolicyDetail(long policyNo) => OkResponse(await _service.GetPolicyDetailAsync(policyNo));

    [HttpPost("policies")]
    public async Task<IActionResult> SavePolicy([FromBody] HrmLeavePolicySetupDto dto) => OkResponse(await _service.SavePolicyAsync(dto), dto.PolicyNo > 0 ? "Policy updated" : "Policy created");

    [HttpDelete("policies/{policyNo:long}")]
    public async Task<IActionResult> DeletePolicy(long policyNo) { await _service.DeletePolicyAsync(policyNo); return OkResponse<object?>(null, "Policy deleted"); }
}

[Route("api/hrm/1006")]
[Route("api/v1/hrm/forms/hrm1006")]
public class Hrm1006Controller : ApiControllerBase
{
    private readonly IHrm1006Service _service;
    public Hrm1006Controller(IHrm1006Service service) => _service = service;

    [HttpGet("holidays")]
    public async Task<IActionResult> GetList() => OkResponse(await _service.GetListAsync());

    [HttpGet("holidays/branch/{branchNo:long}")]
    public async Task<IActionResult> GetByBranch(long branchNo) => OkResponse(await _service.GetListByBranchAsync(branchNo));

    [HttpGet("holidays/{no:long}")]
    public async Task<IActionResult> GetDetail(long no) => OkResponse(await _service.GetDetailAsync(no));

    [HttpPost("holidays")]
    public async Task<IActionResult> Save([FromBody] HrmHoliday dto) => OkResponse(await _service.SaveAsync(dto), dto.HolidayNo > 0 ? "Holiday updated" : "Holiday created");

    [HttpDelete("holidays/{no:long}")]
    public async Task<IActionResult> Delete(long no) { await _service.DeleteAsync(no); return OkResponse<object?>(null, "Holiday deleted"); }
}

[Route("api/hrm/1007")]
[Route("api/v1/hrm/forms/hrm1007")]
public class Hrm1007Controller : ApiControllerBase
{
    private readonly IHrm1007Service _service;
    public Hrm1007Controller(IHrm1007Service service) => _service = service;

    [HttpGet("salary-components")]
    public async Task<IActionResult> GetList() => OkResponse(await _service.GetListAsync());

    [HttpGet("salary-components/{no:long}")]
    public async Task<IActionResult> GetDetail(long no) => OkResponse(await _service.GetDetailAsync(no));

    [HttpPost("salary-components")]
    public async Task<IActionResult> Save([FromBody] HrmSalaryComponent dto) => OkResponse(await _service.SaveAsync(dto), dto.ComponentNo > 0 ? "Component updated" : "Component created");

    [HttpDelete("salary-components/{no:long}")]
    public async Task<IActionResult> Delete(long no) { await _service.DeleteAsync(no); return OkResponse<object?>(null, "Component deleted"); }
}

[Route("api/hrm/1008")]
[Route("api/v1/hrm/forms/hrm1008")]
public class Hrm1008Controller : ApiControllerBase
{
    private readonly IHrm1008Service _service;
    public Hrm1008Controller(IHrm1008Service service) => _service = service;

    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings() => OkResponse(await _service.GetSettingsAsync());

    [HttpPost("settings")]
    public async Task<IActionResult> SaveSettings([FromBody] Hrm1008SettingsDto dto) => OkResponse(await _service.SaveSettingsAsync(dto), "HR settings saved");
}

[Route("api/hrm/1009")]
[Route("api/v1/hrm/forms/hrm1009")]
public class Hrm1009Controller : ApiControllerBase
{
    private readonly IHrm1009Service _service;
    public Hrm1009Controller(IHrm1009Service service) => _service = service;

    [HttpGet("tax-slabs")]
    public async Task<IActionResult> GetList() => OkResponse(await _service.GetListAsync());

    [HttpGet("tax-slabs/{no:long}")]
    public async Task<IActionResult> GetDetail(long no) => OkResponse(await _service.GetDetailAsync(no));

    [HttpPost("tax-slabs")]
    public async Task<IActionResult> Save([FromBody] Hrm1009TaxSlabDto dto) => OkResponse(await _service.SaveAsync(dto), dto.TaxSlabNo.HasValue ? "Tax slab updated" : "Tax slab created");

    [HttpDelete("tax-slabs/{no:long}")]
    public async Task<IActionResult> Delete(long no) { await _service.DeleteAsync(no); return OkResponse<object?>(null, "Tax slab deleted"); }
}

// ── Attendance (1100-series) ─────────────────────────────────────────────

[Route("api/hrm/1101")]
[Route("api/v1/hrm/forms/hrm1101")]
public class Hrm1101Controller : ApiControllerBase
{
    private readonly IHrm1101Service _service;
    public Hrm1101Controller(IHrm1101Service service) => _service = service;

    [HttpGet("attendances")]
    public async Task<IActionResult> GetList([FromQuery] long? branchNo, [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate) =>
        OkResponse(await _service.GetListAsync(branchNo, fromDate, toDate));

    [HttpGet("attendances/{no:long}")]
    public async Task<IActionResult> GetDetail(long no) => OkResponse(await _service.GetDetailAsync(no));

    [HttpPost("attendances")]
    public async Task<IActionResult> Save([FromBody] HrmAttendance dto) => OkResponse(await _service.SaveAsync(dto), dto.AttendanceNo > 0 ? "Attendance updated" : "Attendance created");

    [HttpDelete("attendances/{no:long}")]
    public async Task<IActionResult> Delete(long no) { await _service.DeleteAsync(no); return OkResponse<object?>(null, "Attendance deleted"); }
}

[Route("api/hrm/1102")]
[Route("api/v1/hrm/forms/hrm1102")]
public class Hrm1102Controller : ApiControllerBase
{
    private readonly IHrm1102Service _service;
    public Hrm1102Controller(IHrm1102Service service) => _service = service;

    [HttpPost("sync")]
    [HttpPost("sync-punches")]
    public async Task<IActionResult> Sync([FromBody] Hrm1102SyncRequestDto dto)
    {
        var res = await _service.SyncAsync(dto);
        return OkResponse(res, $"Synced {res.Total} punch(es): {res.Created} created, {res.Updated} updated, {res.Skipped} skipped");
    }

    [HttpGet("recent")]
    public async Task<IActionResult> GetRecent() => OkResponse(await _service.GetRecentAsync());
}

[Route("api/hrm/1103")]
[Route("api/v1/hrm/forms/hrm1103")]
public class Hrm1103Controller : ApiControllerBase
{
    private readonly IHrm1103Service _service;
    public Hrm1103Controller(IHrm1103Service service) => _service = service;

    [HttpGet("adjustments")]
    public async Task<IActionResult> GetList() => OkResponse(await _service.GetListAsync());

    [HttpGet("adjustments/{no:long}")]
    public async Task<IActionResult> GetDetail(long no) => OkResponse(await _service.GetDetailAsync(no));

    [HttpPost("adjustments")]
    public async Task<IActionResult> Save([FromBody] HrmAttendanceAdjustment dto) => OkResponse(await _service.SaveAsync(dto), dto.AdjustmentNo > 0 ? "Adjustment updated" : "Adjustment created");

    [HttpPost("adjustments/{no:long}/approve")]
    public async Task<IActionResult> Approve(long no) => OkResponse(await _service.ApproveAsync(no), "Adjustment approved");

    [HttpPost("adjustments/{no:long}/reject")]
    public async Task<IActionResult> Reject(long no, [FromQuery] string? reason) => OkResponse(await _service.RejectAsync(no, reason), "Adjustment rejected");

    [HttpDelete("adjustments/{no:long}")]
    public async Task<IActionResult> Delete(long no) { await _service.DeleteAsync(no); return OkResponse<object?>(null, "Adjustment deleted"); }
}

[Route("api/hrm/1104")]
[Route("api/v1/hrm/forms/hrm1104")]
public class Hrm1104Controller : ApiControllerBase
{
    private readonly IHrm1104Service _service;
    public Hrm1104Controller(IHrm1104Service service) => _service = service;

    [HttpGet("rosters")]
    public async Task<IActionResult> GetList() => OkResponse(await _service.GetListAsync());

    [HttpGet("rosters/{no:long}")]
    public async Task<IActionResult> GetDetail(long no) => OkResponse(await _service.GetDetailAsync(no));

    [HttpPost("rosters")]
    public async Task<IActionResult> Save([FromBody] HrmShiftRoster dto) => OkResponse(await _service.SaveAsync(dto), dto.RosterNo > 0 ? "Roster updated" : "Roster created");

    [HttpPost("rosters/{no:long}/publish")]
    public async Task<IActionResult> Publish(long no) => OkResponse(await _service.PublishAsync(no), "Roster published");

    [HttpPost("rosters/{no:long}/cancel")]
    public async Task<IActionResult> Cancel(long no) => OkResponse(await _service.CancelAsync(no), "Roster cancelled");

    [HttpDelete("rosters/{no:long}")]
    public async Task<IActionResult> Delete(long no) { await _service.DeleteAsync(no); return OkResponse<object?>(null, "Roster deleted"); }
}

[Route("api/hrm/1105")]
[Route("api/v1/hrm/forms/hrm1105")]
public class Hrm1105Controller : ApiControllerBase
{
    private readonly IHrm1105Service _service;
    public Hrm1105Controller(IHrm1105Service service) => _service = service;

    [HttpGet("overtimes")]
    public async Task<IActionResult> GetList() => OkResponse(await _service.GetListAsync());

    [HttpGet("overtimes/{no:long}")]
    public async Task<IActionResult> GetDetail(long no) => OkResponse(await _service.GetDetailAsync(no));

    [HttpPost("overtimes")]
    public async Task<IActionResult> Save([FromBody] HrmOvertime dto) => OkResponse(await _service.SaveAsync(dto), dto.OvertimeNo > 0 ? "Overtime updated" : "Overtime created");

    [HttpPost("overtimes/{no:long}/submit")]
    public async Task<IActionResult> Submit(long no) => OkResponse(await _service.SubmitAsync(no), "Overtime submitted");

    [HttpPost("overtimes/{no:long}/approve")]
    public async Task<IActionResult> Approve(long no) => OkResponse(await _service.ApproveAsync(no), "Overtime approved");

    [HttpPost("overtimes/{no:long}/reject")]
    public async Task<IActionResult> Reject(long no, [FromQuery] string? reason) => OkResponse(await _service.RejectAsync(no, reason), "Overtime rejected");

    [HttpPost("overtimes/{no:long}/cancel")]
    public async Task<IActionResult> Cancel(long no) => OkResponse(await _service.CancelAsync(no), "Overtime cancelled");

    [HttpDelete("overtimes/{no:long}")]
    public async Task<IActionResult> Delete(long no) { await _service.DeleteAsync(no); return OkResponse<object?>(null, "Overtime deleted"); }
}

[Route("api/hrm/1106")]
[Route("api/v1/hrm/forms/hrm1106")]
public class Hrm1106Controller : ApiControllerBase
{
    private readonly IHrm1106Service _service;
    public Hrm1106Controller(IHrm1106Service service) => _service = service;

    [HttpGet("movements")]
    public async Task<IActionResult> GetList() => OkResponse(await _service.GetListAsync());

    [HttpGet("movements/employee/{employeeNo:long}")]
    public async Task<IActionResult> GetByEmployee(long employeeNo) => OkResponse(await _service.GetListByEmployeeAsync(employeeNo));

    [HttpGet("movements/{no:long}")]
    public async Task<IActionResult> GetDetail(long no) => OkResponse(await _service.GetDetailAsync(no));

    [HttpPost("movements")]
    public async Task<IActionResult> Save([FromBody] HrmEmployeeMovement dto) => OkResponse(await _service.SaveAsync(dto), dto.MovementNo > 0 ? "Movement updated" : "Movement created");

    [HttpPost("movements/{no:long}/submit")]
    public async Task<IActionResult> Submit(long no) => OkResponse(await _service.SubmitAsync(no), "Movement submitted");

    [HttpPost("movements/{no:long}/approve")]
    public async Task<IActionResult> Approve(long no) => OkResponse(await _service.ApproveAsync(no), "Movement approved");

    [HttpPost("movements/{no:long}/reject")]
    public async Task<IActionResult> Reject(long no, [FromQuery] string? reason) => OkResponse(await _service.RejectAsync(no, reason), "Movement rejected");

    [HttpPost("movements/{no:long}/cancel")]
    public async Task<IActionResult> Cancel(long no) => OkResponse(await _service.CancelAsync(no), "Movement cancelled");

    [HttpDelete("movements/{no:long}")]
    public async Task<IActionResult> Delete(long no) { await _service.DeleteAsync(no); return OkResponse<object?>(null, "Movement deleted"); }
}

// ── Payroll (1200-series) ────────────────────────────────────────────────

[Route("api/hrm/1201")]
[Route("api/v1/hrm/forms/hrm1201")]
public class Hrm1201Controller : ApiControllerBase
{
    private readonly IHrm1201Service _service;
    public Hrm1201Controller(IHrm1201Service service) => _service = service;

    [HttpGet("salary-structures")]
    public async Task<IActionResult> GetList() => OkResponse(await _service.GetListAsync());

    [HttpGet("salary-structures/employee/{employeeNo:long}")]
    public async Task<IActionResult> GetByEmployee(long employeeNo) => OkResponse(await _service.GetListByEmployeeAsync(employeeNo));

    [HttpGet("salary-structures/{no:long}")]
    public async Task<IActionResult> GetDetail(long no) => OkResponse(await _service.GetDetailAsync(no));

    [HttpPost("salary-structures")]
    public async Task<IActionResult> Save([FromBody] Hrm1201SalaryStructureDto dto) => OkResponse(await _service.SaveAsync(dto), dto.SalaryStructureNo.HasValue ? "Structure updated" : "Structure created");

    [HttpDelete("salary-structures/{no:long}")]
    public async Task<IActionResult> Delete(long no) { await _service.DeleteAsync(no); return OkResponse<object?>(null, "Structure deleted"); }
}

[Route("api/hrm/1202")]
[Route("api/v1/hrm/forms/hrm1202")]
public class Hrm1202Controller : ApiControllerBase
{
    private readonly IHrm1202Service _service;
    public Hrm1202Controller(IHrm1202Service service) => _service = service;

    [HttpGet("runs")]
    public async Task<IActionResult> GetList() => OkResponse(await _service.GetListAsync());

    [HttpGet("runs/{no:long}")]
    public async Task<IActionResult> GetDetail(long no) => OkResponse(await _service.GetDetailAsync(no));

    [HttpPost("runs")]
    public async Task<IActionResult> Create([FromBody] Hrm1202PayrollRunDto dto) => OkResponse(await _service.CreateRunAsync(dto), "Payroll run created");

    [HttpPost("runs/{no:long}/calculate")]
    public async Task<IActionResult> Calculate(long no) => OkResponse(await _service.CalculateAsync(no), "Payroll calculated");

    [HttpPost("runs/{no:long}/approve")]
    public async Task<IActionResult> Approve(long no) => OkResponse(await _service.ApproveAsync(no), "Payroll approved");

    [HttpPost("runs/{no:long}/pay")]
    public async Task<IActionResult> MarkPaid(long no) => OkResponse(await _service.MarkPaidAsync(no), "Payroll marked paid");

    [HttpPost("runs/{no:long}/cancel")]
    public async Task<IActionResult> Cancel(long no) => OkResponse(await _service.CancelAsync(no), "Payroll run cancelled");

    [HttpDelete("runs/{no:long}")]
    public async Task<IActionResult> Delete(long no) { await _service.DeleteAsync(no); return OkResponse<object?>(null, "Payroll run deleted"); }
}

[Route("api/hrm/1203")]
[Route("api/v1/hrm/forms/hrm1203")]
public class Hrm1203Controller : ApiControllerBase
{
    private readonly IHrm1203Service _service;
    public Hrm1203Controller(IHrm1203Service service) => _service = service;

    [HttpGet("runs")]
    public async Task<IActionResult> GetRuns() => OkResponse(await _service.GetRunsAsync());

    [HttpGet("sheet/{payrollRunNo:long}")]
    public async Task<IActionResult> GetSheet(long payrollRunNo) => OkResponse(await _service.GetSheetAsync(payrollRunNo));
}

[Route("api/hrm/1204")]
[Route("api/v1/hrm/forms/hrm1204")]
public class Hrm1204Controller : ApiControllerBase
{
    private readonly IHrm1204Service _service;
    public Hrm1204Controller(IHrm1204Service service) => _service = service;

    [HttpGet("runs")]
    public async Task<IActionResult> GetRuns() => OkResponse(await _service.GetRunsAsync());

    [HttpGet("payslips")]
    public async Task<IActionResult> GetPayslips([FromQuery] long runNo) => OkResponse(await _service.GetPayslipsAsync(runNo));

    [HttpGet("payslips/{id:long}")]
    public async Task<IActionResult> GetPayslipDetail(long id) => OkResponse(await _service.GetPayslipDetailAsync(id));
}

[Route("api/hrm/1205")]
[Route("api/v1/hrm/forms/hrm1205")]
public class Hrm1205Controller : ApiControllerBase
{
    private readonly IHrm1205Service _service;
    public Hrm1205Controller(IHrm1205Service service) => _service = service;

    [HttpGet("runs")]
    public async Task<IActionResult> GetList() => OkResponse(await _service.GetListAsync());

    [HttpGet("runs/{no:long}")]
    public async Task<IActionResult> GetDetail(long no) => OkResponse(await _service.GetDetailAsync(no));

    [HttpPost("runs")]
    public async Task<IActionResult> Save([FromBody] HrmBonusRun dto) => OkResponse(await _service.SaveAsync(dto), dto.BonusRunNo > 0 ? "Bonus run updated" : "Bonus run created");

    [HttpPost("runs/{no:long}/calculate")]
    public async Task<IActionResult> Calculate(long no) => OkResponse(await _service.CalculateAsync(no), "Bonus calculated");

    [HttpPost("runs/{no:long}/approve")]
    public async Task<IActionResult> Approve(long no) => OkResponse(await _service.ApproveAsync(no), "Bonus approved");

    [HttpPost("runs/{no:long}/reject")]
    public async Task<IActionResult> Reject(long no) => OkResponse(await _service.RejectAsync(no), "Bonus rejected");

    [HttpPost("runs/{no:long}/pay")]
    public async Task<IActionResult> MarkPaid(long no) => OkResponse(await _service.MarkPaidAsync(no), "Bonus marked paid");

    [HttpPost("runs/{no:long}/cancel")]
    public async Task<IActionResult> Cancel(long no) => OkResponse(await _service.CancelAsync(no), "Bonus cancelled");

    [HttpDelete("runs/{no:long}")]
    public async Task<IActionResult> Delete(long no) { await _service.DeleteAsync(no); return OkResponse<object?>(null, "Bonus run deleted"); }
}

[Route("api/hrm/1206")]
[Route("api/v1/hrm/forms/hrm1206")]
public class Hrm1206Controller : ApiControllerBase
{
    private readonly IHrm1206Service _service;
    public Hrm1206Controller(IHrm1206Service service) => _service = service;

    [HttpGet("loans")]
    public async Task<IActionResult> GetList() => OkResponse(await _service.GetListAsync());

    [HttpGet("loans/{no:long}")]
    public async Task<IActionResult> GetDetail(long no) => OkResponse(await _service.GetDetailAsync(no));

    [HttpPost("loans")]
    public async Task<IActionResult> Save([FromBody] Hrm1206LoanDto dto) => OkResponse(await _service.SaveAsync(dto), dto.LoanNo > 0 ? "Loan updated" : "Loan created");

    [HttpPost("loans/{no:long}/submit")]
    public async Task<IActionResult> Submit(long no) => OkResponse(await _service.SubmitAsync(no), "Loan submitted");

    [HttpPost("loans/{no:long}/approve")]
    public async Task<IActionResult> Approve(long no) => OkResponse(await _service.ApproveAsync(no), "Loan approved");

    [HttpPost("loans/{no:long}/reject")]
    public async Task<IActionResult> Reject(long no) => OkResponse(await _service.RejectAsync(no), "Loan rejected");

    [HttpPost("loans/{no:long}/disburse")]
    public async Task<IActionResult> Disburse(long no) => OkResponse(await _service.DisburseAsync(no), "Loan disbursed");

    [HttpPost("loans/{no:long}/recover")]
    public async Task<IActionResult> Recover(long no, [FromQuery] decimal amount) => OkResponse(await _service.RecordRecoveryAsync(no, amount), "Recovery recorded");

    [HttpPost("loans/{no:long}/cancel")]
    public async Task<IActionResult> Cancel(long no) => OkResponse(await _service.CancelAsync(no), "Loan cancelled");

    [HttpDelete("loans/{no:long}")]
    public async Task<IActionResult> Delete(long no) { await _service.DeleteAsync(no); return OkResponse<object?>(null, "Loan deleted"); }
}

[Route("api/hrm/1207")]
[Route("api/v1/hrm/forms/hrm1207")]
public class Hrm1207Controller : ApiControllerBase
{
    private readonly IHrm1207Service _service;
    public Hrm1207Controller(IHrm1207Service service) => _service = service;

    [HttpGet("settlements")]
    public async Task<IActionResult> GetList() => OkResponse(await _service.GetListAsync());

    [HttpGet("settlements/{no:long}")]
    public async Task<IActionResult> GetDetail(long no) => OkResponse(await _service.GetDetailAsync(no));

    [HttpPost("settlements")]
    public async Task<IActionResult> Save([FromBody] Hrm1207SettlementDto dto) => OkResponse(await _service.SaveAsync(dto), dto.SettlementNo.HasValue ? "Settlement updated" : "Settlement created");

    [HttpPost("settlements/{no:long}/submit")]
    public async Task<IActionResult> Submit(long no) => OkResponse(await _service.SubmitAsync(no), "Settlement submitted");

    [HttpPost("settlements/{no:long}/approve")]
    public async Task<IActionResult> Approve(long no) => OkResponse(await _service.ApproveAsync(no), "Settlement approved");

    [HttpPost("settlements/{no:long}/reject")]
    public async Task<IActionResult> Reject(long no) => OkResponse(await _service.RejectAsync(no), "Settlement rejected");

    [HttpPost("settlements/{no:long}/pay")]
    public async Task<IActionResult> MarkPaid(long no) => OkResponse(await _service.MarkPaidAsync(no), "Settlement marked paid");

    [HttpPost("settlements/{no:long}/cancel")]
    public async Task<IActionResult> Cancel(long no) => OkResponse(await _service.CancelAsync(no), "Settlement cancelled");

    [HttpDelete("settlements/{no:long}")]
    public async Task<IActionResult> Delete(long no) { await _service.DeleteAsync(no); return OkResponse<object?>(null, "Settlement deleted"); }
}

// ── Leave (1300-series) ──────────────────────────────────────────────────

[Route("api/hrm/1301")]
[Route("api/v1/hrm/forms/hrm1301")]
public class Hrm1301Controller : ApiControllerBase
{
    private readonly IHrm1301Service _service;
    public Hrm1301Controller(IHrm1301Service service) => _service = service;

    [HttpGet("leaves")]
    public async Task<IActionResult> GetList() => OkResponse(await _service.GetListAsync());

    [HttpGet("leaves/employee/{employeeNo:long}")]
    public async Task<IActionResult> GetByEmployee(long employeeNo) => OkResponse(await _service.GetListByEmployeeAsync(employeeNo));

    [HttpGet("leaves/filtered")]
    public async Task<IActionResult> GetFilteredHistory([FromQuery] long employeeNo, [FromQuery] long? leaveTypeNo, [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate) =>
        OkResponse(await _service.GetFilteredHistoryAsync(employeeNo, leaveTypeNo, fromDate, toDate));

    [HttpGet("leaves/{no:long}")]
    public async Task<IActionResult> GetDetail(long no) => OkResponse(await _service.GetDetailAsync(no));

    [HttpGet("leaves/status")]
    public async Task<IActionResult> GetApprovalList(
        [FromQuery] long? branchNo,
        [FromQuery(Name = "department_no")] long? departmentNo,
        [FromQuery(Name = "designation_no")] long? designationNo,
        [FromQuery(Name = "employee_no")] long? employeeNo,
        [FromQuery(Name = "leave_type_no")] long? leaveTypeNo,
        [FromQuery(Name = "status")] int? status) =>
        OkResponse(await _service.GetApprovalListAsync(branchNo, departmentNo, designationNo, employeeNo, leaveTypeNo, status));

    [HttpGet("balance")]
    public async Task<IActionResult> GetBalance([FromQuery] long employeeNo, [FromQuery] int? leaveYear) => OkResponse(await _service.GetBalanceAsync(employeeNo, leaveYear));

    [HttpPost("leaves")]
    public async Task<IActionResult> Save([FromBody] Hrm1301LeaveApplicationDto dto) => OkResponse(await _service.SaveAsync(dto), dto.LeaveApplicationNo.HasValue ? "Leave updated" : "Leave created");

    [HttpPost("leaves/{no:long}/submit")]
    public async Task<IActionResult> Submit(long no) => OkResponse(await _service.SubmitAsync(no), "Leave submitted");

    [HttpPost("leaves/{no:long}/approve")]
    public async Task<IActionResult> Approve(long no, [FromQuery] string? remarks) => OkResponse(await _service.ApproveAsync(no, remarks), "Leave approved");

    [HttpPost("leaves/{no:long}/reject")]
    public async Task<IActionResult> Reject(long no, [FromQuery] string? reason) => OkResponse(await _service.RejectAsync(no, reason), "Leave rejected");

    [HttpPost("leaves/{no:long}/cancel")]
    public async Task<IActionResult> Cancel(long no) => OkResponse(await _service.CancelAsync(no), "Leave cancelled");

    [HttpPut("leaves/{no:long}/reliever-status")]
    public async Task<IActionResult> UpdateRelieverStatus(long no, [FromBody] RelieverStatusRequest request) =>
        OkResponse(await _service.UpdateRelieverStatusAsync(no, request.Status, request.Remarks), "Reliever status updated");

    [HttpDelete("leaves/{no:long}")]
    public async Task<IActionResult> Delete(long no) { await _service.DeleteAsync(no); return OkResponse<object?>(null, "Leave deleted"); }
}

public record RelieverStatusRequest(short Status, string? Remarks);

[Route("api/hrm/1303")]
[Route("api/v1/hrm/forms/hrm1303")]
public class Hrm1303Controller : ApiControllerBase
{
    private readonly IHrm1303Service _service;
    public Hrm1303Controller(IHrm1303Service service) => _service = service;

    [HttpGet("balances")]
    public async Task<IActionResult> GetBalances([FromQuery] long employeeNo) => OkResponse(await _service.GetBalancesAsync(employeeNo));
}

// ── Recruitment (1400-series) ────────────────────────────────────────────

[Route("api/hrm/1401")]
[Route("api/v1/hrm/forms/hrm1401")]
public class Hrm1401Controller : ApiControllerBase
{
    private readonly IHrm1401Service _service;
    public Hrm1401Controller(IHrm1401Service service) => _service = service;

    [HttpGet("requisitions")]
    public async Task<IActionResult> GetList() => OkResponse(await _service.GetListAsync());

    [HttpGet("requisitions/branch/{branchNo:long}")]
    public async Task<IActionResult> GetByBranch(long branchNo) => OkResponse(await _service.GetListByBranchAsync(branchNo));

    [HttpGet("requisitions/{no:long}")]
    public async Task<IActionResult> GetDetail(long no) => OkResponse(await _service.GetDetailAsync(no));

    [HttpPost("requisitions")]
    public async Task<IActionResult> Save([FromBody] HrmJobRequisition dto) => OkResponse(await _service.SaveAsync(dto), dto.RequisitionNo > 0 ? "Requisition updated" : "Requisition created");

    [HttpPost("requisitions/{no:long}/submit")]
    public async Task<IActionResult> Submit(long no) => OkResponse(await _service.SubmitAsync(no), "Requisition submitted");

    [HttpPost("requisitions/{no:long}/approve")]
    public async Task<IActionResult> Approve(long no) => OkResponse(await _service.ApproveAsync(no), "Requisition approved");

    [HttpPost("requisitions/{no:long}/reject")]
    public async Task<IActionResult> Reject(long no, [FromQuery] string? reason) => OkResponse(await _service.RejectAsync(no, reason), "Requisition rejected");

    [HttpPost("requisitions/{no:long}/hold")]
    public async Task<IActionResult> Hold(long no) => OkResponse(await _service.HoldAsync(no), "Requisition put on hold");

    [HttpPost("requisitions/{no:long}/resume")]
    public async Task<IActionResult> Resume(long no) => OkResponse(await _service.ResumeAsync(no), "Requisition resumed");

    [HttpPost("requisitions/{no:long}/close")]
    public async Task<IActionResult> Close(long no) => OkResponse(await _service.CloseAsync(no), "Requisition closed");

    [HttpPost("requisitions/{no:long}/cancel")]
    public async Task<IActionResult> Cancel(long no) => OkResponse(await _service.CancelAsync(no), "Requisition cancelled");

    [HttpDelete("requisitions/{no:long}")]
    public async Task<IActionResult> Delete(long no) { await _service.DeleteAsync(no); return OkResponse<object?>(null, "Requisition deleted"); }
}

[Route("api/hrm/1402")]
[Route("api/v1/hrm/forms/hrm1402")]
public class Hrm1402Controller : ApiControllerBase
{
    private readonly IHrm1402Service _service;
    public Hrm1402Controller(IHrm1402Service service) => _service = service;

    [HttpGet("candidates")]
    public async Task<IActionResult> GetList() => OkResponse(await _service.GetListAsync());

    [HttpGet("candidates/branch/{branchNo:long}")]
    public async Task<IActionResult> GetByBranch(long branchNo) => OkResponse(await _service.GetListByBranchAsync(branchNo));

    [HttpGet("candidates/offerable")]
    public async Task<IActionResult> GetOfferable() => OkResponse(await _service.GetOfferableAsync());

    [HttpGet("candidates/{no:long}")]
    public async Task<IActionResult> GetDetail(long no) => OkResponse(await _service.GetDetailAsync(no));

    [HttpPost("candidates")]
    public async Task<IActionResult> Save([FromBody] HrmCandidate dto) => OkResponse(await _service.SaveAsync(dto), dto.CandidateNo > 0 ? "Candidate updated" : "Candidate created");

    [HttpPost("candidates/{no:long}/mark-offered")]
    public async Task<IActionResult> MarkOffered(long no) => OkResponse(await _service.MarkOfferedAsync(no), "Candidate marked as offered");

    [HttpPost("candidates/{no:long}/mark-hired")]
    public async Task<IActionResult> MarkHired(long no) => OkResponse(await _service.MarkHiredAsync(no), "Candidate marked as hired");

    [HttpDelete("candidates/{no:long}")]
    public async Task<IActionResult> Delete(long no) { await _service.DeleteAsync(no); return OkResponse<object?>(null, "Candidate deleted"); }
}

[Route("api/hrm/1404")]
[Route("api/v1/hrm/forms/hrm1404")]
public class Hrm1404Controller : ApiControllerBase
{
    private readonly IHrm1404Service _service;
    public Hrm1404Controller(IHrm1404Service service) => _service = service;

    [HttpGet("offers")]
    public async Task<IActionResult> GetList() => OkResponse(await _service.GetListAsync());

    [HttpGet("offers/{no:long}")]
    public async Task<IActionResult> GetDetail(long no) => OkResponse(await _service.GetDetailAsync(no));

    [HttpPost("offers")]
    public async Task<IActionResult> Save([FromBody] HrmOffer dto) => OkResponse(await _service.SaveAsync(dto), dto.OfferNo > 0 ? "Offer updated" : "Offer created");

    [HttpPost("offers/{no:long}/send")]
    public async Task<IActionResult> Send(long no) => OkResponse(await _service.SendAsync(no), "Offer sent");

    [HttpPost("offers/{no:long}/accept")]
    public async Task<IActionResult> Accept(long no) => OkResponse(await _service.AcceptAsync(no), "Offer accepted");

    [HttpPost("offers/{no:long}/decline")]
    public async Task<IActionResult> Decline(long no) => OkResponse(await _service.DeclineAsync(no), "Offer declined");

    [HttpPost("offers/{no:long}/revoke")]
    public async Task<IActionResult> Revoke(long no) => OkResponse(await _service.RevokeAsync(no), "Offer revoked");

    [HttpDelete("offers/{no:long}")]
    public async Task<IActionResult> Delete(long no) { await _service.DeleteAsync(no); return OkResponse<object?>(null, "Offer deleted"); }
}

// ── Standalone CRUD Controllers (Java hrm/controller/) ─────────────────

[Route("api/v1/hrm/departments")]
public class HrmDepartmentController : ApiControllerBase
{
    private readonly IHrmDepartmentService _service;
    public HrmDepartmentController(IHrmDepartmentService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetList() => OkResponse(await _service.GetListAsync());

    [HttpGet("branch/{branchNo:long}")]
    public async Task<IActionResult> GetByBranch(long branchNo) => OkResponse(await _service.GetListByBranchAsync(branchNo));

    [HttpGet("{no:long}")]
    public async Task<IActionResult> GetDetail(long no) => OkResponse(await _service.GetDetailAsync(no));

    [HttpPost]
    public async Task<IActionResult> Save([FromBody] HrmDepartment dto) => OkResponse(await _service.SaveAsync(dto), dto.DepartmentNo > 0 ? "Department updated" : "Department created");

    [HttpPut("{no:long}")]
    public async Task<IActionResult> Update(long no, [FromBody] HrmDepartment dto) { dto.DepartmentNo = no; return OkResponse(await _service.SaveAsync(dto), "Department updated"); }

    [HttpDelete("{no:long}")]
    public async Task<IActionResult> Delete(long no) { await _service.DeleteAsync(no); return OkResponse<object?>(null, "Department deleted"); }
}

[Route("api/v1/hrm/designations")]
public class HrmDesignationController : ApiControllerBase
{
    private readonly IHrmDesignationService _service;
    public HrmDesignationController(IHrmDesignationService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetList() => OkResponse(await _service.GetListAsync());

    [HttpGet("department/{departmentNo:long}")]
    public async Task<IActionResult> GetByDepartment(long departmentNo) => OkResponse(await _service.GetListByDepartmentAsync(departmentNo));

    [HttpGet("{no:long}")]
    public async Task<IActionResult> GetDetail(long no) => OkResponse(await _service.GetDetailAsync(no));

    [HttpPost]
    public async Task<IActionResult> Save([FromBody] HrmDesignation dto) => OkResponse(await _service.SaveAsync(dto), dto.DesignationNo > 0 ? "Designation updated" : "Designation created");

    [HttpPut("{no:long}")]
    public async Task<IActionResult> Update(long no, [FromBody] HrmDesignation dto) { dto.DesignationNo = no; return OkResponse(await _service.SaveAsync(dto), "Designation updated"); }

    [HttpDelete("{no:long}")]
    public async Task<IActionResult> Delete(long no) { await _service.DeleteAsync(no); return OkResponse<object?>(null, "Designation deleted"); }
}

[Route("api/v1/hrm/employees")]
public class HrmEmployeeController : ApiControllerBase
{
    private readonly IHrmEmployeeService _service;
    public HrmEmployeeController(IHrmEmployeeService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetList() => OkResponse(await _service.GetListAsync());

    [HttpGet("branch/{branchNo:long}")]
    public async Task<IActionResult> GetByBranch(long branchNo) => OkResponse(await _service.GetListByBranchAsync(branchNo));

    [HttpGet("department/{departmentNo:long}")]
    public async Task<IActionResult> GetByDepartment(long departmentNo) => OkResponse(await _service.GetListByDepartmentAsync(departmentNo));

    [HttpGet("{no:long}")]
    public async Task<IActionResult> GetDetail(long no) => OkResponse(await _service.GetDetailAsync(no));

    [HttpPost]
    public async Task<IActionResult> Save([FromBody] HrmEmployee dto) => OkResponse(await _service.SaveAsync(dto), dto.EmployeeNo > 0 ? "Employee updated" : "Employee created");

    [HttpPost("bulk")]
    public async Task<IActionResult> BulkInsert([FromBody] List<HrmEmployee> dtos) => OkResponse(await _service.BulkInsertAsync(dtos), $"Bulk inserted {dtos.Count} employees");

    [HttpPut("{no:long}")]
    public async Task<IActionResult> Update(long no, [FromBody] HrmEmployee dto) { dto.EmployeeNo = no; return OkResponse(await _service.SaveAsync(dto), "Employee updated"); }

    [HttpDelete("{no:long}")]
    public async Task<IActionResult> Delete(long no) { await _service.DeleteAsync(no); return OkResponse<object?>(null, "Employee deleted"); }
}


