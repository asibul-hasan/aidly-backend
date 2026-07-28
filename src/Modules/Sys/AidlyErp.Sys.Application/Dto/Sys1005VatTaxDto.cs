using System.Text.Json.Serialization;

namespace AidlyErp.Sys.Application.Dto;

/// <summary>DTO for the SYS_1005 VAT / Tax Setup form.</summary>
public class Sys1005VatTaxDto
{
    [JsonPropertyName("vat_tax_no")]
    public long? VatTaxNo { get; set; }

    [JsonPropertyName("company_no")]
    public long? CompanyNo { get; set; }

    [JsonPropertyName("tax_code")]
    public string? TaxCode { get; set; }

    [JsonPropertyName("tax_name")]
    public string? TaxName { get; set; }

    [JsonPropertyName("tax_type")]
    public short? TaxType { get; set; }

    [JsonPropertyName("rate_percentage")]
    public decimal? RatePercentage { get; set; }

    [JsonPropertyName("effective_from")]
    public DateOnly? EffectiveFrom { get; set; }

    [JsonPropertyName("effective_to")]
    public DateOnly? EffectiveTo { get; set; }

    [JsonPropertyName("gl_account_no")]
    public long? GlAccountNo { get; set; }

    [JsonPropertyName("authority_name")]
    public string? AuthorityName { get; set; }

    /// <summary>
    /// Write-only. Insert creates one record per listed branch; empty or absent means a single
    /// company-wide record (<c>branch_no = NULL</c>) that applies to every branch.
    /// </summary>
    [JsonPropertyName("branch_nos")]
    public List<long>? BranchNos { get; set; }

    [JsonPropertyName("branch_no")]
    public long? BranchNo { get; set; }

    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }

    [JsonPropertyName("is_active")]
    public short? IsActive { get; set; }

    [JsonPropertyName("row_version")]
    public long? RowVersion { get; set; }
}
