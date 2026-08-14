using System.Text.Json.Serialization;

namespace AidlyErp.Sal.Application.Dto;

// ── pricing ──────────────────────────────────────────────────────────────────

/// <summary>One cart line as the till sends it — quantity and intent, never money.</summary>
public class SalPriceLineRequestDto
{
    [JsonPropertyName("product_no")]
    public long ProductNo { get; set; }

    [JsonPropertyName("variant_no")]
    public long? VariantNo { get; set; }

    [JsonPropertyName("uom_no")]
    public long? UomNo { get; set; }

    [JsonPropertyName("qty")]
    public decimal Qty { get; set; }

    /// <summary>Optional manual override; rejected below the product's floor without approval.</summary>
    [JsonPropertyName("unit_price")]
    public decimal? UnitPrice { get; set; }

    /// <summary>1=Amount 2=Percent.</summary>
    [JsonPropertyName("line_discount_type")]
    public short LineDiscountType { get; set; } = 1;

    [JsonPropertyName("line_discount_value")]
    public decimal LineDiscountValue { get; set; }

    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }
}

/// <summary>A cart to price. The response is authoritative; the screen only displays it.</summary>
public class SalQuotePriceRequestDto
{
    [JsonPropertyName("customer_no")]
    public long? CustomerNo { get; set; }

    /// <summary>1=Amount 2=Percent.</summary>
    [JsonPropertyName("bill_discount_type")]
    public short BillDiscountType { get; set; } = 1;

    [JsonPropertyName("bill_discount_value")]
    public decimal BillDiscountValue { get; set; }

    [JsonPropertyName("shipping_charge")]
    public decimal ShippingCharge { get; set; }

    /// <summary>Set when the cashier has re-authenticated to go below the min-price floor.</summary>
    [JsonPropertyName("allow_below_min_price")]
    public bool AllowBelowMinPrice { get; set; }

    [JsonPropertyName("lines")]
    public List<SalPriceLineRequestDto> Lines { get; set; } = new();
}

public class SalPricedLineDto
{
    [JsonPropertyName("product_no")]
    public long ProductNo { get; set; }

    [JsonPropertyName("product_name")]
    public string? ProductName { get; set; }

    [JsonPropertyName("variant_no")]
    public long? VariantNo { get; set; }

    [JsonPropertyName("uom_no")]
    public long UomNo { get; set; }

    [JsonPropertyName("qty")]
    public decimal Qty { get; set; }

    [JsonPropertyName("qty_base")]
    public decimal QtyBase { get; set; }

    [JsonPropertyName("unit_price")]
    public decimal UnitPrice { get; set; }

    [JsonPropertyName("mrp")]
    public decimal? Mrp { get; set; }

    [JsonPropertyName("line_discount_amount")]
    public decimal LineDiscountAmount { get; set; }

    /// <summary>Discount an automatic promotion gave this line.</summary>
    [JsonPropertyName("promotion_discount")]
    public decimal PromotionDiscount { get; set; }

    [JsonPropertyName("promotion_no")]
    public long? PromotionNo { get; set; }

    /// <summary>1 when the line is a promotion giveaway: zero price, real stock movement.</summary>
    [JsonPropertyName("is_free_item")]
    public short IsFreeItem { get; set; }

    /// <summary>This line's share of the header discount, spread by value.</summary>
    [JsonPropertyName("bill_discount_share")]
    public decimal BillDiscountShare { get; set; }

    [JsonPropertyName("vat_tax_no")]
    public long? VatTaxNo { get; set; }

    [JsonPropertyName("tax_rate_pct")]
    public decimal TaxRatePct { get; set; }

    [JsonPropertyName("is_tax_inclusive")]
    public short IsTaxInclusive { get; set; }

    [JsonPropertyName("taxable_amount")]
    public decimal TaxableAmount { get; set; }

    [JsonPropertyName("tax_amount")]
    public decimal TaxAmount { get; set; }

