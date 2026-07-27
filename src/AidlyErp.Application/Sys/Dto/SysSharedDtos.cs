using System.Text.Json.Serialization;

namespace AidlyErp.Application.Sys.Dto;

/// <summary>Shared module DTO used by the generic <c>/api/v1/sys/modules</c> endpoints.</summary>
public class SysModuleDto
{
    [JsonPropertyName("module_no")] public long? ModuleNo { get; set; }
    [JsonPropertyName("module_code")] public string? ModuleCode { get; set; }
    [JsonPropertyName("module_name")] public string? ModuleName { get; set; }
    [JsonPropertyName("module_desc")] public string? ModuleDesc { get; set; }
    [JsonPropertyName("module_icon")] public string? ModuleIcon { get; set; }
    [JsonPropertyName("module_route")] public string? ModuleRoute { get; set; }
    [JsonPropertyName("order_sl")] public int? OrderSl { get; set; }
    [JsonPropertyName("is_active")] public short? IsActive { get; set; }
    [JsonPropertyName("row_version")] public long? RowVersion { get; set; }
}

/// <summary>Shared submodule DTO used by the generic <c>/api/v1/sys/submodules</c> endpoints.</summary>
public class SysSubmoduleDto
{
    [JsonPropertyName("submodule_no")] public long? SubmoduleNo { get; set; }
    [JsonPropertyName("module_no")] public long? ModuleNo { get; set; }
    [JsonPropertyName("submodule_code")] public string? SubmoduleCode { get; set; }
    [JsonPropertyName("submodule_name")] public string? SubmoduleName { get; set; }
    [JsonPropertyName("submodule_icon")] public string? SubmoduleIcon { get; set; }
    [JsonPropertyName("submodule_route")] public string? SubmoduleRoute { get; set; }
    [JsonPropertyName("order_sl")] public int? OrderSl { get; set; }
    [JsonPropertyName("is_active")] public short? IsActive { get; set; }
    [JsonPropertyName("row_version")] public long? RowVersion { get; set; }
}

/// <summary>Shared menu DTO used by the generic <c>/api/v1/sys/menus</c> endpoints.</summary>
public class MenuDto
{
    [JsonPropertyName("menu_no")] public long? MenuNo { get; set; }
    [JsonPropertyName("submodule_no")] public long? SubmoduleNo { get; set; }
    [JsonPropertyName("form_id")] public string? FormId { get; set; }
    [JsonPropertyName("form_name")] public string? FormName { get; set; }
    [JsonPropertyName("menu_desc")] public string? MenuDesc { get; set; }
    [JsonPropertyName("menu_type")] public string? MenuType { get; set; }
    [JsonPropertyName("route_path")] public string? RoutePath { get; set; }
    [JsonPropertyName("icon_name")] public string? IconName { get; set; }
    [JsonPropertyName("order_sl")] public int? OrderSl { get; set; }
    [JsonPropertyName("is_active")] public short? IsActive { get; set; }
    [JsonPropertyName("row_version")] public long? RowVersion { get; set; }
}

/// <summary>Shared company DTO used by the generic <c>/api/v1/sys/companies</c> endpoints.</summary>
public class CompanyDto
{
    [JsonPropertyName("company_no")] public long? CompanyNo { get; set; }
    [JsonPropertyName("company_id")] public string? CompanyId { get; set; }
    [JsonPropertyName("company_name")] public string? CompanyName { get; set; }
    [JsonPropertyName("company_name_nls")] public string? CompanyNameNls { get; set; }
    [JsonPropertyName("company_type")] public string? CompanyType { get; set; }
    [JsonPropertyName("trade_license_no")] public string? TradeLicenseNo { get; set; }
    [JsonPropertyName("vat_reg_no")] public string? VatRegNo { get; set; }
    [JsonPropertyName("tin_no")] public string? TinNo { get; set; }
    [JsonPropertyName("bin_no")] public string? BinNo { get; set; }
    [JsonPropertyName("reg_no")] public string? RegNo { get; set; }
    [JsonPropertyName("company_addr1")] public string? CompanyAddr1 { get; set; }
    [JsonPropertyName("company_addr2")] public string? CompanyAddr2 { get; set; }
    [JsonPropertyName("city")] public string? City { get; set; }
    [JsonPropertyName("state_province")] public string? StateProvince { get; set; }
    [JsonPropertyName("post_code")] public string? PostCode { get; set; }
    [JsonPropertyName("country_code")] public string? CountryCode { get; set; }
    [JsonPropertyName("mobile_no")] public string? MobileNo { get; set; }
    [JsonPropertyName("contact_no")] public string? ContactNo { get; set; }
    [JsonPropertyName("email")] public string? Email { get; set; }
    [JsonPropertyName("website")] public string? Website { get; set; }

