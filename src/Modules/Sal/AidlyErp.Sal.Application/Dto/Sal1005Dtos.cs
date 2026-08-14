using System.Text.Json.Serialization;

namespace AidlyErp.Sal.Application.Dto;

/// <summary>SAL_1005 POS Terminal Setup — one register.</summary>
public class Sal1005TerminalDto
{
    [JsonPropertyName("terminal_no")]
    public long? TerminalNo { get; set; }

    [JsonPropertyName("terminal_id")]
    public string? TerminalId { get; set; }

    [JsonPropertyName("terminal_name")]
    public string? TerminalName { get; set; }

    /// <summary>Stock source — sales on this till relieve this warehouse.</summary>
    [JsonPropertyName("warehouse_no")]
    public long WarehouseNo { get; set; }

    [JsonPropertyName("warehouse_name")]
    public string? WarehouseName { get; set; }

    [JsonPropertyName("device_uuid")]
    public string? DeviceUuid { get; set; }

    [JsonPropertyName("receipt_prefix")]
    public string? ReceiptPrefix { get; set; }

    [JsonPropertyName("cash_gl_account_no")]
    public long? CashGlAccountNo { get; set; }

    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }

    [JsonPropertyName("is_active")]
    public short? IsActive { get; set; }

    [JsonPropertyName("row_version")]
    public long? RowVersion { get; set; }
}

public class Sal1005LookupDto
{
    [JsonPropertyName("warehouses")]
    public List<SalOptionDto> Warehouses { get; set; } = new();
}