    [JsonPropertyName("line_total")]
    public decimal LineTotal { get; set; }

    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }
}

public class SalQuotePriceResultDto
{
    [JsonPropertyName("sub_total")]
    public decimal SubTotal { get; set; }

    [JsonPropertyName("line_discount_total")]
    public decimal LineDiscountTotal { get; set; }

    [JsonPropertyName("bill_discount_amount")]
    public decimal BillDiscountAmount { get; set; }

    /// <summary>Total given away by automatic promotions, line and bill level combined.</summary>
    [JsonPropertyName("promotion_discount")]
    public decimal PromotionDiscount { get; set; }

    /// <summary>Promotions that fired, so the confirm path can bump their usage counts.</summary>
    [JsonPropertyName("applied_promotions")]
    public List<long> AppliedPromotions { get; set; } = new();

    [JsonPropertyName("taxable_amount")]
    public decimal TaxableAmount { get; set; }

    [JsonPropertyName("tax_amount")]
    public decimal TaxAmount { get; set; }

    [JsonPropertyName("shipping_charge")]
    public decimal ShippingCharge { get; set; }

    /// <summary>Rounding to the currency's smallest unit; stored so the GL ties out.</summary>
    [JsonPropertyName("round_off")]
    public decimal RoundOff { get; set; }

    [JsonPropertyName("grand_total")]
    public decimal GrandTotal { get; set; }

    [JsonPropertyName("lines")]
    public List<SalPricedLineDto> Lines { get; set; } = new();
}

// ── tender ───────────────────────────────────────────────────────────────────

public class SalTenderDto
{
    [JsonPropertyName("payment_method")]
    public short PaymentMethod { get; set; }

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    /// <summary>Cash handed over; change is tendered − amount.</summary>
    [JsonPropertyName("tendered_amount")]
    public decimal? TenderedAmount { get; set; }

    [JsonPropertyName("card_last4")]
    public string? CardLast4 { get; set; }

    [JsonPropertyName("card_type")]
    public string? CardType { get; set; }

    [JsonPropertyName("approval_code")]
    public string? ApprovalCode { get; set; }

    [JsonPropertyName("mobile_provider")]
    public string? MobileProvider { get; set; }

    [JsonPropertyName("txn_ref")]
    public string? TxnRef { get; set; }

    [JsonPropertyName("bank_no")]
    public long? BankNo { get; set; }

    [JsonPropertyName("cheque_no")]
    public string? ChequeNo { get; set; }

    [JsonPropertyName("cheque_date")]
    public DateTime? ChequeDate { get; set; }

    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }
}

/// <summary>
/// A till ringing up a sale. Carries quantities, tenders and an idempotency key — never totals,
/// which the server derives from the catalogue.
/// </summary>
public class SalPosSaleRequestDto
{
    /// <summary>
    /// Generated by the till before the first submit attempt and reused on every retry. This is
    /// what makes a double-click, a flaky network or a replayed offline sale safe.
    /// </summary>
    [JsonPropertyName("client_uuid")]
    public Guid ClientUuid { get; set; }

    [JsonPropertyName("terminal_no")]
    public long TerminalNo { get; set; }

    [JsonPropertyName("customer_no")]
    public long CustomerNo { get; set; }

    [JsonPropertyName("bill_discount_type")]
    public short BillDiscountType { get; set; } = 1;

    [JsonPropertyName("bill_discount_value")]
    public decimal BillDiscountValue { get; set; }

    [JsonPropertyName("shipping_charge")]
    public decimal ShippingCharge { get; set; }

    /// <summary>Set once a manager has re-authenticated for a below-floor price.</summary>
    [JsonPropertyName("allow_below_min_price")]
    public bool AllowBelowMinPrice { get; set; }

    [JsonPropertyName("salesperson_employee_no")]
    public long? SalespersonEmployeeNo { get; set; }

    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }

    [JsonPropertyName("lines")]
    public List<SalPriceLineRequestDto> Lines { get; set; } = new();

    [JsonPropertyName("tenders")]
    public List<SalTenderDto> Tenders { get; set; } = new();
}

