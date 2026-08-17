using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AidlyErp.Shared.Core;

namespace AidlyErp.Pur.Domain;

[Table("pur_receipt")]
public class PurReceipt : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("receipt_no")]
    public long ReceiptNo { get; set; }

    [Column("receipt_id")]
    [StringLength(40)]
    public string ReceiptId { get; set; } = string.Empty;

    [Column("supplier_no")]
    public long SupplierNo { get; set; }

    [Column("warehouse_no")]
    public long WarehouseNo { get; set; }

    [Column("order_no")]
    public long? OrderNo { get; set; }

    [Column("receipt_date")]
    public DateTime ReceiptDate { get; set; }

    [Column("sub_total")]
    public decimal SubTotal { get; set; } = 0m;

    [Column("tax_amount")]
    public decimal TaxAmount { get; set; } = 0m;

    [Column("grand_total")]
    public decimal GrandTotal { get; set; } = 0m;

    [Column("fin_year_no")]
    public long? FinYearNo { get; set; }

    [Column("fin_period_no")]
    public long? FinPeriodNo { get; set; }

    [Column("status")]
    public short Status { get; set; } = 1;

    [Column("posted_by")]
    public long? PostedBy { get; set; }

    [Column("posted_at")]
    public DateTime? PostedAt { get; set; }

    [Column("cancelled_by")]
    public long? CancelledBy { get; set; }

    [Column("cancelled_at")]
    public DateTime? CancelledAt { get; set; }

    [Column("cancel_reason")]
    [StringLength(500)]
    public string? CancelReason { get; set; }

    [Column("remarks")]
    [StringLength(500)]
    public string? Remarks { get; set; }

    [Column("branch_no")]
    public long BranchNo { get; set; }

    [Column("company_no")]
    public long CompanyNo { get; set; }
}

[Table("pur_receipt_dtl")]
public class PurReceiptDtl : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("receipt_dtl_no")]
    public long ReceiptDtlNo { get; set; }

    [Column("receipt_no")]
    public long ReceiptNo { get; set; }

    [Column("line_no")]
    public int LineNo { get; set; }

    [Column("order_dtl_no")]
    public long? OrderDtlNo { get; set; }

    [Column("product_no")]
    public long ProductNo { get; set; }

    [Column("variant_no")]
    public long? VariantNo { get; set; }

    [Column("uom_no")]
    public long UomNo { get; set; }

    [Column("qty")]
    public decimal Qty { get; set; } = 0m;

    [Column("qty_base")]
    public decimal QtyBase { get; set; } = 0m;

    [Column("unit_cost")]
    public decimal UnitCost { get; set; } = 0m;

    [Column("tax_rate_pct")]
    public decimal TaxRatePct { get; set; } = 0m;

    [Column("tax_amount")]
    public decimal TaxAmount { get; set; } = 0m;

    [Column("line_total")]
    public decimal LineTotal { get; set; } = 0m;

    [Column("batch_no")]
    public long? BatchNo { get; set; }

    [Column("batch_code")]
    [StringLength(60)]
    public string? BatchCode { get; set; }

    [Column("mfg_date")]
    public DateTime? MfgDate { get; set; }

    [Column("expiry_date")]
    public DateTime? ExpiryDate { get; set; }

    [Column("remarks")]
    [StringLength(250)]
    public string? Remarks { get; set; }

    [NotMapped]
    public long ItemNo { get => ProductNo; set => ProductNo = value; }

    [NotMapped]
    public decimal Quantity { get => Qty; set => Qty = value; }

    [NotMapped]
    public decimal QtyReceived { get => Qty; set => Qty = value; }
}

