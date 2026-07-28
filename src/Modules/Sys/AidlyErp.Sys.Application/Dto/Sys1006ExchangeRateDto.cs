using System.Text.Json.Serialization;

namespace AidlyErp.Sys.Application.Dto;

/// <summary>DTO for the SYS_1006 Exchange Rate Setup form.</summary>
public class Sys1006ExchangeRateDto
{
    [JsonPropertyName("exchange_rate_no")]
    public long? ExchangeRateNo { get; set; }

    [JsonPropertyName("currency_no")]
    public long? CurrencyNo { get; set; }

    /// <summary>Display label, resolved as "<c>CODE - Name</c>"; not persisted.</summary>
    [JsonPropertyName("currency_label")]
    public string? CurrencyLabel { get; set; }

    [JsonPropertyName("rate_date")]
    public DateOnly? RateDate { get; set; }

    [JsonPropertyName("rate")]
    public decimal? Rate { get; set; }

    [JsonPropertyName("is_active")]
    public short? IsActive { get; set; }

    [JsonPropertyName("row_version")]
    public long? RowVersion { get; set; }
}

/// <summary>Generic {value, label} option row used by SYS dropdowns.</summary>
public class SysLookupDto
{
    [JsonPropertyName("value")]
    public long Value { get; set; }

    [JsonPropertyName("label")]
    public string? Label { get; set; }

    public SysLookupDto() { }

    public SysLookupDto(long value, string? label)
    {
        Value = value;
        Label = label;
    }
}
