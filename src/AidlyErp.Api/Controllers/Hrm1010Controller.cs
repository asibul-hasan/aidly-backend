using Microsoft.AspNetCore.Mvc;
using AidlyErp.Application.Hrm.Dto;
using AidlyErp.Application.Hrm.Services;

namespace AidlyErp.Api.Controllers;

/// <summary>
/// HRM_1010 — leave-type and leave-policy endpoints.
/// Java Hrm1010Controller was merged into Hrm1005Controller; this controller
/// preserves the /api/v1/hrm/forms/hrm1010 route so existing clients resolve it.
/// Backed by IHrm1005Service (the same service Hrm1005Controller uses).
/// </summary>
[Route("api/hrm/1010")]
[Route("api/v1/hrm/forms/hrm1010")]
public class Hrm1010Controller : ApiControllerBase
{
    private readonly IHrm1005Service _service;
    public Hrm1010Controller(IHrm1005Service service) { _service = service; }

    [HttpGet("leave-types")]
    public async Task<IActionResult> GetList() => OkResponse(await _service.GetListAsync());

    [HttpGet("leave-types/branch/{branchNo:long}")]
    public async Task<IActionResult> GetByBranch(long branchNo) => OkResponse(await _service.GetListByBranchAsync(branchNo));

    [HttpGet("leave-types/{no:long}")]
    public async Task<IActionResult> GetDetail(long no) => OkResponse(await _service.GetDetailAsync(no));

    [HttpPost("leave-types")]
    public async Task<IActionResult> Save([FromBody] Hrm1005LeaveTypeDto dto) =>
        OkResponse(await _service.SaveAsync(dto), dto.LeaveTypeNo.HasValue && dto.LeaveTypeNo > 0 ? "Leave type updated" : "Leave type created");

    [HttpDelete("leave-types/{no:long}")]
    public async Task<IActionResult> Delete(long no) { await _service.DeleteAsync(no); return OkResponse<object?>(null, "Leave type deleted"); }

    [HttpGet("policies")]
    public async Task<IActionResult> GetPolicyList() => OkResponse(await _service.GetPolicyListAsync());

    [HttpGet("policies/{policyNo:long}")]
    public async Task<IActionResult> GetPolicyDetail(long policyNo) => OkResponse(await _service.GetPolicyDetailAsync(policyNo));

    [HttpPost("policies")]
    public async Task<IActionResult> SavePolicy([FromBody] HrmLeavePolicySetupDto dto) =>
        OkResponse(await _service.SavePolicyAsync(dto), dto.PolicyNo.HasValue && dto.PolicyNo > 0 ? "Policy updated" : "Policy created");

    [HttpDelete("policies/{policyNo:long}")]
    public async Task<IActionResult> DeletePolicy(long policyNo) { await _service.DeletePolicyAsync(policyNo); return OkResponse<object?>(null, "Policy deleted"); }
}
