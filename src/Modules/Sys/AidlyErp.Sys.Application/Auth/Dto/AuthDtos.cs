using System.Text.Json.Serialization;

namespace AidlyErp.Sys.Application.Auth.Dto;

public class LoginRequest
{
    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("user_id")]
    public string? UserId { get; set; }

    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;

    [JsonPropertyName("company_no")]
    public long? CompanyNo { get; set; }

    [JsonPropertyName("branch_no")]
    public long? BranchNo { get; set; }

    public string GetIdentifier() => !string.IsNullOrWhiteSpace(Username) ? Username : (UserId ?? string.Empty);
}

public class LoginResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; } = string.Empty;

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = "Bearer";

    [JsonPropertyName("expires_in")]
    public long ExpiresIn { get; set; } = 300; // 5 min in seconds

    [JsonPropertyName("user_no")]
    public long UserNo { get; set; }

    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("user_name")]
    public string UserName { get; set; } = string.Empty;

    [JsonPropertyName("company_no")]
    public long CompanyNo { get; set; }

    [JsonPropertyName("branch_no")]
    public long BranchNo { get; set; }

    [JsonPropertyName("access_scope")]
    public short AccessScope { get; set; }

    // ─── RBAC session payload ────────────────────────────────────────────────
    // The shell renders navigation and gates actions from these. Without them the
    // frontend holds a valid token but has no menu, so it cannot draw anything.

    [JsonPropertyName("auth_config")]
    public AuthConfigResponse? AuthConfig { get; set; }

    [JsonPropertyName("employee_no")]
    public long? EmployeeNo { get; set; }

    [JsonPropertyName("employee_name")]
    public string? EmployeeName { get; set; }

    [JsonPropertyName("department_name")]
    public string? DepartmentName { get; set; }

    [JsonPropertyName("designation_name")]
    public string? DesignationName { get; set; }

    [JsonPropertyName("company_name")]
    public string? CompanyName { get; set; }

    [JsonPropertyName("branch_name")]
    public string? BranchName { get; set; }

    [JsonPropertyName("available_branches")]
    public List<BranchDto> AvailableBranches { get; set; } = new();

    [JsonPropertyName("role_nos")]
    public List<long> RoleNos { get; set; } = new();

    [JsonPropertyName("roles")]
    public List<AuthRoleDto> Roles { get; set; } = new();

    [JsonPropertyName("must_change_password")]
    public short? MustChangePassword { get; set; }

    [JsonPropertyName("menu")]
    public List<MenuItemDto> Menu { get; set; } = new();
}

public class RefreshTokenRequest
{
    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; } = string.Empty;
}

public class RefreshTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; } = string.Empty;

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = "Bearer";

    [JsonPropertyName("expires_in")]
    public long ExpiresIn { get; set; } = 300;
}

public class AuthConfigResponse
{
    [JsonPropertyName("access_token_expiration_ms")]
    public long AccessTokenExpirationMs { get; set; } = 300000;

    [JsonPropertyName("refresh_token_expiration_ms")]
    public long RefreshTokenExpirationMs { get; set; } = 604800000;

    /// <summary>How long before expiry the client should proactively refresh the access token.</summary>
    [JsonPropertyName("access_token_refresh_before_ms")]
    public long AccessTokenRefreshBeforeMs { get; set; } = 30000;

    /// <summary>Idle period after which the client should treat the session as ended.</summary>
    [JsonPropertyName("inactivity_timeout_ms")]
    public long InactivityTimeoutMs { get; set; } = 900000;
}

public class CurrentUserResponse
{
    [JsonPropertyName("user_no")]
    public long UserNo { get; set; }

    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("user_name")]
    public string UserName { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("employee_no")]
    public long? EmployeeNo { get; set; }

    [JsonPropertyName("designation")]
    public string? Designation { get; set; }

    [JsonPropertyName("company_no")]
    public long CompanyNo { get; set; }

    [JsonPropertyName("company_name")]
    public string? CompanyName { get; set; }

    [JsonPropertyName("branch_no")]
    public long BranchNo { get; set; }

    [JsonPropertyName("branch_name")]
    public string? BranchName { get; set; }

    [JsonPropertyName("access_scope")]
    public short AccessScope { get; set; }
}

public class ChangePasswordRequest
{
    [JsonPropertyName("old_password")]
    public string OldPassword { get; set; } = string.Empty;

    [JsonPropertyName("new_password")]
    public string NewPassword { get; set; } = string.Empty;
}

public class ResetPasswordRequest
{
    [JsonPropertyName("user_name")]
    public string UserName { get; set; } = string.Empty;

    [JsonPropertyName("new_password")]
    public string NewPassword { get; set; } = string.Empty;
}

public class SwitchBranchRequest
{
    [JsonPropertyName("branch_no")]
    public long BranchNo { get; set; }
}

public class SwitchCompanyRequest
{
    [JsonPropertyName("company_no")]
    public long CompanyNo { get; set; }

    [JsonPropertyName("branch_no")]
    public long? BranchNo { get; set; }
}

/// <summary>
/// Per-form permission grant.
///
/// <para>Every flag defaults to <b>false</b>. The Java <c>FormPermissionResponse</c> is a Lombok
/// builder whose unset booleans are <c>false</c>, and <c>RbacAuthorizationService.empty()</c>
/// relies on that to deny access. Defaulting these to <c>true</c> would make an
/// unpopulated/failed resolve fail <i>open</i> — granting every permission on the form.</para>
/// </summary>
/// <summary>
/// A company the caller can switch into, with the branches available inside it. Returned by the
/// pre-login context-discovery call.
/// </summary>
public class UserContextResponse
{
    [JsonPropertyName("company_no")]
    public long? CompanyNo { get; set; }

    [JsonPropertyName("company_name")]
    public string? CompanyName { get; set; }

    [JsonPropertyName("branches")]
    public List<UserContextBranchDto> Branches { get; set; } = new();
}

/// <summary>Minimal branch identity for the context picker — the Java nested <c>BranchDto</c>.</summary>
public class UserContextBranchDto
{
    [JsonPropertyName("branch_no")]
    public long? BranchNo { get; set; }

    [JsonPropertyName("branch_name")]
    public string? BranchName { get; set; }

    public UserContextBranchDto() { }

    public UserContextBranchDto(long? branchNo, string? branchName)
    {
        BranchNo = branchNo;
        BranchName = branchName;
    }
}

public class FormPermissionResponse
{
    [JsonPropertyName("form_id")]
    public string FormId { get; set; } = string.Empty;

    [JsonPropertyName("form_name")]
    public string? FormName { get; set; }

    [JsonPropertyName("can_view")]
    public bool CanView { get; set; }

    [JsonPropertyName("can_insert")]
    public bool CanInsert { get; set; }

    [JsonPropertyName("can_update")]
    public bool CanUpdate { get; set; }

    [JsonPropertyName("can_delete")]
    public bool CanDelete { get; set; }

    [JsonPropertyName("can_approve")]
    public bool CanApprove { get; set; }

    [JsonPropertyName("can_post")]
    public bool CanPost { get; set; }

    [JsonPropertyName("can_cancel")]
    public bool CanCancel { get; set; }

    [JsonPropertyName("can_export")]
    public bool CanExport { get; set; }
}
