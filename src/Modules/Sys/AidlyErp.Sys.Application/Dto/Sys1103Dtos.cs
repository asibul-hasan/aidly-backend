using System.Text.Json.Serialization;

namespace AidlyErp.Sys.Application.Dto;

/// <summary>Role option in the SYS1103 role picker.</summary>
public class Sys1103RoleOptionDto
{
    [JsonPropertyName("role_no")]
    public long RoleNo { get; set; }

    [JsonPropertyName("role_id")]
    public string? RoleId { get; set; }

    [JsonPropertyName("role_name")]
    public string? RoleName { get; set; }

    [JsonPropertyName("is_system_role")]
    public short? IsSystemRole { get; set; }

    public Sys1103RoleOptionDto() { }

    public Sys1103RoleOptionDto(long roleNo, string? roleId, string? roleName, short? isSystemRole)
    {
        RoleNo = roleNo;
        RoleId = roleId;
        RoleName = roleName;
        IsSystemRole = isSystemRole;
    }
}

/// <summary>One form's grants in the SYS1103 permission matrix.</summary>
public class Sys1103PermissionRowDto
{
    [JsonPropertyName("menu_no")]
    public long? MenuNo { get; set; }

    [JsonPropertyName("form_id")]
    public string? FormId { get; set; }

    [JsonPropertyName("form_name")]
    public string? FormName { get; set; }

    [JsonPropertyName("module_code")]
    public string? ModuleCode { get; set; }

    [JsonPropertyName("module_name")]
    public string? ModuleName { get; set; }

    [JsonPropertyName("submodule_name")]
    public string? SubmoduleName { get; set; }

    [JsonPropertyName("can_view")]
    public short? CanView { get; set; }

    [JsonPropertyName("can_insert")]
    public short? CanInsert { get; set; }

    [JsonPropertyName("can_update")]
    public short? CanUpdate { get; set; }

    [JsonPropertyName("can_delete")]
    public short? CanDelete { get; set; }

    [JsonPropertyName("can_approve")]
    public short? CanApprove { get; set; }

    [JsonPropertyName("can_post")]
    public short? CanPost { get; set; }

    [JsonPropertyName("can_cancel")]
    public short? CanCancel { get; set; }

    [JsonPropertyName("can_export")]
    public short? CanExport { get; set; }

    /// <summary>1 = COMPANY, 2 = BRANCH (the default).</summary>
    [JsonPropertyName("perm_scope")]
    public short? PermScope { get; set; }

    /// <summary>1 = ALL (the default), 2 = OWN records only.</summary>
    [JsonPropertyName("record_filter")]
    public short? RecordFilter { get; set; }

    /// <summary>
    /// Row-level data scope: 1 = BRANCH (the default — no narrowing beyond the tenant filter),
    /// 2 = DEPARTMENT (the caller's department), 3 = EMPLOYEE (the caller's own records).
    /// See <see cref="AidlyErp.Shared.Core.Security.DataScopeConstants"/>.
    /// </summary>
    [JsonPropertyName("data_scope")]
    public short? DataScope { get; set; }
}
