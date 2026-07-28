using Microsoft.AspNetCore.Mvc;
using AidlyErp.Sys.Application.Auth.Dto;
using AidlyErp.Sys.Application.Auth.Services;

namespace AidlyErp.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class AuthController : ApiControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpGet("config")]
    public IActionResult GetConfig()
    {
        var config = new AuthConfigResponse
        {
            AccessTokenExpirationMs = 300000,
            RefreshTokenExpirationMs = 604800000
        };
        return OkResponse(config);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.LoginAsync(request);
        return OkResponse(result, "Login successful");
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        return OkResponse<object?>(null, "Logged out successfully");
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
    {
        var result = await _authService.RefreshAsync(request);
        return OkResponse(result, "Token refreshed successfully");
    }

    [HttpPost("switch-branch")]
    public async Task<IActionResult> SwitchBranch([FromBody] SwitchBranchRequest request)
    {
        var result = await _authService.SwitchBranchAsync(request);
        return OkResponse(result, "Branch switched successfully");
    }

    [HttpPost("switch-company")]
    public async Task<IActionResult> SwitchCompany([FromBody] SwitchCompanyRequest request)
    {
        var result = await _authService.SwitchCompanyAsync(request);
        return OkResponse(result, "Company switched successfully");
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMe()
    {
        var user = await _authService.GetCurrentUserAsync();
        return OkResponse(user);
    }

    [HttpGet("permissions/check")]
    public IActionResult CheckPermission([FromQuery] string formId)
    {
        var permission = _authService.GetFormPermission(formId);
        return OkResponse(permission);
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        await _authService.ChangePasswordAsync(request);
        return OkResponse<object?>(null, "Password changed successfully");
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        await _authService.ResetPasswordAsync(request);
        return OkResponse<object?>(null, "Password reset successfully");
    }

    // ─── Pre-login context discovery ─────────────────────────────────────────
    // Public by design: the login screen calls these before a token exists, so the
    // user can pick a company/branch. Both user_id and user_name are accepted, as
    // in the Java controller.

    [HttpGet("contexts")]
    public async Task<IActionResult> GetContexts([FromQuery(Name = "user_id")] string? userId,
                                                 [FromQuery(Name = "user_name")] string? userName) =>
        OkResponse(await _authService.GetUserContextsAsync(userId ?? userName));

    [HttpGet("companies")]
    public async Task<IActionResult> GetCompanies([FromQuery(Name = "user_id")] string? userId,
                                                  [FromQuery(Name = "user_name")] string? userName) =>
        OkResponse(await _authService.GetCompaniesByUserIdAsync(userId ?? userName));

    [HttpGet("branches")]
    public async Task<IActionResult> GetBranches([FromQuery(Name = "user_id")] string? userId,
                                                 [FromQuery(Name = "user_name")] string? userName) =>
        OkResponse(await _authService.GetBranchesByUserIdAsync(userId ?? userName));
}
