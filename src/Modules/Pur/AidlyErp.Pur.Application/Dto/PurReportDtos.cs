using System.Text.Json.Serialization;

namespace AidlyErp.Pur.Application.Dto;

/// <summary>
/// One supplier's outstanding payables, bucketed by how overdue each bill is. The buckets are what
/// a payment run is planned from, so they age from the due date, not the invoice date.
/// </summary>
public class PurSupplierAgingDto
{
    [JsonPropertyName("supplier_no")]
    public long SupplierNo { get; set; }

    [JsonPropertyName("supplier_name")]
    public string? SupplierName { get; set; }

    /// <summary>Not yet due.</summary>
    [JsonPropertyName("current_amount")]
    public decimal CurrentAmount { get; set; }

    [JsonPropertyName("days_1_30")]
    public decimal Days1To30 { get; set; }

    [JsonPropertyName("days_31_60")]
    public decimal Days31To60 { get; set; }

    [JsonPropertyName("days_61_90")]
    public decimal Days61To90 { get; set; }

    [JsonPropertyName("days_over_90")]
    public decimal DaysOver90 { get; set; }

    [JsonPropertyName("total_due")]
    public decimal TotalDue { get; set; }

    /// <summary>Age of the oldest unpaid bill — the number that says how bad it has got.</summary>
    [JsonPropertyName("oldest_days")]
    public int OldestDays { get; set; }

    [JsonPropertyName("invoice_count")]
    public int InvoiceCount { get; set; }
}

public class PurSpendRowDto
{
    [JsonPropertyName("key_no")]
    public long KeyNo { get; set; }

    [JsonPropertyName("label")]
    public string? Label { get; set; }

    [JsonPropertyName("invoice_count")]
    public int InvoiceCount { get; set; }

    [JsonPropertyName("spend")]
    public decimal Spend { get; set; }

    /// <summary>Share of total spend in the period.</summary>
    [JsonPropertyName("share_pct")]
    public decimal SharePct { get; set; }
}

public class PurAgingReportDto
{
    [JsonPropertyName("as_of")]
    public DateTime AsOf { get; set; }

    [JsonPropertyName("total_due")]
    public decimal TotalDue { get; set; }

    [JsonPropertyName("rows")]
    public List<PurSupplierAgingDto> Rows { get; set; } = new();
}

public class PurSpendReportDto
{
    [JsonPropertyName("from_date")]
    public DateTime FromDate { get; set; }

    [JsonPropertyName("to_date")]
    public DateTime ToDate { get; set; }

    [JsonPropertyName("total_spend")]
    public decimal TotalSpend { get; set; }

    [JsonPropertyName("by_supplier")]
    public List<PurSpendRowDto> BySupplier { get; set; } = new();

    [JsonPropertyName("by_category")]
    public List<PurSpendRowDto> ByCategory { get; set; } = new();
}
