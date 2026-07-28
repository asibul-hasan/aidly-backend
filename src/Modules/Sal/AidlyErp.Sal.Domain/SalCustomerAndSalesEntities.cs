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

    [Column("company_name")]
    [StringLength(200)]
    public string? CompanyName { get; set; }

    [Column("customer_group")]
    [StringLength(50)]
    public string? CustomerGroup { get; set; }

    [Column("email")]
    [StringLength(250)]
    public string? Email { get; set; }

    [Column("phone")]
    [StringLength(30)]
    public string? Phone { get; set; }

    [Column("mobile")]
    [StringLength(30)]
    public string? Mobile { get; set; }

    [Column("address")]
    [StringLength(500)]
    public string? Address { get; set; }

    [Column("city")]
    [StringLength(100)]
    public string? City { get; set; }

    [Column("country")]
    [StringLength(100)]
    public string? Country { get; set; }

    [Column("tax_number")]
    [StringLength(50)]
    public string? TaxNumber { get; set; }

    [Column("currency_no")]
    public long? CurrencyNo { get; set; }

    [Column("credit_limit")]
    public decimal CreditLimit { get; set; } = 0m;

    [Column("credit_days")]
    public int CreditDays { get; set; } = 0;

    [Column("opening_balance")]
    public decimal OpeningBalance { get; set; } = 0m;

    [Column("current_balance")]
    public decimal CurrentBalance { get; set; } = 0m;

    [Column("gl_account_no")]
    public long? GlAccountNo { get; set; }

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

    [Column("customer_no")]
    public long CustomerNo { get; set; }

    [Column("trans_date")]
    public DateTime TransDate { get; set; }

    [Column("trans_type")]
    [StringLength(40)]
    public string TransType { get; set; } = string.Empty; // INVOICE, RECEIPT, RETURN, REFUND

    [Column("doc_no")]
    public long DocNo { get; set; }

    [Column("doc_id")]
    [StringLength(40)]
    public string? DocId { get; set; }

    [Column("narration")]
    [StringLength(500)]
    public string? Narration { get; set; }

    [Column("debit_amount")]
    public decimal DebitAmount { get; set; } = 0m;

    [Column("credit_amount")]
    public decimal CreditAmount { get; set; } = 0m;

    [Column("balance_amount")]
    public decimal BalanceAmount { get; set; } = 0m;

    [Column("company_no")]
    public long CompanyNo { get; set; }

    [Column("branch_no")]
    public long BranchNo { get; set; }

    [NotMapped]
    public long LedgerNo { get => CustomerLedgerNo; set => CustomerLedgerNo = value; }
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

    [Column("item_no")]
    public long ItemNo { get; set; }

    [Column("uom_no")]
    public long? UomNo { get; set; }

    [Column("quantity")]
    public decimal Quantity { get; set; } = 0m;

    [Column("unit_price")]
    public decimal UnitPrice { get; set; } = 0m;

    [Column("unit_cost")]
    public decimal UnitCost { get; set; } = 0m;

    [Column("line_total")]
    public decimal LineTotal { get; set; } = 0m;

    [Column("discount_percent")]
    public decimal DiscountPercent { get; set; } = 0m;

    [Column("discount_amount")]
    public decimal DiscountAmount { get; set; } = 0m;

    [Column("taxable_amount")]
    public decimal TaxableAmount { get; set; } = 0m;

    [Column("tax_percent")]
    public decimal TaxPercent { get; set; } = 0m;

    [Column("tax_amount")]
    public decimal TaxAmount { get; set; } = 0m;

    [Column("net_amount")]
    public decimal NetAmount { get; set; } = 0m;

    [Column("total_cost")]
    public decimal TotalCost { get; set; } = 0m;

    [Column("remarks")]
    [StringLength(250)]
    public string? Remarks { get; set; }

    [NotMapped]
    public long ProductNo { get => ItemNo; set => ItemNo = value; }

    [NotMapped]
    public decimal Qty { get => Quantity; set => Quantity = value; }

    [NotMapped]
    public decimal TotalPrice { get => NetAmount; set => NetAmount = value; }
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
    public long? FinYearNo { get; set; }

    [Column("posted_by")]
    public long? PostedBy { get; set; }

    [Column("posted_at")]
    public DateTime? PostedAt { get; set; }

    [Column("remarks")]
    public string? Remarks { get; set; }

    [Column("company_no")]
    public long CompanyNo { get; set; }

    [Column("branch_no")]
    public long BranchNo { get; set; }
}

[Table("sal_receipt_alloc")]
public class SalReceiptAlloc : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("alloc_no")]
    public long AllocNo { get; set; }

    [Column("receipt_no")]
    public long ReceiptNo { get; set; }

    [Column("invoice_no")]
    public long InvoiceNo { get; set; }

    [Column("allocated_amount")]
    public decimal AllocatedAmount { get; set; } = 0m;
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

    [Column("invoice_no")]
    public long? InvoiceNo { get; set; }

    [Column("customer_no")]
    public long CustomerNo { get; set; }

    [Column("warehouse_no")]
    public long WarehouseNo { get; set; }

    [Column("total_amount")]
    public decimal TotalAmount { get; set; } = 0m;

    [Column("tax_amount")]
    public decimal TaxAmount { get; set; } = 0m;

    [Column("grand_total")]
    public decimal GrandTotal { get; set; } = 0m;

    [Column("status")]
    public short Status { get; set; } = 1; // 1=Draft 2=Posted 3=Cancelled

    [Column("reason")]
    [StringLength(250)]
    public string? Reason { get; set; }

    [Column("remarks")]
    public string? Remarks { get; set; }

    [Column("company_no")]
    public long CompanyNo { get; set; }

    [Column("branch_no")]
    public long BranchNo { get; set; }
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

    [Column("item_no")]
    public long ItemNo { get; set; }

    [Column("uom_no")]
    public long? UomNo { get; set; }

    [Column("quantity")]
    public decimal Quantity { get; set; } = 0m;

    [Column("unit_price")]
    public decimal UnitPrice { get; set; } = 0m;

    [Column("unit_cost")]
    public decimal UnitCost { get; set; } = 0m;

    [Column("line_total")]
    public decimal LineTotal { get; set; } = 0m;

    [Column("remarks")]
    [StringLength(250)]
    public string? Remarks { get; set; }

    [NotMapped]
    public long ProductNo { get => ItemNo; set => ItemNo = value; }

    [NotMapped]
    public decimal Qty { get => Quantity; set => Quantity = value; }
}
