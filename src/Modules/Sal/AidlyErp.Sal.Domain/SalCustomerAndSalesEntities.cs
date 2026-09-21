using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AidlyErp.Shared.Core;

namespace AidlyErp.Sal.Domain;

[Table("sal_customer")]
public class SalCustomer : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("customer_no")]
    public long CustomerNo { get; set; }

    /// <summary>The business key is <c>customer_id</c> in the schema, not <c>customer_code</c>.</summary>
    [Column("customer_id")]
    [StringLength(30)]
    public string CustomerId { get; set; } = string.Empty;

    [Column("customer_name")]
    [StringLength(200)]
    public string CustomerName { get; set; } = string.Empty;

    [Column("customer_group_no")]
    public long? CustomerGroupNo { get; set; }

    /// <summary>1=Walk-in 2=Regular 3=Corporate. Promotions can target a type.</summary>
    [Column("customer_type")]
    public short CustomerType { get; set; } = 1;

    /// <summary>1=Retail 2=Wholesale 3=Special — the price list this customer buys on.</summary>
    [Column("price_tier")]
    public short PriceTier { get; set; } = 1;

    [Column("email")]
    [StringLength(150)]
    public string? Email { get; set; }

    [Column("mobile_no")]
    [StringLength(20)]
    public string? MobileNo { get; set; }

    [Column("address_line1")]
    [StringLength(250)]
    public string? AddressLine1 { get; set; }

    [Column("address_line2")]
    [StringLength(250)]
    public string? AddressLine2 { get; set; }

    [Column("city")]
    [StringLength(100)]
    public string? City { get; set; }

    [Column("post_code")]
    [StringLength(20)]
    public string? PostCode { get; set; }

    [Column("country_code")]
    [StringLength(10)]
    public string? CountryCode { get; set; }

    [Column("vat_reg_no")]
    [StringLength(50)]
    public string? VatRegNo { get; set; }

    [Column("tin_no")]
    [StringLength(50)]
    public string? TinNo { get; set; }

    [Column("default_currency_no")]
    public long? DefaultCurrencyNo { get; set; }

    [Column("credit_limit")]
    public decimal CreditLimit { get; set; } = 0m;

    [Column("credit_days")]
    public int CreditDays { get; set; } = 0;

    [Column("opening_balance")]
    public decimal OpeningBalance { get; set; } = 0m;

    [Column("current_due")]
    public decimal CurrentDue { get; set; } = 0m;

    // Both are NOT NULL with no database default, so leaving them unmapped made every INSERT into
    // sal_customer fail — the customer form could not create a customer at all, which in turn
    // blocked POS, since a sale requires one. Defaults here are the safe reading of each: a new
    // customer is cash-only until somebody grants credit, and starts with no loyalty balance.
    [Column("is_credit_allowed")]
    public short IsCreditAllowed { get; set; } = 0;

    [Column("loyalty_points")]
    public decimal LoyaltyPoints { get; set; } = 0m;

    [NotMapped]
    public decimal CurrentBalance { get => CurrentDue; set => CurrentDue = value; }

    [NotMapped]
    public string? Mobile { get => MobileNo; set => MobileNo = value; }

    [NotMapped]
    public string? Phone { get => MobileNo; set => MobileNo = value; }

    [Column("company_no")]
    public long CompanyNo { get; set; }

    [Column("branch_no")]
    public long? BranchNo { get; set; }
}

[Table("sal_customer_ledger")]
public class SalCustomerLedger : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("customer_ledger_no")]
    public long CustomerLedgerNo { get; set; }

    [Column("company_no")]
    public long CompanyNo { get; set; }

    [Column("branch_no")]
    public long BranchNo { get; set; }

    [Column("customer_no")]
    public long CustomerNo { get; set; }

    [Column("txn_date")]
    public DateTime TxnDate { get; set; }

    /// <summary>1=Opening 2=Invoice 3=Receipt 4=Return 5=Adjustment.</summary>
    [Column("ref_doc_type")]
    public short RefDocType { get; set; }

    [Column("ref_doc_no")]
    [StringLength(40)]
    public string RefDocNo { get; set; } = string.Empty;

    [Column("ref_doc_pk")]
    public long? RefDocPk { get; set; }

    [Column("debit")]
    public decimal Debit { get; set; } = 0m;

    [Column("credit")]
    public decimal Credit { get; set; } = 0m;

    [Column("balance_after")]
    public decimal BalanceAfter { get; set; } = 0m;

    [Column("fin_year_no")]
    public long? FinYearNo { get; set; }

    [Column("fin_period_no")]
    public long? FinPeriodNo { get; set; }

    [Column("remarks")]
    [StringLength(250)]
    public string? Remarks { get; set; }

    [NotMapped]
    public long LedgerNo { get => CustomerLedgerNo; set => CustomerLedgerNo = value; }

    [NotMapped]
    public DateTime TransDate { get => TxnDate; set => TxnDate = value; }
}

