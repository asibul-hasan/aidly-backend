using AidlyErp.Application.Common.Exceptions;
using AidlyErp.Application.Sys.Dto;
using AidlyErp.Application.Sys.Services;
using Microsoft.AspNetCore.Mvc;

namespace AidlyErp.Api.Controllers.Sys;

/// <summary>
/// Dedicated controller for the SYS1101 User Management form.
///
/// <code>
/// GET    /employees                          – employee picker (?is_active=)
/// GET    /employees/{employeeNo}             – employee detail
/// GET    /users/by-employee/{employeeNo}     – the employee's login account, or null
/// GET    /users/{userNo}                     – user detail
/// POST   /users                              – create user
/// POST   /users/from-employee/{employeeNo}   – provision a user from employee data
/// PUT    /users/{userNo}                     – update user
/// DELETE /users/{userNo}                     – soft-delete user
/// PATCH  /users/{userNo}/lock                – lock / unlock (?locked=)
/// POST   /users/{userNo}/reset-password      – reset password
/// GET    /users/{userNo}/roles               – role assignments per branch
/// </code>
/// </summary>
[ApiController]
[Route("api/v1/sys/forms/sys1101")]
public class Sys1101Controller : ApiControllerBase
{
    private readonly ISys1101Service _service;

    public Sys1101Controller(ISys1101Service service) => _service = service;

    // ─── Employees ───────────────────────────────────────────────────────────

    [HttpGet("employees")]
    public async Task<IActionResult> GetEmployeeList([FromQuery(Name = "is_active")] short? isActive,
                                                     CancellationToken cancellationToken) =>
        OkResponse(await _service.GetEmployeeListAsync(isActive, cancellationToken));

    [HttpGet("employees/{employeeNo:long}")]
    public async Task<IActionResult> GetEmployeeDetail(long employeeNo, CancellationToken cancellationToken) =>
        OkResponse(await _service.GetEmployeeDetailAsync(employeeNo, cancellationToken));

    // ─── Users ───────────────────────────────────────────────────────────────

    [HttpGet("users/by-employee/{employeeNo:long}")]
    public async Task<IActionResult> GetUserByEmployeeNo(long employeeNo, CancellationToken cancellationToken) =>
        OkResponse(await _service.GetUserByEmployeeNoAsync(employeeNo, cancellationToken));

    [HttpGet("users/{userNo:long}")]
    public async Task<IActionResult> GetUserDetail(long userNo, CancellationToken cancellationToken) =>
        OkResponse(await _service.GetUserDetailAsync(userNo, cancellationToken));

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser([FromBody] Sys1101UserDto dto, CancellationToken cancellationToken) =>
        OkResponse(await _service.CreateUserAsync(dto, cancellationToken), "User created");

    [HttpPost("users/from-employee/{employeeNo:long}")]
    public async Task<IActionResult> CreateUserFromEmployee(long employeeNo, CancellationToken cancellationToken) =>
        OkResponse(await _service.CreateUserFromEmployeeAsync(employeeNo, cancellationToken), "User created");

    [HttpPut("users/{userNo:long}")]
    public async Task<IActionResult> UpdateUser(long userNo, [FromBody] Sys1101UserDto dto,
                                                CancellationToken cancellationToken) =>
        OkResponse(await _service.UpdateUserAsync(userNo, dto, cancellationToken), "User updated");

    [HttpDelete("users/{userNo:long}")]
    public async Task<IActionResult> DeleteUser(long userNo, CancellationToken cancellationToken)
    {
        await _service.DeleteUserAsync(userNo, cancellationToken);
        return OkResponse<object?>(null, "User deleted");
    }

    [HttpPatch("users/{userNo:long}/lock")]
    public async Task<IActionResult> LockUser(long userNo, [FromQuery] short locked,
                                              CancellationToken cancellationToken) =>
        OkResponse(await _service.LockUserAsync(userNo, locked, cancellationToken),
            locked == 1 ? "User locked" : "User unlocked");

    [HttpPost("users/{userNo:long}/reset-password")]
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

    [HttpGet("users/{userNo:long}/roles")]
    public async Task<IActionResult> GetUserRoles(long userNo, CancellationToken cancellationToken) =>
        OkResponse(await _service.GetUserRolesAsync(userNo, cancellationToken));
}
