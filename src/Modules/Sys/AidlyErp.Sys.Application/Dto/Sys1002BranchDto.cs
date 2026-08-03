using System.Text.Json.Serialization;

namespace AidlyErp.Sys.Application.Dto;

/// <summary>Form-specific DTO for the SYS1002 Branch Setup form.</summary>
public class Sys1002BranchDto
{
    /// <summary>Server-assigned; ignored on write (Java marks it <c>Access.READ_ONLY</c>).</summary>
    [JsonPropertyName("branch_no")]
    public long? BranchNo { get; set; }

    /// <summary>Server-assigned from the tenant context; ignored on write.</summary>
    [JsonPropertyName("company_no")]
    public long? CompanyNo { get; set; }

    [JsonPropertyName("branch_id")]
    public string? BranchId { get; set; }

    [JsonPropertyName("branch_name")]
    public string? BranchName { get; set; }

    [JsonPropertyName("branch_name_nls")]
    public string? BranchNameNls { get; set; }

    [JsonPropertyName("branch_type")]
    public string? BranchType { get; set; }

    [JsonPropertyName("branch_addr1")]
    public string? BranchAddr1 { get; set; }

    [JsonPropertyName("branch_addr2")]
    public string? BranchAddr2 { get; set; }

    [JsonPropertyName("city")]
    public string? City { get; set; }

    [JsonPropertyName("post_code")]
    public string? PostCode { get; set; }

    [JsonPropertyName("mobile_no")]
    public string? MobileNo { get; set; }

    [JsonPropertyName("contact_no")]
    public string? ContactNo { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("manager_employee_no")]
    public long? ManagerEmployeeNo { get; set; }

    /// <summary>Resolved display name of the manager; not persisted.</summary>
    [JsonPropertyName("manager_name")]
    public string? ManagerName { get; set; }

    [JsonPropertyName("is_main_branch")]
    public short? IsMainBranch { get; set; }

    [JsonPropertyName("is_active")]
    public short? IsActive { get; set; }

    [JsonPropertyName("row_version")]
    public long? RowVersion { get; set; }
}