[Table("sal_invoice")]
public class SalInvoice : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("invoice_no")]
    public long InvoiceNo { get; set; }

    [Column("invoice_id")]
    [StringLength(40)]
    public string InvoiceId { get; set; } = string.Empty;

    [Column("invoice_date")]
    public DateTime InvoiceDate { get; set; }

    [Column("sale_type")]
    public short SaleType { get; set; } = 2; // 1=POS 2=Credit 3=Quotation

    [Column("customer_no")]
    public long CustomerNo { get; set; }

    [Column("warehouse_no")]
    public long WarehouseNo { get; set; }

    [Column("currency_no")]
    public long? CurrencyNo { get; set; }

    [Column("exchange_rate")]
    public decimal ExchangeRate { get; set; } = 1.0m;

    [Column("status")]
    public short Status { get; set; } = 1; // 1=Draft 2=Posted 3=Cancelled

    [Column("sub_total")]
    public decimal SubTotal { get; set; } = 0m;

    [Column("line_discount_total")]
    public decimal LineDiscountTotal { get; set; } = 0m;

    [Column("taxable_amount")]
    public decimal TaxableAmount { get; set; } = 0m;

    [Column("tax_amount")]
    public decimal TaxAmount { get; set; } = 0m;

    [Column("shipping_charge")]
    public decimal ShippingCharge { get; set; } = 0m;

    [Column("round_off")]
    public decimal RoundOff { get; set; } = 0m;

    [Column("grand_total")]
    public decimal GrandTotal { get; set; } = 0m;

    [Column("paid_amount")]
    public decimal PaidAmount { get; set; } = 0m;

    [Column("change_amount")]
    public decimal ChangeAmount { get; set; } = 0m;

    [Column("due_amount")]
    public decimal DueAmount { get; set; } = 0m;

    [Column("returned_amount")]
    public decimal ReturnedAmount { get; set; } = 0m;

    [Column("total_cost")]
    public decimal TotalCost { get; set; } = 0m;

    [Column("payment_status")]
    public short PaymentStatus { get; set; } = 1; // 1=Unpaid 2=Partial 3=Paid

    [Column("due_date")]
    public DateTime? DueDate { get; set; }

    [Column("fin_year_no")]
    public long? FinYearNo { get; set; }

    [Column("fin_period_no")]
    public long? FinPeriodNo { get; set; }

    [Column("approval_request_no")]
    public long? ApprovalRequestNo { get; set; }

    [Column("posted_by")]
    public long? PostedBy { get; set; }

    [Column("posted_at")]
    public DateTime? PostedAt { get; set; }

    [Column("cancelled_by")]
    public long? CancelledBy { get; set; }

    [Column("cancelled_at")]
    public DateTime? CancelledAt { get; set; }

    [Column("cancel_reason")]
    [StringLength(250)]
    public string? CancelReason { get; set; }

    [Column("remarks")]
    public string? Remarks { get; set; }

    [Column("company_no")]
    public long CompanyNo { get; set; }

    [Column("branch_no")]
    public long BranchNo { get; set; }

    [Column("gl_voucher_no")]
    [StringLength(30)]
    public string? GlVoucherNo { get; set; }

    // ── POS ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Idempotency key supplied by the till. Uniquely indexed across all rows including
    /// soft-deleted ones, so a double-click or a replayed offline sale collides with the original
    /// instead of ringing it up twice.
    /// </summary>
    [Column("client_uuid")]
    public Guid ClientUuid { get; set; }

    /// <summary>Time of day the sale rang up — the Z-report and hourly reports need more than a date.</summary>
    [Column("invoice_time")]
    public DateTime InvoiceTime { get; set; }

    [Column("terminal_no")]
    public long? TerminalNo { get; set; }

    /// <summary>The drawer period this sale belongs to; null for a back-office credit invoice.</summary>
    [Column("pos_session_no")]
    public long? PosSessionNo { get; set; }

    /// <summary>1=Amount 2=Percent.</summary>
    [Column("bill_discount_type")]
    public short BillDiscountType { get; set; } = 1;

    [Column("bill_discount_value")]
    public decimal BillDiscountValue { get; set; }

    /// <summary>Resolved bill discount, distributed back over lines so tax and margin stay right.</summary>
    [Column("bill_discount_amount")]
    public decimal BillDiscountAmount { get; set; }

    [Column("promotion_discount")]
    public decimal PromotionDiscount { get; set; }

    [Column("salesperson_employee_no")]
    public long? SalespersonEmployeeNo { get; set; }

    [NotMapped]
    public decimal TotalAmount { get => GrandTotal; set => GrandTotal = value; }
}

