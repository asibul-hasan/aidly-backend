using System.Text.Json.Serialization;

namespace AidlyErp.Application.Sys.Dto;

/// <summary>DTO for the SYS_1004 Currency Setup form.</summary>
public class Sys1004CurrencyDto
{
    [JsonPropertyName("currency_no")]
    public long? CurrencyNo { get; set; }

    [JsonPropertyName("company_no")]
    public long? CompanyNo { get; set; }

    [JsonPropertyName("currency_code")]
    public string? CurrencyCode { get; set; }

    [JsonPropertyName("currency_name")]
    public string? CurrencyName { get; set; }

    [JsonPropertyName("currency_symbol")]
    public string? CurrencySymbol { get; set; }

    [JsonPropertyName("fraction_name")]
    public string? FractionName { get; set; }

    [JsonPropertyName("decimal_places")]
    public short? DecimalPlaces { get; set; }

    /// <summary>1 = International (1,000,000), 0 = Indian (10,00,000).</summary>
    [JsonPropertyName("number_system")]
    public short? NumberSystem { get; set; }

    [JsonPropertyName("exchange_rate")]
    public decimal? ExchangeRate { get; set; }

    [JsonPropertyName("is_base_currency")]
    public short? IsBaseCurrency { get; set; }

    /// <summary>
    /// Write-only. Insert creates one currency row per listed branch; empty or absent means a
    /// single company-wide row (<c>branch_no = NULL</c>) that applies to every branch.
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

/// <summary>Lightweight currency list for common use across forms — no exchange rate history.</summary>
public class CurrencyLookupDto
{
    [JsonPropertyName("currency_no")]
    public long CurrencyNo { get; set; }

    [JsonPropertyName("currency_code")]
    public string? CurrencyCode { get; set; }

    [JsonPropertyName("currency_name")]
    public string? CurrencyName { get; set; }

    [JsonPropertyName("currency_symbol")]
    public string? CurrencySymbol { get; set; }

    [JsonPropertyName("fraction_name")]
    public string? FractionName { get; set; }

    [JsonPropertyName("decimal_places")]
    public short DecimalPlaces { get; set; }

    [JsonPropertyName("number_system")]
    public short NumberSystem { get; set; }

    [JsonPropertyName("exchange_rate")]
    public decimal ExchangeRate { get; set; }

    [JsonPropertyName("is_base_currency")]
    public short IsBaseCurrency { get; set; }
}

/// <summary>
/// Base-currency formatting settings for the active branch. The frontend layout reads this on
/// boot to configure number/currency display ERP-wide.
/// </summary>
public class BaseCurrencySettingsDto
{
    [JsonPropertyName("currency_code")]
    public string? CurrencyCode { get; set; }

    [JsonPropertyName("currency_name")]
    public string? CurrencyName { get; set; }

    [JsonPropertyName("currency_symbol")]
    public string? CurrencySymbol { get; set; }

    /// <summary>"International" or "Indian", derived from <c>number_system</c>.</summary>
    [JsonPropertyName("number_format")]
    public string? NumberFormat { get; set; }

    [JsonPropertyName("decimal_places")]
    public short DecimalPlaces { get; set; }
}
