using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AidlyErp.Shared.Core;

namespace AidlyErp.Sal.Domain;

/// <summary>Pricing tier and default terms a customer inherits from its group.</summary>
[Table("sal_customer_group")]
public class SalCustomerGroup : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("customer_group_no")]
    public long CustomerGroupNo { get; set; }

    [Column("company_no")]
    public long CompanyNo { get; set; }

    [Column("group_id")]
    [StringLength(30)]
    public string GroupId { get; set; } = string.Empty;

    [Column("group_name")]
    [StringLength(150)]
    public string GroupName { get; set; } = string.Empty;

    /// <summary>1=Retail 2=Wholesale 3=Special.</summary>
    [Column("price_tier")]
    public short PriceTier { get; set; } = 1;

    [Column("default_credit_days")]
    public int DefaultCreditDays { get; set; }

    [Column("default_discount_pct")]
    public decimal DefaultDiscountPct { get; set; }

    [Column("remarks")]
    public string? Remarks { get; set; }
}

/// <summary>
/// A discount rule: what it gives, who it applies to, when it runs and how often.
/// Evaluated by <c>SalPromotionEngine</c> as step 3 of pricing.
/// </summary>
[Table("sal_promotion")]
public class SalPromotion : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("promotion_no")]
    public long PromotionNo { get; set; }

    [Column("company_no")]
    public long CompanyNo { get; set; }

    /// <summary>Null runs the promotion in every branch.</summary>
    [Column("branch_no")]
    public long? BranchNo { get; set; }

    [Column("promotion_id")]
    [StringLength(30)]
    public string PromotionId { get; set; } = string.Empty;

    [Column("promotion_name")]
    [StringLength(150)]
    public string PromotionName { get; set; } = string.Empty;

    /// <summary>
    /// 1=LinePct 2=LineAmount 3=BillPct 4=BillAmount 5=BuyXGetY 6=QtyBreak 7=Coupon 8=Bundle.
    /// The engine currently fires 1–5 and skips the rest.
    /// </summary>
    [Column("promo_type")]
    public short PromoType { get; set; }

    /// <summary>1=Product 2=Category 3=Brand 4=All 5=Customer/Group.</summary>
    [Column("scope_type")]
    public short ScopeType { get; set; } = 1;

    [Column("coupon_code")]
    [StringLength(40)]
    public string? CouponCode { get; set; }

    /// <summary>Higher runs first; a non-stackable hit then closes the line to others.</summary>
    [Column("priority")]
    public short Priority { get; set; }

    [Column("is_stackable")]
    public short IsStackable { get; set; }

    [Column("min_qty")]
    public decimal? MinQty { get; set; }

    [Column("min_amount")]
    public decimal? MinAmount { get; set; }

    [Column("discount_pct")]
    public decimal? DiscountPct { get; set; }

    [Column("discount_amount")]
    public decimal? DiscountAmount { get; set; }

    [Column("buy_qty")]
    public decimal? BuyQty { get; set; }

    [Column("get_qty")]
    public decimal? GetQty { get; set; }

    /// <summary>Caps what a percentage rule can give away on a large basket.</summary>
    [Column("max_discount_amount")]
    public decimal? MaxDiscountAmount { get; set; }

    [Column("customer_type")]
    public short? CustomerType { get; set; }

    [Column("price_tier")]
    public short? PriceTier { get; set; }

    [Column("start_date")]
    public DateTime StartDate { get; set; }

    [Column("end_date")]
    public DateTime EndDate { get; set; }

    [Column("start_time")]
    public TimeSpan? StartTime { get; set; }

    [Column("end_time")]
    public TimeSpan? EndTime { get; set; }

    /// <summary>Bit 0 = Monday … bit 6 = Sunday. Null runs every day.</summary>
    [Column("weekday_mask")]
    public short? WeekdayMask { get; set; }

    [Column("usage_limit")]
    public int? UsageLimit { get; set; }

    [Column("used_count")]
    public int UsedCount { get; set; }

    [Column("remarks")]
    public string? Remarks { get; set; }
}

/// <summary>
/// What a promotion targets. For BuyXGetY, <c>target_role</c> separates the items that must be
/// bought from the ones given away.
/// </summary>
[Table("sal_promotion_dtl")]
public class SalPromotionDtl
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("promotion_dtl_no")]
    public long PromotionDtlNo { get; set; }

    [Column("promotion_no")]
    public long PromotionNo { get; set; }

    /// <summary>1=Condition (buy) 2=Reward (get).</summary>
    [Column("target_role")]
    public short TargetRole { get; set; } = 1;

    [Column("product_no")]
    public long? ProductNo { get; set; }

    [Column("variant_no")]
    public long? VariantNo { get; set; }

    [Column("category_no")]
    public long? CategoryNo { get; set; }

    [Column("brand_no")]
    public long? BrandNo { get; set; }

    [Column("qty")]
    public decimal? Qty { get; set; }

    [Column("row_version")]
    public long RowVersion { get; set; } = 1;
}
