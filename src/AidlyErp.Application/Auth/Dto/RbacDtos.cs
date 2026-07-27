using System.Text.Json.Serialization;

namespace AidlyErp.Application.Auth.Dto;

/// <summary>Port of <c>core.auth.dto.AuthRoleDto</c>.</summary>
public class AuthRoleDto
{
    [JsonPropertyName("role_no")]
    public long RoleNo { get; set; }

    [JsonPropertyName("role_name")]
    public string? RoleName { get; set; }

    [JsonPropertyName("is_default")]
    public short IsDefault { get; set; }

    public AuthRoleDto() { }

    public AuthRoleDto(long roleNo, string? roleName, short isDefault)
    {
        RoleNo = roleNo;
        RoleName = roleName;
        IsDefault = isDefault;
    }
}

/// <summary>Port of <c>sys.dto.BranchDto</c> as consumed by the RBAC session context.</summary>
public class BranchDto
{
    [JsonPropertyName("company_no")]
    public long? CompanyNo { get; set; }

    [JsonPropertyName("company_name")]
    public string? CompanyName { get; set; }

    [JsonPropertyName("branch_no")]
    public long? BranchNo { get; set; }

    [JsonPropertyName("branch_name")]
    public string? BranchName { get; set; }

    [JsonPropertyName("is_active")]
    public short IsActive { get; set; } = 1;
}

/// <summary>Port of <c>core.auth.dto.MenuItemDto</c>.</summary>
public class MenuItemDto
{
    [JsonPropertyName("form_id")]
    public string? FormId { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("icon")]
    public string? Icon { get; set; }

    [JsonPropertyName("route")]
    public string? Route { get; set; }

    [JsonPropertyName("module")]
    public string? Module { get; set; }

    /// <summary>Module code — the key the permission config groups forms under.</summary>
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("submodule")]
    public string? Submodule { get; set; }

    [JsonPropertyName("module_icon")]
    public string? ModuleIcon { get; set; }

    [JsonPropertyName("submodule_icon")]
    public string? SubmoduleIcon { get; set; }

    [JsonPropertyName("module_serial")]
    public int? ModuleSerial { get; set; }

    [JsonPropertyName("can_insert")]
    public bool CanInsert { get; set; }

    [JsonPropertyName("can_update")]
    public bool CanUpdate { get; set; }

    [JsonPropertyName("can_view")]
    public bool CanView { get; set; }

    [JsonPropertyName("can_delete")]
    public bool CanDelete { get; set; }

    [JsonPropertyName("can_approve")]
    public bool CanApprove { get; set; }
}

/// <summary>Port of <c>core.auth.dto.FormPermissionDto</c>.</summary>
public class FormPermissionDto
{
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

    public FormPermissionDto() { }

    public FormPermissionDto(bool canView, bool canInsert, bool canUpdate, bool canDelete, bool canApprove)
    {
        CanView = canView;
        CanInsert = canInsert;
        CanUpdate = canUpdate;
        CanDelete = canDelete;
        CanApprove = canApprove;
    }
}

/// <summary>Port of <c>core.auth.dto.ModulePermissionDto</c>.</summary>
public class ModulePermissionDto
{
    [JsonPropertyName("module")]
    public string? Module { get; set; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("forms")]
    public Dictionary<string, FormPermissionDto> Forms { get; set; } = new();
}

/// <summary>Port of <c>core.auth.dto.PermissionConfigDto</c>.</summary>
public class PermissionConfigDto
{
    [JsonPropertyName("modules")]
    public List<ModulePermissionDto> Modules { get; set; } = new();

    public PermissionConfigDto() { }

    public PermissionConfigDto(List<ModulePermissionDto> modules) => Modules = modules;
}

/// <summary>Port of <c>core.auth.model.RbacSessionContext</c>.</summary>
public class RbacSessionContext
{
    public long? CompanyNo { get; set; }
    public string? CompanyName { get; set; }
    public long? BranchNo { get; set; }
    public string? BranchName { get; set; }
    public List<long> RoleNos { get; set; } = new();
    public List<AuthRoleDto> Roles { get; set; } = new();
    public List<BranchDto> Branches { get; set; } = new();
    public List<MenuItemDto> Menu { get; set; } = new();
    public PermissionConfigDto Permissions { get; set; } = new();

    /// <summary>Per-form permission bitmasks (formId → mask, see <c>PermissionBits</c>) baked into the JWT.</summary>
    public Dictionary<string, int> PermBits { get; set; } = new();
}
