using System.Text.Json.Serialization;

namespace AidlyErp.Sal.Application.Dto;

public class Sal1001LineDto
{
    [JsonPropertyName("invoice_dtl_no")]
    public long? InvoiceDtlNo { get; set; }

    [JsonPropertyName("product_no")]
    public long ProductNo { get; set; }

    [JsonPropertyName("product_code")]
    public string? ProductCode { get; set; }

    [JsonPropertyName("product_name")]
    public string? ProductName { get; set; }

    [JsonPropertyName("uom_no")]
    public long? UomNo { get; set; }

    [JsonPropertyName("uom_code")]
    public string? UomCode { get; set; }

    [JsonPropertyName("uom_name")]
    public string? UomName { get; set; }

    [JsonPropertyName("line_no")]
    public int LineNo { get; set; }

    [JsonPropertyName("qty")]
    public decimal Qty { get; set; }

    [JsonPropertyName("qty_base")]
    public decimal QtyBase { get; set; }

    [JsonPropertyName("batch_code")]
    public string? BatchCode { get; set; }

    [JsonPropertyName("unit_price")]
    public decimal UnitPrice { get; set; }

    [JsonPropertyName("unit_cost")]
    public decimal UnitCost { get; set; }

    [JsonPropertyName("line_total")]
    public decimal LineTotal { get; set; }

    [JsonPropertyName("line_discount_pct")]
    public decimal LineDiscountPct { get; set; }

    [JsonPropertyName("line_discount_amount")]
    public decimal LineDiscountAmount { get; set; }

    [JsonPropertyName("taxable_amount")]
    public decimal TaxableAmount { get; set; }

    [JsonPropertyName("tax_rate_pct")]
    public decimal TaxRatePct { get; set; }

    [JsonPropertyName("tax_amount")]
    public decimal TaxAmount { get; set; }

    [JsonPropertyName("net_amount")]
    public decimal NetAmount { get; set; }

    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }
}

public class Sal1001InvoiceDto
{
    [JsonPropertyName("invoice_no")]
    public long? InvoiceNo { get; set; }

    [JsonPropertyName("invoice_id")]
    public string? InvoiceId { get; set; }

    [JsonPropertyName("invoice_date")]
    public DateTime InvoiceDate { get; set; }

    [JsonPropertyName("sale_type")]
    public short SaleType { get; set; } = 2; // 1=POS 2=Credit

    [JsonPropertyName("customer_no")]
    public long CustomerNo { get; set; }

    [JsonPropertyName("customer_name")]
    public string? CustomerName { get; set; }

    [JsonPropertyName("warehouse_no")]
    public long WarehouseNo { get; set; }

    [JsonPropertyName("warehouse_name")]
    public string? WarehouseName { get; set; }

    [JsonPropertyName("branch_no")]
    public long? BranchNo { get; set; }

    [JsonPropertyName("status")]
    public short Status { get; set; } = 1; // 1=Draft 2=Posted 3=Cancelled

    [JsonPropertyName("sub_total")]
    public decimal SubTotal { get; set; }

    [JsonPropertyName("line_discount_total")]
    public decimal LineDiscountTotal { get; set; }

    [JsonPropertyName("taxable_amount")]
    public decimal TaxableAmount { get; set; }

    [JsonPropertyName("tax_amount")]
    public decimal TaxAmount { get; set; }

    [JsonPropertyName("shipping_charge")]
    public decimal ShippingCharge { get; set; }

    [JsonPropertyName("round_off")]
    public decimal RoundOff { get; set; }

    [JsonPropertyName("grand_total")]
    public decimal GrandTotal { get; set; }

    [JsonPropertyName("paid_amount")]
    public decimal PaidAmount { get; set; }

    [JsonPropertyName("due_amount")]
    public decimal DueAmount { get; set; }

    [JsonPropertyName("total_cost")]
    public decimal TotalCost { get; set; }

    [JsonPropertyName("payment_status")]
    public short PaymentStatus { get; set; } = 1; // 1=Unpaid 2=Partial 3=Paid

    [JsonPropertyName("due_date")]
    public DateTime? DueDate { get; set; }

    [JsonPropertyName("fin_year_no")]
    public long? FinYearNo { get; set; }


    /// <summary>The GL voucher the posting engine produced for this invoice, once it has drained
    /// the outbox. Null until then — posting is asynchronous, so an empty value means "not yet",
    /// not "failed".</summary>
    [JsonPropertyName("gl_voucher_no")]
    public string? GlVoucherNo { get; set; }
    [JsonPropertyName("approval_request_no")]
    public long? ApprovalRequestNo { get; set; }

    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }

    [JsonPropertyName("is_active")]
    public short IsActive { get; set; } = 1;

    [JsonPropertyName("row_version")]
    public long? RowVersion { get; set; }

    [JsonPropertyName("lines")]
    public List<Sal1001LineDto> Lines { get; set; } = new();
}