    /// <summary>Accepts a <c>data:image/...;base64,…</c> payload on write; returns the stored path.</summary>
    [JsonPropertyName("logo_path")] public string? LogoPath { get; set; }

    [JsonPropertyName("is_active")] public short? IsActive { get; set; }
    [JsonPropertyName("row_version")] public long? RowVersion { get; set; }
}

/// <summary>A user's role assignment on one branch, from <c>sys_user_branch</c>.</summary>
public class UserRoleDto
{
    [JsonPropertyName("user_role_no")] public long? UserRoleNo { get; set; }
    [JsonPropertyName("user_no")] public long? UserNo { get; set; }
    [JsonPropertyName("branch_no")] public long? BranchNo { get; set; }
    [JsonPropertyName("role_no")] public long? RoleNo { get; set; }
    [JsonPropertyName("role_name")] public string? RoleName { get; set; }
    [JsonPropertyName("is_primary")] public short? IsPrimary { get; set; }
    [JsonPropertyName("is_active")] public short? IsActive { get; set; }
    [JsonPropertyName("row_version")] public long? RowVersion { get; set; }
}

/// <summary>
/// Shared user DTO for <c>/api/v1/sys/users</c>. The employee-derived fields below the
/// <c>row_version</c> line are read-only enrichment, resolved from the linked HRM employee.
/// </summary>
public class UserDto
{
    [JsonPropertyName("user_no")] public long? UserNo { get; set; }
    [JsonPropertyName("user_id")] public string? UserId { get; set; }
    [JsonPropertyName("user_name")] public string? UserName { get; set; }
    [JsonPropertyName("email")] public string? Email { get; set; }
    [JsonPropertyName("employee_no")] public long? EmployeeNo { get; set; }

    /// <summary>Write-only; never echoed back.</summary>
    [JsonPropertyName("password")] public string? Password { get; set; }

    [JsonPropertyName("is_active")] public short? IsActive { get; set; }
    [JsonPropertyName("is_locked")] public short? IsLocked { get; set; }
    [JsonPropertyName("failed_login_count")] public int? FailedLoginCount { get; set; }
    [JsonPropertyName("company_no")] public long? CompanyNo { get; set; }
    [JsonPropertyName("access_scope")] public short? AccessScope { get; set; }
    [JsonPropertyName("default_branch_no")] public long? DefaultBranchNo { get; set; }
    [JsonPropertyName("last_login_at")] public DateTime? LastLoginAt { get; set; }
    [JsonPropertyName("password_changed_at")] public DateTime? PasswordChangedAt { get; set; }
    [JsonPropertyName("row_version")] public long? RowVersion { get; set; }

    // --- Read-only enrichment from the linked employee ---
    [JsonPropertyName("employee_id")] public string? EmployeeId { get; set; }
    [JsonPropertyName("employee_name")] public string? EmployeeName { get; set; }
    [JsonPropertyName("department_no")] public long? DepartmentNo { get; set; }
    [JsonPropertyName("department_name")] public string? DepartmentName { get; set; }
    [JsonPropertyName("designation_no")] public long? DesignationNo { get; set; }
    [JsonPropertyName("designation_name")] public string? DesignationName { get; set; }
    [JsonPropertyName("branch_no")] public long? BranchNo { get; set; }
    [JsonPropertyName("branch_name")] public string? BranchName { get; set; }
    [JsonPropertyName("official_email")] public string? OfficialEmail { get; set; }
    [JsonPropertyName("mobile_number")] public string? MobileNumber { get; set; }
    [JsonPropertyName("joining_date")] public DateOnly? JoiningDate { get; set; }
    [JsonPropertyName("employee_status")] public short? EmployeeStatus { get; set; }