[Table("sal_invoice_dtl")]
public class SalInvoiceDtl : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("invoice_dtl_no")]
    public long InvoiceDtlNo { get; set; }

    [Column("invoice_no")]
    public long InvoiceNo { get; set; }

    [Column("line_no")]
    public int LineNo { get; set; }

    [Column("product_no")]
    public long ProductNo { get; set; }

    [Column("variant_no")]
    public long? VariantNo { get; set; }

    [Column("batch_no")]
    public long? BatchNo { get; set; }

    [Column("uom_no")]
    public long UomNo { get; set; }

    [Column("qty")]
    public decimal Qty { get; set; } = 0m;

    [Column("qty_base")]
    public decimal QtyBase { get; set; } = 0m;

    [Column("unit_price")]
    public decimal UnitPrice { get; set; } = 0m;

    [Column("line_discount_pct")]
    public decimal LineDiscountPct { get; set; } = 0m;

    [Column("line_discount_amount")]
    public decimal LineDiscountAmount { get; set; } = 0m;

    [Column("vat_tax_no")]
    public long? VatTaxNo { get; set; }

    [Column("tax_rate_pct")]
    public decimal TaxRatePct { get; set; } = 0m;

    [Column("taxable_amount")]
    public decimal TaxableAmount { get; set; } = 0m;

    [Column("tax_amount")]
    public decimal TaxAmount { get; set; } = 0m;

    [Column("line_total")]
    public decimal LineTotal { get; set; } = 0m;

    [Column("unit_cost")]
    public decimal UnitCost { get; set; } = 0m;

    [Column("line_cost")]
    public decimal LineCost { get; set; } = 0m;

    [Column("returned_qty")]
    public decimal ReturnedQty { get; set; } = 0m;

    [Column("remarks")]
    [StringLength(250)]
    public string? Remarks { get; set; }

    // ── POS ──────────────────────────────────────────────────────────────────

    /// <summary>Printed price, so a receipt can show the saving against it.</summary>
    [Column("mrp")]
    public decimal? Mrp { get; set; }

    /// <summary>1=Amount 2=Percent.</summary>
    [Column("line_discount_type")]
    public short LineDiscountType { get; set; } = 1;

    [Column("line_discount_value")]
    public decimal LineDiscountValue { get; set; }

    /// <summary>When 1, the unit price already contains the tax and it is extracted rather than added.</summary>
    [Column("is_tax_inclusive")]
    public short IsTaxInclusive { get; set; }

    [Column("promotion_no")]
    public long? PromotionNo { get; set; }

    [Column("promotion_discount")]
    public decimal PromotionDiscount { get; set; }

    /// <summary>A giveaway line from a Buy-X-Get-Y promotion: zero price, real stock movement.</summary>
    [Column("is_free_item")]
    public short IsFreeItem { get; set; }

    [NotMapped]
    public long ItemNo { get => ProductNo; set => ProductNo = value; }

    [NotMapped]
    public decimal Quantity { get => Qty; set => Qty = value; }
}

[Table("sal_receipt")]
public class SalReceipt : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("receipt_no")]
    public long ReceiptNo { get; set; }

    [Column("receipt_id")]
    [StringLength(40)]
    public string ReceiptId { get; set; } = string.Empty;

    [Column("receipt_date")]
    public DateTime ReceiptDate { get; set; }

    [Column("customer_no")]
    public long CustomerNo { get; set; }

    [Column("payment_method")]
    public short PaymentMethod { get; set; } = 1; // 1=Cash 2=Bank 3=Cheque 4=Card

    [Column("amount")]
    public decimal Amount { get; set; } = 0m;

    [Column("allocated_amount")]
    public decimal AllocatedAmount { get; set; } = 0m;

    [Column("unallocated_amount")]
    public decimal UnallocatedAmount { get; set; } = 0m;

    [Column("gl_account_no")]
    public long? GlAccountNo { get; set; }

    [Column("cheque_no")]
    [StringLength(50)]
    public string? ChequeNo { get; set; }

    [Column("cheque_date")]
    public DateTime? ChequeDate { get; set; }

    [Column("txn_ref")]
    [StringLength(100)]
    public string? TxnRef { get; set; }

    [Column("status")]
    public short Status { get; set; } = 1; // 1=Draft 2=Posted 3=Cancelled

    [Column("fin_year_no")]
    public long FinYearNo { get; set; }

    [Column("fin_period_no")]
    public long? FinPeriodNo { get; set; }

    [Column("posted_at")]
    public DateTime? PostedAt { get; set; }

    [Column("remarks")]
    public string? Remarks { get; set; }

    [Column("company_no")]
    public long CompanyNo { get; set; }

    [Column("branch_no")]
    public long BranchNo { get; set; }

    [Column("gl_voucher_no")]
    [StringLength(30)]
    public string? GlVoucherNo { get; set; }
}