[Table("pur_invoice")]
public class PurInvoice : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("invoice_no")]
    public long InvoiceNo { get; set; }

    [Column("invoice_id")]
    [StringLength(40)]
    public string InvoiceId { get; set; } = string.Empty;

    [Column("supplier_invoice_no")]
    [StringLength(60)]
    public string? SupplierInvoiceNo { get; set; }

    [Column("supplier_no")]
    public long SupplierNo { get; set; }

    [Column("warehouse_no")]
    public long WarehouseNo { get; set; }

    [Column("order_no")]
    public long? OrderNo { get; set; }

    [Column("invoice_date")]
    public DateTime InvoiceDate { get; set; }

    [Column("receive_mode")]
    public short ReceiveMode { get; set; } = 1;

    [Column("currency_no")]
    public long? CurrencyNo { get; set; }

    [Column("exchange_rate")]
    public decimal ExchangeRate { get; set; } = 1m;

    [Column("sub_total")]
    public decimal SubTotal { get; set; } = 0m;

    [Column("discount_total")]
    public decimal DiscountTotal { get; set; } = 0m;

    [Column("taxable_amount")]
    public decimal TaxableAmount { get; set; } = 0m;

    [Column("tax_amount")]
    public decimal TaxAmount { get; set; } = 0m;

    [Column("shipping_charge")]
    public decimal ShippingCharge { get; set; } = 0m;

    [Column("other_charges")]
    public decimal OtherCharges { get; set; } = 0m;

    [Column("round_off")]
    public decimal RoundOff { get; set; } = 0m;

    [Column("grand_total")]
    public decimal GrandTotal { get; set; } = 0m;

    [Column("paid_amount")]
    public decimal PaidAmount { get; set; } = 0m;

    [Column("due_amount")]
    public decimal DueAmount { get; set; } = 0m;

    [Column("payment_status")]
    public short PaymentStatus { get; set; } = 1;

    [Column("status")]
    public short Status { get; set; } = 1;

    [Column("due_date")]
    public DateTime? DueDate { get; set; }

    [Column("fin_year_no")]
    public long? FinYearNo { get; set; }

    [Column("fin_period_no")]
    public long? FinPeriodNo { get; set; }

    [Column("landed_cost_total")]
    public decimal LandedCostTotal { get; set; } = 0m;

    [Column("returned_amount")]
    public decimal ReturnedAmount { get; set; } = 0m;

    [Column("approval_request_no")]
    public long? ApprovalRequestNo { get; set; }

    [Column("remarks")]
    public string? Remarks { get; set; }

    [Column("posted_by")]
    public long? PostedBy { get; set; }

    [Column("posted_at")]
    public DateTime? PostedAt { get; set; }

    [Column("cancelled_by")]
    public long? CancelledBy { get; set; }

    [Column("cancelled_at")]
    public DateTime? CancelledAt { get; set; }

    [Column("cancel_reason")]
    [StringLength(500)]
    public string? CancelReason { get; set; }

    [Column("branch_no")]
    public long BranchNo { get; set; }

    [Column("company_no")]
    public long CompanyNo { get; set; }

    [Column("gl_voucher_no")]
    [StringLength(30)]
    public string? GlVoucherNo { get; set; }

    [NotMapped]
    public decimal TotalAmount { get => GrandTotal; set => GrandTotal = value; }
}

