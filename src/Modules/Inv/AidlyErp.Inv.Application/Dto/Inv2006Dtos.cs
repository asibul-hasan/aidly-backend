using System.Text.Json.Serialization;

namespace AidlyErp.Inv.Application.Dto;

public class Inv2006CountLineDto
{
    [JsonPropertyName("count_dtl_no")]
    public long? CountDtlNo { get; set; }

    [JsonPropertyName("line_no")]
    public int LineNo { get; set; }

    [JsonPropertyName("product_no")]
    public long ProductNo { get; set; }

    [JsonPropertyName("product_name")]
    public string? ProductName { get; set; }

    [JsonPropertyName("variant_no")]
    public long? VariantNo { get; set; }

    [JsonPropertyName("batch_no")]
    public long? BatchNo { get; set; }

    [JsonPropertyName("batch_code")]
    public string? BatchCode { get; set; }

    /// <summary>What the system held when the sheet was opened. Read-only to the counter.</summary>
    [JsonPropertyName("system_qty")]
    public decimal SystemQty { get; set; }

    [JsonPropertyName("counted_qty")]
    public decimal CountedQty { get; set; }

    [JsonPropertyName("variance_qty")]
    public decimal VarianceQty { get; set; }

    [JsonPropertyName("unit_cost")]
    public decimal UnitCost { get; set; }

    /// <summary>Variance valued at cost — what the discrepancy is worth.</summary>
    [JsonPropertyName("variance_value")]
    public decimal VarianceValue { get; set; }

    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }
}

public class Inv2006CountDto
{
    [JsonPropertyName("count_no")]
    public long? CountNo { get; set; }

    [JsonPropertyName("count_id")]
    public string? CountId { get; set; }

    [JsonPropertyName("count_date")]
    public DateTime CountDate { get; set; }

    [JsonPropertyName("warehouse_no")]
    public long WarehouseNo { get; set; }

    [JsonPropertyName("warehouse_name")]
    public string? WarehouseName { get; set; }

    /// <summary>1=Full 2=Cycle 3=Spot.</summary>
    [JsonPropertyName("count_type")]
    public short CountType { get; set; } = 1;

    /// <summary>1=Draft 2=Counting 3=Review 4=Posted 5=Cancelled.</summary>
    [JsonPropertyName("status")]
    public short Status { get; set; } = 1;

    [JsonPropertyName("freeze_stock")]
    public short FreezeStock { get; set; }

    [JsonPropertyName("variance_adjustment_no")]
    public long? VarianceAdjustmentNo { get; set; }

    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }

    [JsonPropertyName("is_active")]
    public short? IsActive { get; set; }

    [JsonPropertyName("row_version")]
    public long? RowVersion { get; set; }

    // ── review figures ───────────────────────────────────────────────────────

    [JsonPropertyName("line_count")]
    public int LineCount { get; set; }

    /// <summary>Lines where counted differs from system — the ones worth looking at.</summary>
    [JsonPropertyName("variance_line_count")]
    public int VarianceLineCount { get; set; }

    [JsonPropertyName("variance_value_total")]
    public decimal VarianceValueTotal { get; set; }

    [JsonPropertyName("lines")]
    public List<Inv2006CountLineDto> Lines { get; set; } = new();
}

/// <summary>Opens a sheet by snapshotting what the warehouse currently holds.</summary>
public class Inv2006StartCountDto
{
    [JsonPropertyName("warehouse_no")]
    public long WarehouseNo { get; set; }

    [JsonPropertyName("count_date")]
    public DateTime CountDate { get; set; }

    [JsonPropertyName("count_type")]
    public short CountType { get; set; } = 1;

    [JsonPropertyName("freeze_stock")]
    public short FreezeStock { get; set; }

    /// <summary>
    /// Limits a cycle or spot count to these products. Empty snapshots the whole warehouse.
    /// </summary>
    [JsonPropertyName("product_nos")]
    public List<long> ProductNos { get; set; } = new();

    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }
}
