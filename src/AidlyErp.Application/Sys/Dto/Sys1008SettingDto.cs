using System.Text.Json.Serialization;

namespace AidlyErp.Application.Sys.Dto;

/// <summary>DTO for the SYS_1008 System Settings form.</summary>
public class Sys1008SettingDto
{
    [JsonPropertyName("setting_no")]
    public long? SettingNo { get; set; }

    [JsonPropertyName("branch_no")]
    public long? BranchNo { get; set; }

    /// <summary>
    /// Write-only. Insert creates one record per listed branch; empty or absent means a single
    /// company-wide record (<c>branch_no = NULL</c>).
    /// </summary>
    [JsonPropertyName("branch_nos")]
    public List<long>? BranchNos { get; set; }

    [JsonPropertyName("setting_key")]
    public string? SettingKey { get; set; }

    [JsonPropertyName("setting_value")]
    public string? SettingValue { get; set; }

    /// <summary>1=String, 2=Number, 3=Boolean, 4=JSON, 5=Date.</summary>
    [JsonPropertyName("value_type")]
    public short? ValueType { get; set; }

    [JsonPropertyName("setting_group")]
    public string? SettingGroup { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("is_active")]
    public short? IsActive { get; set; }

    [JsonPropertyName("row_version")]
    public long? RowVersion { get; set; }
}
