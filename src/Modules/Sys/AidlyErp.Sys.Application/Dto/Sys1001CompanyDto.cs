using System.Text.Json.Serialization;

namespace AidlyErp.Sys.Application.Dto;

/// <summary>
/// Form-specific DTO for the SYS1001 Company Setup form.
/// Contains only the <c>sys_company</c> fields the form needs.
/// </summary>
public class Sys1001CompanyDto
{
    /// <summary>Server-assigned; ignored on write (Java marks it <c>Access.READ_ONLY</c>).</summary>
    [JsonPropertyName("company_no")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? CompanyNo { get; set; }

    [JsonPropertyName("company_id")]
    public string? CompanyId { get; set; }

    [JsonPropertyName("company_name")]
    public string? CompanyName { get; set; }

    [JsonPropertyName("company_name_nls")]
    public string? CompanyNameNls { get; set; }

    [JsonPropertyName("company_type")]
    public string? CompanyType { get; set; }

    [JsonPropertyName("trade_license_no")]
    public string? TradeLicenseNo { get; set; }

    [JsonPropertyName("vat_reg_no")]
    public string? VatRegNo { get; set; }

    [JsonPropertyName("tin_no")]
    public string? TinNo { get; set; }

    [JsonPropertyName("bin_no")]
    public string? BinNo { get; set; }

    [JsonPropertyName("reg_no")]
    public string? RegNo { get; set; }

    [JsonPropertyName("company_addr1")]
    public string? CompanyAddr1 { get; set; }

    [JsonPropertyName("company_addr2")]
    public string? CompanyAddr2 { get; set; }

    [JsonPropertyName("city")]
    public string? City { get; set; }

    [JsonPropertyName("state_province")]
    public string? StateProvince { get; set; }

    [JsonPropertyName("post_code")]
    public string? PostCode { get; set; }

    [JsonPropertyName("country_code")]
    public string? CountryCode { get; set; }

    [JsonPropertyName("mobile_no")]
    public string? MobileNo { get; set; }

    [JsonPropertyName("contact_no")]
    public string? ContactNo { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("website")]
    public string? Website { get; set; }

    [JsonPropertyName("logo_path")]
    public string? LogoPath { get; set; }

    [JsonPropertyName("is_active")]
    public short? IsActive { get; set; }

    [JsonPropertyName("row_version")]
    public long? RowVersion { get; set; }
}
