using System.Text.Json.Serialization;

namespace AidlyErp.Sys.Application.Dto;

/// <summary>Generic {value, label} option row for the SYS1104 pickers.</summary>
public class Sys1104OptionDto
{
    [JsonPropertyName("value")]
    public long Value { get; set; }

    [JsonPropertyName("label")]
    public string? Label { get; set; }

    public Sys1104OptionDto() { }

    public Sys1104OptionDto(long value, string? label)
    {
        Value = value;
        Label = label;
    }
}

/// <summary>Role option, carrying the branches the role template is published to.</summary>
public class Sys1104RoleOptionDto
{
    [JsonPropertyName("value")]
    public long Value { get; set; }

    [JsonPropertyName("label")]
    public string? Label { get; set; }

    [JsonPropertyName("branch_nos")]
    public List<long> BranchNos { get; set; } = new();

    public Sys1104RoleOptionDto() { }

    public Sys1104RoleOptionDto(long value, string? label, List<long> branchNos)
    {
        Value = value;
        Label = label;
        BranchNos = branchNos;
    }
}

/// <summary>User option in the SYS1104 user picker.</summary>
public class Sys1104UserOptionDto
{
    [JsonPropertyName("user_no")]
    public long UserNo { get; set; }

    [JsonPropertyName("user_id")]
    public string? UserId { get; set; }

    [JsonPropertyName("user_name")]
    public string? UserName { get; set; }

    [JsonPropertyName("employee_no")]
    public long? EmployeeNo { get; set; }

    [JsonPropertyName("access_scope")]
    public short? AccessScope { get; set; }

    public Sys1104UserOptionDto() { }

    public Sys1104UserOptionDto(long userNo, string? userId, string? userName, long? employeeNo, short? accessScope)
    {
        UserNo = userNo;
        UserId = userId;
        UserName = userName;
        EmployeeNo = employeeNo;
        AccessScope = accessScope;
    }
}

/// <summary>One branch membership with its role, from <c>sys_user_branch</c>.</summary>
public class Sys1104MappingDto
{
    [JsonPropertyName("user_branch_no")]
    public long? UserBranchNo { get; set; }

    [JsonPropertyName("branch_no")]
    public long? BranchNo { get; set; }

    [JsonPropertyName("branch_name")]
    public string? BranchName { get; set; }

    [JsonPropertyName("role_no")]
    public long? RoleNo { get; set; }

    [JsonPropertyName("role_name")]
    public string? RoleName { get; set; }

    [JsonPropertyName("is_default")]
    public short? IsDefault { get; set; }

    [JsonPropertyName("is_active")]
    public short? IsActive { get; set; }
}

/// <summary>Body of the bulk mapping save — applies the same rows to many users.</summary>
public class Sys1104BulkSaveDto
{
    [JsonPropertyName("user_nos")]
    public List<long>? UserNos { get; set; }

    [JsonPropertyName("rows")]
    public List<Sys1104MappingDto>? Rows { get; set; }
}
