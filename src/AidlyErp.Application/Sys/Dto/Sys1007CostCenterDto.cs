using System.Text.Json.Serialization;

namespace AidlyErp.Application.Sys.Dto;

/// <summary>DTO for the SYS_1007 Cost Center Setup form.</summary>
public class Sys1007CostCenterDto
{
    [JsonPropertyName("cost_center_no")]
    public long? CostCenterNo { get; set; }

    [JsonPropertyName("cost_center_id")]
    public string? CostCenterId { get; set; }

    [JsonPropertyName("cost_center_name")]
    public string? CostCenterName { get; set; }

    [JsonPropertyName("branch_no")]
    public long? BranchNo { get; set; }

    /// <summary>
    /// Write-only, and honoured for <b>root</b> cost centres only — a child always inherits its
    /// parent's branch so the whole subtree stays on one branch.
    /// </summary>
    [JsonPropertyName("branch_nos")]
    public List<long>? BranchNos { get; set; }

    [JsonPropertyName("parent_cost_center_no")]
    public long? ParentCostCenterNo { get; set; }

    [JsonPropertyName("is_active")]
    public short? IsActive { get; set; }

    [JsonPropertyName("row_version")]
    public long? RowVersion { get; set; }
}
