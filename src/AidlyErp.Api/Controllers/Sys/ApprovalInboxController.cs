using AidlyErp.Application.Common.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AidlyErp.Api.Controllers.Sys;

/// <summary>Body of an approve/reject action.</summary>
public class ApprovalActionRequest
{
    [System.Text.Json.Serialization.JsonPropertyName("remarks")]
    public string? Remarks { get; set; }
}

/// <summary>
/// The approval inbox — <c>/api/v1/sys/approvals</c>.
///
/// <code>
/// GET  /pending                          – requests awaiting the caller's action
/// POST /{approvalRequestNo}/approve      – approve the current step
/// POST /{approvalRequestNo}/reject       – reject (terminates the request)
/// </code>
/// </summary>
[ApiController]
[Route("api/v1/sys/approvals")]
public class ApprovalInboxController : ApiControllerBase
{
    private readonly IApprovalService _service;

    public ApprovalInboxController(IApprovalService service) => _service = service;

    [HttpGet("pending")]
    public async Task<IActionResult> GetPending(CancellationToken cancellationToken) =>
        OkResponse(await _service.GetPendingAsync(cancellationToken));

    [HttpPost("{approvalRequestNo:long}/approve")]
    public async Task<IActionResult> Approve(long approvalRequestNo, [FromBody] ApprovalActionRequest? request,
                                             CancellationToken cancellationToken)
    {
        await _service.ActAsync(approvalRequestNo, true, request?.Remarks, cancellationToken);
        return OkResponse<object?>(null, "Approved");
    }

    [HttpPost("{approvalRequestNo:long}/reject")]
    public async Task<IActionResult> Reject(long approvalRequestNo, [FromBody] ApprovalActionRequest? request,
                                            CancellationToken cancellationToken)
    {
        await _service.ActAsync(approvalRequestNo, false, request?.Remarks, cancellationToken);
        return OkResponse<object?>(null, "Rejected");
    }
}
