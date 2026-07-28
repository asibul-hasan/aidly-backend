using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace AidlyErp.Sys.Application.Dto;

/// <summary>Employee row shown in the SYS1101 User Management picker.</summary>
public class Sys1101EmployeeDto
{
    [JsonPropertyName("employee_no")]
    public long EmployeeNo { get; set; }

    [JsonPropertyName("employee_id")]
    public string? EmployeeId { get; set; }

    [JsonPropertyName("full_name")]
    public string? FullName { get; set; }

    [JsonPropertyName("department_no")]
    public long? DepartmentNo { get; set; }

    [JsonPropertyName("department_name")]
    public string? DepartmentName { get; set; }

    [JsonPropertyName("designation_no")]
    public long? DesignationNo { get; set; }

    [JsonPropertyName("designation_name")]
    public string? DesignationName { get; set; }

    [JsonPropertyName("is_active")]
    public short? IsActive { get; set; }

    [JsonPropertyName("joining_date")]
    public DateOnly? JoiningDate { get; set; }

    [JsonPropertyName("official_email")]
    public string? OfficialEmail { get; set; }

    [JsonPropertyName("mobile_number")]
    public string? MobileNumber { get; set; }
}

/// <summary>Login account for the SYS1101 User Management form.</summary>
public class Sys1101UserDto
{
    [JsonPropertyName("user_no")]
    public long? UserNo { get; set; }

    [JsonPropertyName("user_name")]
    public string? UserName { get; set; }

    [JsonPropertyName("employee_no")]
    public long? EmployeeNo { get; set; }

    [JsonPropertyName("user_id")]
    public string? UserId { get; set; }

    [JsonPropertyName("is_active")]
    public short? IsActive { get; set; }

    [JsonPropertyName("is_locked")]
    public short? IsLocked { get; set; }

    [JsonPropertyName("access_scope")]
    public short? AccessScope { get; set; }

    [JsonPropertyName("failed_login_count")]
    public int? FailedLoginCount { get; set; }

    [JsonPropertyName("last_login_at")]
    public DateTime? LastLoginAt { get; set; }

    [JsonPropertyName("password_changed_at")]
    public DateTime? PasswordChangedAt { get; set; }

    [JsonPropertyName("row_version")]
    public long? RowVersion { get; set; }

    /// <summary>Write-only. Never populated on read — only the hash is stored.</summary>
    [JsonPropertyName("password")]
    public string? Password { get; set; }
}

/// <summary>Body of the admin password-reset call.</summary>
public class UserPasswordResetRequest
{
    [JsonPropertyName("new_password")]
    [Required(ErrorMessage = "New password is required")]
    [StringLength(500, MinimumLength = 8, ErrorMessage = "New password must be at least 8 characters")]
    public string? NewPassword { get; set; }
}

/// <summary>A role assignment on one of the user's branch memberships.</summary>
public class Sys1101UserRoleDto
{
    [JsonPropertyName("user_role_no")]
    public long? UserRoleNo { get; set; }

    [JsonPropertyName("role_no")]
    public long? RoleNo { get; set; }

    [JsonPropertyName("role_name")]
    public string? RoleName { get; set; }

    [JsonPropertyName("is_primary")]
    public short? IsPrimary { get; set; }

    [JsonPropertyName("is_active")]
    public short? IsActive { get; set; }

    public Sys1101UserRoleDto() { }

    public Sys1101UserRoleDto(long? userRoleNo, long? roleNo, string? roleName, short? isPrimary, short? isActive)
    {
        UserRoleNo = userRoleNo;
        RoleNo = roleNo;
        RoleName = roleName;
        IsPrimary = isPrimary;
        IsActive = isActive;
    }
}