// ── session ──────────────────────────────────────────────────────────────────

public class SalPosSessionDto
{
    [JsonPropertyName("session_no")]
    public long SessionNo { get; set; }

    [JsonPropertyName("session_id")]
    public string? SessionId { get; set; }

    [JsonPropertyName("terminal_no")]
    public long TerminalNo { get; set; }

    [JsonPropertyName("terminal_name")]
    public string? TerminalName { get; set; }

    [JsonPropertyName("warehouse_no")]
    public long WarehouseNo { get; set; }

    [JsonPropertyName("cashier_user_no")]
    public long CashierUserNo { get; set; }

    [JsonPropertyName("opened_at")]
    public DateTime OpenedAt { get; set; }

    [JsonPropertyName("opening_float")]
    public decimal OpeningFloat { get; set; }

    [JsonPropertyName("closed_at")]
    public DateTime? ClosedAt { get; set; }

    [JsonPropertyName("expected_cash")]
    public decimal? ExpectedCash { get; set; }

    [JsonPropertyName("counted_cash")]
    public decimal? CountedCash { get; set; }

    [JsonPropertyName("cash_variance")]
    public decimal? CashVariance { get; set; }

    [JsonPropertyName("total_sales")]
    public decimal TotalSales { get; set; }

    [JsonPropertyName("total_returns")]
    public decimal TotalReturns { get; set; }

    [JsonPropertyName("invoice_count")]
    public int InvoiceCount { get; set; }

    [JsonPropertyName("status")]
    public short Status { get; set; }

    [JsonPropertyName("variance_remarks")]
    public string? VarianceRemarks { get; set; }
}

/// <summary>One tender method's take for a session — the body of the Z-report.</summary>
public class SalPosTenderTotalDto
{
    [JsonPropertyName("payment_method")]
    public short PaymentMethod { get; set; }

    [JsonPropertyName("method_name")]
    public string MethodName { get; set; } = string.Empty;

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }
}

public class SalPosSessionSummaryDto
{
    [JsonPropertyName("session")]
    public SalPosSessionDto? Session { get; set; }

    [JsonPropertyName("tenders")]
    public List<SalPosTenderTotalDto> Tenders { get; set; } = new();

    [JsonPropertyName("expected_cash")]
    public decimal ExpectedCash { get; set; }

    /// <summary>Opening float plus expected cash — what should physically be in the drawer.</summary>
    [JsonPropertyName("expected_drawer")]
    public decimal ExpectedDrawer { get; set; }

    [JsonPropertyName("total_sales")]
    public decimal TotalSales { get; set; }

    [JsonPropertyName("total_returns")]
    public decimal TotalReturns { get; set; }

    [JsonPropertyName("invoice_count")]
    public int InvoiceCount { get; set; }

    /// <summary>Non-cash takings — reported so the Z-report reconciles to the sales total.</summary>
    [JsonPropertyName("non_cash_total")]
    public decimal NonCashTotal { get; set; }

    /// <summary>Sale value settled on credit rather than tendered; explains sales ≠ tenders.</summary>
    [JsonPropertyName("due_total")]
    public decimal DueTotal { get; set; }
}

public class SalOpenSessionRequestDto
{
    [JsonPropertyName("terminal_no")]
    public long TerminalNo { get; set; }

    [JsonPropertyName("opening_float")]
    public decimal OpeningFloat { get; set; }
}

public class SalCloseSessionRequestDto
{
    [JsonPropertyName("session_no")]
    public long SessionNo { get; set; }

    [JsonPropertyName("counted_cash")]
    public decimal CountedCash { get; set; }

    /// <summary>
    /// Set when a supervisor has signed off a drawer that does not reconcile. Without it, a session
    /// out by more than rounding refuses to close.
    /// </summary>
    [JsonPropertyName("variance_approved")]
    public bool VarianceApproved { get; set; }

    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }
}