[Table("pur_invoice_dtl")]
public class PurInvoiceDtl : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("invoice_dtl_no")]
    public long InvoiceDtlNo { get; set; }

    [Column("invoice_no")]
    public long InvoiceNo { get; set; }

    /// <summary>
    /// The goods-receipt line this invoice line bills for, when the goods arrived on a GRN.
    ///
    /// This is what makes the three-way match work. A GRN already took the goods into stock and
    /// credited GRN clearing, so an invoice line that points at one must NOT move stock again —
    /// it clears the accrual instead (Dr GRN clearing / Cr AP). Null means the one-step path:
    /// the invoice is itself the receipt, so it posts stock (Dr Inventory / Cr AP).
    /// </summary>
    [Column("receipt_dtl_no")]
    public long? ReceiptDtlNo { get; set; }

    [Column("line_no")]
    public int LineNo { get; set; }

    [Column("product_no")]
    public long ProductNo { get; set; }

    [Column("variant_no")]
    public long? VariantNo { get; set; }

    [Column("uom_no")]
    public long UomNo { get; set; }

    [Column("qty")]
    public decimal Qty { get; set; } = 0m;

    [Column("qty_base")]
    public decimal QtyBase { get; set; } = 0m;

    [Column("unit_price")]
    public decimal UnitPrice { get; set; } = 0m;

    [Column("discount_pct")]
    public decimal DiscountPct { get; set; } = 0m;

    [Column("discount_amount")]
    public decimal DiscountAmount { get; set; } = 0m;

    [Column("vat_tax_no")]
    public long? VatTaxNo { get; set; }

    [Column("tax_rate_pct")]
    public decimal TaxRatePct { get; set; } = 0m;

    [Column("is_tax_inclusive")]
    public short IsTaxInclusive { get; set; } = 0;

    [Column("taxable_amount")]
    public decimal TaxableAmount { get; set; } = 0m;

    [Column("tax_amount")]
    public decimal TaxAmount { get; set; } = 0m;

    [Column("line_total")]
    public decimal LineTotal { get; set; } = 0m;

    [Column("landed_cost_alloc")]
    public decimal LandedCostAlloc { get; set; } = 0m;

    [Column("final_unit_cost")]
    public decimal FinalUnitCost { get; set; } = 0m;

    [Column("batch_code")]
    [StringLength(60)]
    public string? BatchCode { get; set; }

    [Column("mfg_date")]
    public DateTime? MfgDate { get; set; }

    [Column("expiry_date")]
    public DateTime? ExpiryDate { get; set; }

    [Column("batch_no")]
    public long? BatchNo { get; set; }

    [Column("returned_qty_base")]
    public decimal ReturnedQtyBase { get; set; } = 0m;

    [Column("remarks")]
    [StringLength(250)]
    public string? Remarks { get; set; }

    [NotMapped]
    public long ItemNo { get => ProductNo; set => ProductNo = value; }

    [NotMapped]
    public decimal Quantity { get => Qty; set => Qty = value; }
}

[Table("pur_return")]
public class PurReturn : AuditEntity
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

    [Column("supplier_no")]
    public long SupplierNo { get; set; }

    [Column("original_invoice_no")]
    public long? OriginalInvoiceNo { get; set; }

    [Column("warehouse_no")]
    public long WarehouseNo { get; set; }

    /// <summary>1=Defective 2=Expired 3=Excess 4=WrongItem 5=PriceDispute 6=Other.</summary>
    [Column("return_reason")]
    public short? ReturnReason { get; set; }

    [Column("status")]
    public short Status { get; set; } = 1;

    /// <summary>1=AdjustPayable(DebitNote) 2=CashRefund 3=Replacement.</summary>
    [Column("settlement_mode")]
    public short SettlementMode { get; set; } = 1;

    [Column("sub_total")]
    public decimal SubTotal { get; set; } = 0m;

    [Column("tax_amount")]
    public decimal TaxAmount { get; set; } = 0m;

    [Column("grand_total")]
    public decimal GrandTotal { get; set; } = 0m;

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

    [NotMapped]
    public long? InvoiceNo { get => OriginalInvoiceNo; set => OriginalInvoiceNo = value; }
}

[Table("pur_return_dtl")]
public class PurReturnDtl : AuditEntity
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

    [Column("unit_cost")]
    public decimal UnitCost { get; set; } = 0m;

    [Column("tax_rate_pct")]
    public decimal TaxRatePct { get; set; } = 0m;

    [Column("tax_amount")]
    public decimal TaxAmount { get; set; } = 0m;

    [Column("line_total")]
    public decimal LineTotal { get; set; } = 0m;

    [Column("remarks")]
    [StringLength(250)]
    public string? Remarks { get; set; }

    [NotMapped]
    public long ItemNo { get => ProductNo; set => ProductNo = value; }

    [NotMapped]
    public decimal Quantity { get => Qty; set => Qty = value; }
}

