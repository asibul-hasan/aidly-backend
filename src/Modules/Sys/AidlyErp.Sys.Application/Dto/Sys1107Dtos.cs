using System.Text.Json.Serialization;

namespace AidlyErp.Sys.Application.Dto;

/// <summary>Generic {value, label} option row for the SYS1107 pickers.</summary>
public class Sys1107OptionDto
{
    [JsonPropertyName("value")]
    public long Value { get; set; }

    [JsonPropertyName("label")]
    public string? Label { get; set; }

    public Sys1107OptionDto() { }

    public Sys1107OptionDto(long value, string? label)
    {
        Value = value;
        Label = label;
    }
}

/// <summary>User option in the SYS1107 user picker.</summary>
public class Sys1107UserOptionDto
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

    public Sys1107UserOptionDto() { }

    public Sys1107UserOptionDto(long userNo, string? userId, string? userName, long? employeeNo, short? accessScope)
    {
        UserNo = userNo;
        UserId = userId;
        UserName = userName;
        EmployeeNo = employeeNo;
        AccessScope = accessScope;
    }
}

/// <summary>One company grant from <c>sys_user_company</c>.</summary>
public class Sys1107MappingDto
{
    [JsonPropertyName("user_company_no")]
    public long? UserCompanyNo { get; set; }

    [JsonPropertyName("company_no")]
    public long? CompanyNo { get; set; }

    [JsonPropertyName("company_name")]
    public string? CompanyName { get; set; }

    [JsonPropertyName("is_default")]
    public short? IsDefault { get; set; }

    [JsonPropertyName("is_owner")]
    public short? IsOwner { get; set; }

    [JsonPropertyName("is_active")]
    public short? IsActive { get; set; }
}
