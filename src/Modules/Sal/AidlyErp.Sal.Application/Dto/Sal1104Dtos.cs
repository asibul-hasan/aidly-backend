using System.Text.Json.Serialization;

namespace AidlyErp.Sal.Application.Dto;

/// <summary>One product / category / brand a promotion targets.</summary>
public class Sal1104TargetDto
{
    [JsonPropertyName("promotion_dtl_no")]
    public long? PromotionDtlNo { get; set; }

    /// <summary>1=Condition (must be bought) 2=Reward (given free).</summary>
    [JsonPropertyName("target_role")]
    public short TargetRole { get; set; } = 1;

    [JsonPropertyName("product_no")]
    public long? ProductNo { get; set; }

    [JsonPropertyName("product_name")]
    public string? ProductName { get; set; }

    [JsonPropertyName("variant_no")]
    public long? VariantNo { get; set; }

    [JsonPropertyName("category_no")]
    public long? CategoryNo { get; set; }

    [JsonPropertyName("brand_no")]
    public long? BrandNo { get; set; }

    [JsonPropertyName("qty")]
    public decimal? Qty { get; set; }
}

public class Sal1104PromotionDto
{
    [JsonPropertyName("promotion_no")]
    public long? PromotionNo { get; set; }

    [JsonPropertyName("promotion_id")]
    public string? PromotionId { get; set; }

    [JsonPropertyName("promotion_name")]
    public string? PromotionName { get; set; }

    /// <summary>1=LinePct 2=LineAmount 3=BillPct 4=BillAmount 5=BuyXGetY (6–8 not yet applied).</summary>
    [JsonPropertyName("promo_type")]
    public short PromoType { get; set; } = 1;

    /// <summary>1=Product 2=Category 3=Brand 4=All 5=Customer/Group.</summary>
    [JsonPropertyName("scope_type")]
    public short ScopeType { get; set; } = 1;

    [JsonPropertyName("branch_no")]
    public long? BranchNo { get; set; }

    [JsonPropertyName("coupon_code")]
    public string? CouponCode { get; set; }

    [JsonPropertyName("priority")]
    public short Priority { get; set; }

    [JsonPropertyName("is_stackable")]
    public short IsStackable { get; set; }

    [JsonPropertyName("min_qty")]
    public decimal? MinQty { get; set; }

    [JsonPropertyName("min_amount")]
    public decimal? MinAmount { get; set; }

    [JsonPropertyName("discount_pct")]
    public decimal? DiscountPct { get; set; }

    [JsonPropertyName("discount_amount")]
    public decimal? DiscountAmount { get; set; }

    [JsonPropertyName("buy_qty")]
    public decimal? BuyQty { get; set; }

    [JsonPropertyName("get_qty")]
    public decimal? GetQty { get; set; }

    [JsonPropertyName("max_discount_amount")]
    public decimal? MaxDiscountAmount { get; set; }

    [JsonPropertyName("customer_type")]
    public short? CustomerType { get; set; }

    [JsonPropertyName("price_tier")]
    public short? PriceTier { get; set; }

    [JsonPropertyName("start_date")]
    public DateTime StartDate { get; set; }

    [JsonPropertyName("end_date")]
    public DateTime EndDate { get; set; }

    [JsonPropertyName("start_time")]
    public string? StartTime { get; set; }

    [JsonPropertyName("end_time")]
    public string? EndTime { get; set; }

    /// <summary>Bit 0 = Monday … bit 6 = Sunday. Null or 0 runs every day.</summary>
    [JsonPropertyName("weekday_mask")]
    public short? WeekdayMask { get; set; }

    [JsonPropertyName("usage_limit")]
    public int? UsageLimit { get; set; }

    [JsonPropertyName("used_count")]
    public int UsedCount { get; set; }

    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }

    [JsonPropertyName("is_active")]
    public short? IsActive { get; set; }

    [JsonPropertyName("row_version")]
    public long? RowVersion { get; set; }

    /// <summary>True when the type is one the engine does not fire yet — the form warns on it.</summary>
    [JsonPropertyName("is_supported")]
    public bool IsSupported { get; set; } = true;

    [JsonPropertyName("targets")]
    public List<Sal1104TargetDto> Targets { get; set; } = new();
}

public class Sal1104LookupDto
{
    [JsonPropertyName("products")]
    public List<SalOptionDto> Products { get; set; } = new();

    [JsonPropertyName("categories")]
    public List<SalOptionDto> Categories { get; set; } = new();

    [JsonPropertyName("brands")]
    public List<SalOptionDto> Brands { get; set; } = new();
}