[Table("sal_receipt_alloc")]
public class SalReceiptAlloc : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("receipt_alloc_no")]
    public long ReceiptAllocNo { get; set; }

    [Column("receipt_no")]
    public long ReceiptNo { get; set; }

    [Column("invoice_no")]
    public long? InvoiceNo { get; set; }

    [Column("allocated_amount")]
    public decimal AllocatedAmount { get; set; } = 0m;

    [NotMapped]
    public long AllocNo { get => ReceiptAllocNo; set => ReceiptAllocNo = value; }
}

[Table("sal_return")]
public class SalReturn : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("return_no")]
    public long ReturnNo { get; set; }

    [Column("return_id")]
    [StringLength(40)]
    public string ReturnId { get; set; } = string.Empty;

    [Column("return_date")]
    public DateTime ReturnDate { get; set; }

    [Column("original_invoice_no")]
    public long? OriginalInvoiceNo { get; set; }

    [Column("customer_no")]
    public long CustomerNo { get; set; }

    [Column("warehouse_no")]
    public long WarehouseNo { get; set; }

    /// <summary>1=Defective 2=WrongItem 3=Expired 4=CustomerChange 5=Other.</summary>
    [Column("return_reason")]
    public short? ReturnReason { get; set; }

    [Column("status")]
    public short Status { get; set; } = 1; // 1=Draft 2=Posted 3=Cancelled

    /// <summary>1=Cash 2=Card 3=Mobile 4=Bank 5=StoreCredit 6=AgainstDue.</summary>
    [Column("refund_method")]
    public short RefundMethod { get; set; } = 1;

    [Column("sub_total")]
    public decimal SubTotal { get; set; } = 0m;

    [Column("tax_amount")]
    public decimal TaxAmount { get; set; } = 0m;

    [Column("grand_total")]
    public decimal GrandTotal { get; set; } = 0m;

    [Column("refund_amount")]
    public decimal RefundAmount { get; set; } = 0m;

    [Column("total_cost")]
    public decimal TotalCost { get; set; } = 0m;

    [Column("fin_year_no")]
    public long FinYearNo { get; set; }

    [Column("fin_period_no")]
    public long? FinPeriodNo { get; set; }

    [Column("posted_at")]
    public DateTime? PostedAt { get; set; }

    [Column("cancelled_at")]
    public DateTime? CancelledAt { get; set; }

    [Column("remarks")]
    public string? Remarks { get; set; }

    [Column("company_no")]
    public long CompanyNo { get; set; }

    [Column("branch_no")]
    public long BranchNo { get; set; }

    [Column("gl_voucher_no")]
    [StringLength(30)]
    public string? GlVoucherNo { get; set; }

    [NotMapped]
    public long? InvoiceNo { get => OriginalInvoiceNo; set => OriginalInvoiceNo = value; }

    [NotMapped]
    public decimal TotalAmount { get => SubTotal; set => SubTotal = value; }
}

[Table("sal_return_dtl")]
public class SalReturnDtl : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("return_dtl_no")]
    public long ReturnDtlNo { get; set; }

    [Column("return_no")]
    public long ReturnNo { get; set; }

    [Column("line_no")]
    public int LineNo { get; set; }

    [Column("product_no")]
    public long ProductNo { get; set; }

    [Column("variant_no")]
    public long? VariantNo { get; set; }

    [Column("batch_no")]
    public long? BatchNo { get; set; }

    [Column("uom_no")]
    public long UomNo { get; set; }

    [Column("qty")]
    public decimal Qty { get; set; } = 0m;

    [Column("qty_base")]
    public decimal QtyBase { get; set; } = 0m;

    [Column("unit_price")]
    public decimal UnitPrice { get; set; } = 0m;

    [Column("net_unit_price")]
    public decimal NetUnitPrice { get; set; } = 0m;

    [Column("tax_rate_pct")]
    public decimal TaxRatePct { get; set; } = 0m;

    [Column("tax_amount")]
    public decimal TaxAmount { get; set; } = 0m;

    [Column("line_total")]
    public decimal LineTotal { get; set; } = 0m;

    [Column("unit_cost")]
    public decimal UnitCost { get; set; } = 0m;

    [Column("line_cost")]
    public decimal LineCost { get; set; } = 0m;

    [Column("restock_flag")]
    public short RestockFlag { get; set; } = 1;

    [Column("remarks")]
    [StringLength(250)]
    public string? Remarks { get; set; }

    [NotMapped]
    public long ItemNo { get => ProductNo; set => ProductNo = value; }

    [NotMapped]
    public decimal Quantity { get => Qty; set => Qty = value; }
}
