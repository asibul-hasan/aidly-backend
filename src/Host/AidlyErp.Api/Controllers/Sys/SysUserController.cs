using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Sys.Application.Dto;
using AidlyErp.Sys.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace AidlyErp.Api.Controllers.Sys;

/// <summary>
/// Generic user CRUD — <c>/api/v1/sys/users</c>.
///
/// <code>
/// GET    /                             – list (?search=&amp;is_active=&amp;is_locked=)
/// GET    /summary                      – compact list for grids
/// GET    /{userNo}                     – detail, enriched from the linked employee
/// GET    /{userNo}/roles                – branch/role assignments
/// GET    /employee/{employeeNo}        – the employee's login account
/// POST   /                             – create
/// PUT    /{userNo}                     – update
/// DELETE /{userNo}                     – soft-delete
/// PATCH  /{userNo}/lock                – lock / unlock (?locked=)
/// POST   /{userNo}/reset-password      – reset password
/// </code>
/// </summary>
[ApiController]
[Route("api/v1/sys/users")]
public class SysUserController : ApiControllerBase
{
    private readonly IUserService _service;

    public SysUserController(IUserService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery(Name = "search")] string? search,
                                             [FromQuery(Name = "is_active")] short? isActive,
                                             [FromQuery(Name = "is_locked")] short? isLocked,
                                             CancellationToken cancellationToken) =>
        OkResponse(await _service.GetListAsync(search, isActive, isLocked, cancellationToken));

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummaryList([FromQuery(Name = "search")] string? search,
                                                    [FromQuery(Name = "is_active")] short? isActive,
                                                    CancellationToken cancellationToken) =>
        OkResponse(await _service.GetSummaryListAsync(search, isActive, cancellationToken));

    [HttpGet("{userNo:long}")]
    public async Task<IActionResult> GetDtl(long userNo, CancellationToken cancellationToken) =>
        OkResponse(await _service.GetDtlAsync(userNo, cancellationToken));

    [HttpGet("{userNo:long}/roles")]
    public async Task<IActionResult> GetUserRoles(long userNo, CancellationToken cancellationToken) =>
        OkResponse(await _service.GetUserRolesAsync(userNo, cancellationToken));

    [HttpGet("employee/{employeeNo:long}")]
    public async Task<IActionResult> GetByEmployeeNo(long employeeNo, CancellationToken cancellationToken) =>
        OkResponse(await _service.GetByEmployeeNoAsync(employeeNo, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] UserDto dto, CancellationToken cancellationToken) =>
        OkResponse(await _service.InsertAsync(dto, cancellationToken), "User created successfully");

    [HttpPut("{userNo:long}")]
    public async Task<IActionResult> Update(long userNo, [FromBody] UserDto dto,
                                            CancellationToken cancellationToken) =>
        OkResponse(await _service.UpdateAsync(userNo, dto, cancellationToken), "User updated successfully");

    [HttpDelete("{userNo:long}")]
    public async Task<IActionResult> Delete(long userNo, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(userNo, cancellationToken);
        return OkResponse<object?>(null, "User deleted successfully");
    }

    [HttpPatch("{userNo:long}/lock")]
    public async Task<IActionResult> SetLocked(long userNo, [FromQuery] short locked,
                                               CancellationToken cancellationToken) =>
        OkResponse(await _service.SetLockedAsync(userNo, locked, cancellationToken),
            locked == 1 ? "User locked" : "User unlocked");

    [HttpPost("{userNo:long}/reset-password")]
    public async Task<IActionResult> ResetPassword(long userNo, [FromBody] UserPasswordResetRequest request,
                                                   CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword))
        {
            throw new ValidationException("New password is required");
        }

        await _service.ResetPasswordAsync(userNo, request.NewPassword, cancellationToken);
        return OkResponse<object?>(null, "Password reset");
    }
}