    [JsonPropertyName("role_mappings")] public List<UserRoleDto>? RoleMappings { get; set; }
}

/// <summary>Compact user row for list screens.</summary>
public class UserSummaryDto
{
    [JsonPropertyName("user_no")] public long UserNo { get; set; }
    [JsonPropertyName("user_id")] public string? UserId { get; set; }
    [JsonPropertyName("user_name")] public string? UserName { get; set; }
    [JsonPropertyName("employee_no")] public long? EmployeeNo { get; set; }
    [JsonPropertyName("is_active")] public short? IsActive { get; set; }
    [JsonPropertyName("is_locked")] public short? IsLocked { get; set; }
}

/// <summary>Shared role DTO used by the generic <c>/api/v1/sys/roles</c> endpoints.</summary>
public class RoleDto
{
    [JsonPropertyName("role_no")] public long? RoleNo { get; set; }
    [JsonPropertyName("company_no")] public long? CompanyNo { get; set; }
    [JsonPropertyName("company_name")] public string? CompanyName { get; set; }
    [JsonPropertyName("role_id")] public string? RoleId { get; set; }
    [JsonPropertyName("role_name")] public string? RoleName { get; set; }
    [JsonPropertyName("role_desc")] public string? RoleDesc { get; set; }

    /// <summary>Branches the role is published to. Empty means <b>all branches</b>.</summary>
    [JsonPropertyName("branch_nos")] public List<long>? BranchNos { get; set; }

    [JsonPropertyName("is_system_role")] public short? IsSystemRole { get; set; }
    [JsonPropertyName("is_active")] public short? IsActive { get; set; }
    [JsonPropertyName("row_version")] public long? RowVersion { get; set; }
}

/// <summary>Compact role option for dropdowns.</summary>
public class RoleLookupDto
{
    [JsonPropertyName("role_no")] public long RoleNo { get; set; }
    [JsonPropertyName("role_id")] public string? RoleId { get; set; }
    [JsonPropertyName("role_name")] public string? RoleName { get; set; }
    [JsonPropertyName("is_system_role")] public short? IsSystemRole { get; set; }
    [JsonPropertyName("is_active")] public short? IsActive { get; set; }
}

/// <summary>Shared branch DTO used by the generic <c>/api/v1/sys/branches</c> endpoints.</summary>
public class BranchDto
{
    [JsonPropertyName("branch_no")] public long? BranchNo { get; set; }
    [JsonPropertyName("branch_id")] public string? BranchId { get; set; }
    [JsonPropertyName("branch_name")] public string? BranchName { get; set; }
    [JsonPropertyName("branch_name_nls")] public string? BranchNameNls { get; set; }
    [JsonPropertyName("branch_type")] public string? BranchType { get; set; }
    [JsonPropertyName("branch_addr1")] public string? BranchAddr1 { get; set; }
    [JsonPropertyName("branch_addr2")] public string? BranchAddr2 { get; set; }
    [JsonPropertyName("city")] public string? City { get; set; }
    [JsonPropertyName("post_code")] public string? PostCode { get; set; }
    [JsonPropertyName("mobile_no")] public string? MobileNo { get; set; }
    [JsonPropertyName("contact_no")] public string? ContactNo { get; set; }
    [JsonPropertyName("email")] public string? Email { get; set; }
    [JsonPropertyName("manager_employee_no")] public long? ManagerEmployeeNo { get; set; }
    [JsonPropertyName("company_no")] public long? CompanyNo { get; set; }
    [JsonPropertyName("company_name")] public string? CompanyName { get; set; }
    [JsonPropertyName("is_main_branch")] public short? IsMainBranch { get; set; }
    [JsonPropertyName("is_active")] public short? IsActive { get; set; }
    [JsonPropertyName("row_version")] public long? RowVersion { get; set; }
}
