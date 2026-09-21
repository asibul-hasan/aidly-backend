using System.Text.Json.Serialization;

namespace AidlyErp.Sal.Application.Dto;

/// <summary>One day's trading, the row a shop owner scans first each morning.</summary>
public class SalDailySalesDto
{
    [JsonPropertyName("sale_date")]
    public DateTime SaleDate { get; set; }

    [JsonPropertyName("invoice_count")]
    public int InvoiceCount { get; set; }

    [JsonPropertyName("gross_sales")]
    public decimal GrossSales { get; set; }

    [JsonPropertyName("discount_total")]
    public decimal DiscountTotal { get; set; }

    [JsonPropertyName("tax_amount")]
    public decimal TaxAmount { get; set; }

    [JsonPropertyName("net_sales")]
    public decimal NetSales { get; set; }

    [JsonPropertyName("cost_total")]
    public decimal CostTotal { get; set; }

    /// <summary>Net sales less cost — the number that says whether the day was worth opening.</summary>
    [JsonPropertyName("margin")]
    public decimal Margin { get; set; }

    [JsonPropertyName("margin_pct")]
    public decimal MarginPct { get; set; }

    [JsonPropertyName("paid_total")]
    public decimal PaidTotal { get; set; }

    [JsonPropertyName("due_total")]
    public decimal DueTotal { get; set; }

    /// <summary>Average basket — net sales over invoice count.</summary>
    [JsonPropertyName("average_basket")]
    public decimal AverageBasket { get; set; }
}

public class SalProductSalesDto
{
    [JsonPropertyName("product_no")]
    public long ProductNo { get; set; }

    [JsonPropertyName("product_name")]
    public string? ProductName { get; set; }

    [JsonPropertyName("qty_sold")]
    public decimal QtySold { get; set; }

    [JsonPropertyName("net_sales")]
    public decimal NetSales { get; set; }

    [JsonPropertyName("cost_total")]
    public decimal CostTotal { get; set; }

    [JsonPropertyName("margin")]
    public decimal Margin { get; set; }

    [JsonPropertyName("margin_pct")]
    public decimal MarginPct { get; set; }
}

public class SalCashierSalesDto
{
    [JsonPropertyName("cashier_user_no")]
    public long CashierUserNo { get; set; }

    [JsonPropertyName("cashier_name")]
    public string? CashierName { get; set; }

    [JsonPropertyName("invoice_count")]
    public int InvoiceCount { get; set; }

    [JsonPropertyName("net_sales")]
    public decimal NetSales { get; set; }

    [JsonPropertyName("average_basket")]
    public decimal AverageBasket { get; set; }

    /// <summary>Sessions closed short or over — the figure worth a conversation.</summary>
    [JsonPropertyName("variance_total")]
    public decimal VarianceTotal { get; set; }
}

/// <summary>Takings by hour of day — what staffing decisions get made from.</summary>
public class SalHourlySalesDto
{
    [JsonPropertyName("hour")]
    public int Hour { get; set; }

    [JsonPropertyName("invoice_count")]
    public int InvoiceCount { get; set; }

    [JsonPropertyName("net_sales")]
    public decimal NetSales { get; set; }
}

public class SalSalesReportDto
{
    [JsonPropertyName("from_date")]
    public DateTime FromDate { get; set; }

    [JsonPropertyName("to_date")]
    public DateTime ToDate { get; set; }

    [JsonPropertyName("totals")]
    public SalDailySalesDto Totals { get; set; } = new();

    [JsonPropertyName("daily")]
    public List<SalDailySalesDto> Daily { get; set; } = new();

    [JsonPropertyName("by_product")]
    public List<SalProductSalesDto> ByProduct { get; set; } = new();

    [JsonPropertyName("by_cashier")]
    public List<SalCashierSalesDto> ByCashier { get; set; } = new();

    [JsonPropertyName("by_hour")]
    public List<SalHourlySalesDto> ByHour { get; set; } = new();
}