/// <summary>A selectable option. Field names match what the Angular SAL forms bind to
/// (`SalOption { no, name }` in features/sal/forms/sal1001/services/data.service.ts).</summary>
public class SalOptionDto
{
    [JsonPropertyName("no")]
    public long No { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public class Sal1001LookupDto
{
    [JsonPropertyName("customers")]
    public List<SalOptionDto> Customers { get; set; } = new();

    [JsonPropertyName("warehouses")]
    public List<SalOptionDto> Warehouses { get; set; } = new();

    [JsonPropertyName("products")]
    public List<SalOptionDto> Products { get; set; } = new();

    [JsonPropertyName("uoms")]
    public List<SalOptionDto> Uoms { get; set; } = new();

    /// <summary>Legacy alias for <see cref="Products"/> — kept so any caller still
    /// reading `items` keeps working. Serialised, never deserialised into.</summary>
    [JsonPropertyName("items")]
    public List<SalOptionDto> Items => Products;
}

public class Sal1101CustomerDto
{
    [JsonPropertyName("customer_no")]
    public long? CustomerNo { get; set; }

    [JsonPropertyName("customer_code")]
    public string? CustomerCode { get; set; }

    [JsonPropertyName("customer_name")]
    public string? CustomerName { get; set; }

    [JsonPropertyName("company_name")]
    public string? CompanyName { get; set; }

    [JsonPropertyName("customer_group")]
    public string? CustomerGroup { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("phone")]
    public string? Phone { get; set; }

    [JsonPropertyName("mobile")]
    public string? Mobile { get; set; }

    [JsonPropertyName("address")]
    public string? Address { get; set; }

    [JsonPropertyName("city")]
    public string? City { get; set; }

    [JsonPropertyName("country")]
    public string? Country { get; set; }

    [JsonPropertyName("tax_number")]
    public string? TaxNumber { get; set; }

    /// <summary>1 = may buy on credit. A credit limit means nothing without it.</summary>
    [JsonPropertyName("is_credit_allowed")]
    public short IsCreditAllowed { get; set; }

    [JsonPropertyName("credit_limit")]
    public decimal CreditLimit { get; set; }

    [JsonPropertyName("credit_days")]
    public int CreditDays { get; set; }

    [JsonPropertyName("opening_balance")]
    public decimal OpeningBalance { get; set; }

    [JsonPropertyName("current_balance")]
    public decimal CurrentBalance { get; set; }

    [JsonPropertyName("gl_account_no")]
    public long? GlAccountNo { get; set; }

    [JsonPropertyName("branch_no")]
    public long? BranchNo { get; set; }

    [JsonPropertyName("is_active")]
    public short IsActive { get; set; } = 1;

    [JsonPropertyName("row_version")]
    public long? RowVersion { get; set; }
}

public class Sal1102AllocDto
{
    [JsonPropertyName("receipt_alloc_no")]
    public long? ReceiptAllocNo { get; set; }

    [JsonPropertyName("receipt_no")]
    public long? ReceiptNo { get; set; }

    [JsonPropertyName("invoice_no")]
    public long InvoiceNo { get; set; }

    [JsonPropertyName("invoice_id")]
    public string? InvoiceId { get; set; }

    /// <summary>What is still outstanding on that invoice. Allocating a receipt without it means
    /// typing an amount blind.</summary>
    [JsonPropertyName("invoice_due")]
    public decimal InvoiceDue { get; set; }

    [JsonPropertyName("allocated_amount")]
    public decimal AllocatedAmount { get; set; }
}

public class Sal1102OpenInvoiceDto
{
    [JsonPropertyName("invoice_no")]
    public long InvoiceNo { get; set; }

    [JsonPropertyName("invoice_id")]
    public string? InvoiceId { get; set; }

    [JsonPropertyName("invoice_date")]
    public DateTime InvoiceDate { get; set; }

    [JsonPropertyName("grand_total")]
    public decimal GrandTotal { get; set; }

    [JsonPropertyName("paid_amount")]
    public decimal PaidAmount { get; set; }

    [JsonPropertyName("due_amount")]
    public decimal DueAmount { get; set; }
}

public class Sal1102ReceiptDto
{
    [JsonPropertyName("receipt_no")]
    public long? ReceiptNo { get; set; }

    [JsonPropertyName("receipt_id")]
    public string? ReceiptId { get; set; }

    [JsonPropertyName("receipt_date")]
    public DateTime ReceiptDate { get; set; }

    [JsonPropertyName("customer_no")]
    public long CustomerNo { get; set; }

    [JsonPropertyName("customer_name")]
    public string? CustomerName { get; set; }

    [JsonPropertyName("payment_method")]
    public short PaymentMethod { get; set; } = 1; // 1=Cash 2=Bank 3=Cheque 4=Card

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("allocated_amount")]
    public decimal AllocatedAmount { get; set; }

    [JsonPropertyName("unallocated_amount")]
    public decimal UnallocatedAmount { get; set; }

    [JsonPropertyName("gl_account_no")]
    public long? GlAccountNo { get; set; }

    [JsonPropertyName("cheque_no")]
    public string? ChequeNo { get; set; }

    [JsonPropertyName("cheque_date")]
    public DateTime? ChequeDate { get; set; }

    [JsonPropertyName("txn_ref")]
    public string? TxnRef { get; set; }

    [JsonPropertyName("status")]
    public short Status { get; set; } = 1; // 1=Draft 2=Posted 3=Cancelled

    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }

    [JsonPropertyName("is_active")]
    public short IsActive { get; set; } = 1;

    [JsonPropertyName("row_version")]
    public long? RowVersion { get; set; }

    [JsonPropertyName("allocations")]
    public List<Sal1102AllocDto> Allocations { get; set; } = new();
}

public class Sal1103LineDto
{
    [JsonPropertyName("return_dtl_no")]
    public long? ReturnDtlNo { get; set; }

    [JsonPropertyName("product_no")]
    public long ProductNo { get; set; }

    [JsonPropertyName("product_code")]
    public string? ProductCode { get; set; }

    [JsonPropertyName("product_name")]
    public string? ProductName { get; set; }

    [JsonPropertyName("uom_no")]
    public long? UomNo { get; set; }

    [JsonPropertyName("uom_name")]
    public string? UomName { get; set; }

    [JsonPropertyName("line_no")]
    public int LineNo { get; set; }

    [JsonPropertyName("qty")]
    public decimal Qty { get; set; }

    [JsonPropertyName("qty_base")]
    public decimal QtyBase { get; set; }

    [JsonPropertyName("batch_code")]
    public string? BatchCode { get; set; }

    [JsonPropertyName("unit_price")]
    public decimal UnitPrice { get; set; }

    /// <summary>Price actually credited back, which is not the original selling price when the
    /// return is partial-value or a restocking deduction applies.</summary>
    [JsonPropertyName("net_unit_price")]
    public decimal NetUnitPrice { get; set; }

    [JsonPropertyName("tax_rate_pct")]
    public decimal TaxRatePct { get; set; }

    [JsonPropertyName("tax_amount")]
    public decimal TaxAmount { get; set; }

    /// <summary>1 = goods come back into stock, 0 = written off (damaged, expired).
    /// The user's call, not a default — it decides whether stock moves at all.</summary>
    [JsonPropertyName("restock_flag")]
    public short RestockFlag { get; set; } = 1;

    [JsonPropertyName("unit_cost")]
    public decimal UnitCost { get; set; }

    [JsonPropertyName("line_total")]
    public decimal LineTotal { get; set; }

    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }
}

public class Sal1103ReturnDto
{
    [JsonPropertyName("return_no")]
    public long? ReturnNo { get; set; }

    [JsonPropertyName("return_id")]
    public string? ReturnId { get; set; }

    [JsonPropertyName("return_date")]
    public DateTime ReturnDate { get; set; }

    [JsonPropertyName("invoice_no")]
    public long? InvoiceNo { get; set; }

    [JsonPropertyName("invoice_id")]
    public string? InvoiceId { get; set; }

    [JsonPropertyName("customer_no")]
    public long CustomerNo { get; set; }

    [JsonPropertyName("customer_name")]
    public string? CustomerName { get; set; }

    [JsonPropertyName("warehouse_no")]
    public long WarehouseNo { get; set; }

    [JsonPropertyName("warehouse_name")]
    public string? WarehouseName { get; set; }

    [JsonPropertyName("sub_total")]
    public decimal SubTotal { get; set; }

    [JsonPropertyName("total_amount")]
    public decimal TotalAmount { get; set; }

    [JsonPropertyName("tax_amount")]
    public decimal TaxAmount { get; set; }

    [JsonPropertyName("grand_total")]
    public decimal GrandTotal { get; set; }

    [JsonPropertyName("total_cost")]
    public decimal TotalCost { get; set; }

    [JsonPropertyName("status")]
    public short Status { get; set; } = 1; // 1=Draft 2=Posted 3=Cancelled

    /// <summary>1=Defective 2=WrongItem 3=Expired 4=CustomerChange 5=Other. A code, not free text —
    /// return reasons are reported on, so they have to be countable.</summary>
    [JsonPropertyName("return_reason")]
    public short? ReturnReason { get; set; }

    /// <summary>1=Cash 2=Card 3=Mobile 4=Bank 5=StoreCredit 6=AgainstDue. Decides which GL leg the
    /// refund lands on, so it cannot be left to a default.</summary>
    [JsonPropertyName("refund_method")]
    public short RefundMethod { get; set; } = 1;

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }

    [JsonPropertyName("is_active")]
    public short IsActive { get; set; } = 1;

    [JsonPropertyName("row_version")]
    public long? RowVersion { get; set; }

    [JsonPropertyName("lines")]
    public List<Sal1103LineDto> Lines { get; set; } = new();
}