[Table("pur_payment")]
public class PurPayment : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("payment_no")]
    public long PaymentNo { get; set; }

    [Column("payment_id")]
    [StringLength(40)]
    public string PaymentId { get; set; } = string.Empty;

    [Column("supplier_no")]
    public long SupplierNo { get; set; }

    [Column("payment_date")]
    public DateTime PaymentDate { get; set; }

    [Column("payment_method")]
    public short PaymentMethod { get; set; } = 1;

    [Column("amount")]
    public decimal Amount { get; set; } = 0m;

    // pur_payment.exchange_rate is NOT NULL and was not mapped at all, so every payment
    // insert failed. Single-currency for now, hence the 1m default — the column exists so a
    // foreign-currency payment can carry its rate later.
    [Column("exchange_rate")]
    public decimal ExchangeRate { get; set; } = 1m;

    [Column("allocated_amount")]
    public decimal AllocatedAmount { get; set; } = 0m;

    [Column("unallocated_amount")]
    public decimal UnallocatedAmount { get; set; } = 0m;

    [Column("gl_account_no")]
    public long? GlAccountNo { get; set; }

    [Column("cheque_no")]
    [StringLength(40)]
    public string? ChequeNo { get; set; }

    [Column("cheque_date")]
    public DateTime? ChequeDate { get; set; }

    [Column("txn_ref")]
    [StringLength(80)]
    public string? TxnRef { get; set; }

    [Column("fin_year_no")]
    public long? FinYearNo { get; set; }

    [Column("fin_period_no")]
    public long? FinPeriodNo { get; set; }

    [Column("status")]
    public short Status { get; set; } = 1;

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
}

[Table("pur_payment_alloc")]
public class PurPaymentAlloc : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("payment_alloc_no")]
    public long PaymentAllocNo { get; set; }

    [Column("payment_no")]
    public long PaymentNo { get; set; }

    [Column("invoice_no")]
    public long? InvoiceNo { get; set; }

    [Column("allocated_amount")]
    public decimal AllocatedAmount { get; set; } = 0m;

    [NotMapped]
    public long AllocNo { get => PaymentAllocNo; set => PaymentAllocNo = value; }
}

[Table("pur_landed_cost")]
public class PurLandedCost : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("landed_cost_no")]
    public long LandedCostNo { get; set; }

    [Column("landed_cost_id")]
    [StringLength(40)]
    public string? LandedCostId { get; set; }

    [Column("cost_date")]
    public DateTime CostDate { get; set; }

    [Column("invoice_no")]
    public long InvoiceNo { get; set; }

    [Column("receipt_no")]
    public long? ReceiptNo { get; set; }

    [Column("cost_type")]
    [StringLength(50)]
    public string? CostType { get; set; } = "FREIGHT";

    [Column("vendor_no")]
    public long? VendorNo { get; set; }

    [Column("amount")]
    public decimal Amount { get; set; } = 0m;

    [Column("alloc_basis")]
    public short AllocBasis { get; set; } = 1;

    [Column("status")]
    public short Status { get; set; } = 1;

    [Column("applied_by")]
    public long? AppliedBy { get; set; }

    [Column("applied_at")]
    public DateTime? AppliedAt { get; set; }

    [Column("remarks")]
    [StringLength(500)]
    public string? Remarks { get; set; }

    [Column("company_no")]
    public long CompanyNo { get; set; }

    [Column("branch_no")]
    public long BranchNo { get; set; }

    [NotMapped]
    public long CostNo { get => LandedCostNo; set => LandedCostNo = value; }

    [NotMapped]
    public decimal TotalCost { get => Amount; set => Amount = value; }
}

[Table("pur_landed_cost_alloc")]
public class PurLandedCostAlloc : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("landed_cost_alloc_no")]
    public long LandedCostAllocNo { get; set; }

    [Column("landed_cost_no")]
    public long LandedCostNo { get; set; }

    [Column("invoice_dtl_no")]
    public long InvoiceDtlNo { get; set; }

    [Column("allocated_amount")]
    public decimal AllocatedAmount { get; set; } = 0m;

    [NotMapped]
    public long AllocNo { get => LandedCostAllocNo; set => LandedCostAllocNo = value; }

    [NotMapped]
    public long CostNo { get => LandedCostNo; set => LandedCostNo = value; }
}
