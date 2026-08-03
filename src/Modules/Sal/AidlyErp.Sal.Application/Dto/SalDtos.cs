using System.Text.Json.Serialization;

namespace AidlyErp.Sal.Application.Dto;

public class Sal1001LineDto
{
    [JsonPropertyName("invoice_dtl_no")]
    public long? InvoiceDtlNo { get; set; }

    [JsonPropertyName("item_no")]
    public long ItemNo { get; set; }

    [JsonPropertyName("item_code")]
    public string? ItemCode { get; set; }

    [JsonPropertyName("item_name")]
    public string? ItemName { get; set; }

    [JsonPropertyName("uom_no")]
    public long? UomNo { get; set; }

    [JsonPropertyName("uom_code")]
    public string? UomCode { get; set; }

    [JsonPropertyName("quantity")]
    public decimal Quantity { get; set; }

    [JsonPropertyName("unit_price")]
    public decimal UnitPrice { get; set; }

    [JsonPropertyName("unit_cost")]
    public decimal UnitCost { get; set; }

    [JsonPropertyName("line_total")]
    public decimal LineTotal { get; set; }

    [JsonPropertyName("discount_percent")]
    public decimal DiscountPercent { get; set; }

    [JsonPropertyName("discount_amount")]
    public decimal DiscountAmount { get; set; }

    [JsonPropertyName("taxable_amount")]
    public decimal TaxableAmount { get; set; }

    [JsonPropertyName("tax_percent")]
    public decimal TaxPercent { get; set; }

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

public class Sal1001LookupDto
{
    [JsonPropertyName("customers")]
    public List<object> Customers { get; set; } = new();

    [JsonPropertyName("warehouses")]
    public List<object> Warehouses { get; set; } = new();

    [JsonPropertyName("items")]
    public List<object> Items { get; set; } = new();
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
    [JsonPropertyName("alloc_no")]
    public long? AllocNo { get; set; }

    [JsonPropertyName("receipt_no")]
    public long? ReceiptNo { get; set; }

    [JsonPropertyName("invoice_no")]
    public long InvoiceNo { get; set; }

    [JsonPropertyName("invoice_id")]
    public string? InvoiceId { get; set; }

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

    [JsonPropertyName("item_no")]
    public long ItemNo { get; set; }

    [JsonPropertyName("item_code")]
    public string? ItemCode { get; set; }

    [JsonPropertyName("item_name")]
    public string? ItemName { get; set; }

    [JsonPropertyName("uom_no")]
    public long? UomNo { get; set; }

    [JsonPropertyName("quantity")]
    public decimal Quantity { get; set; }

    [JsonPropertyName("unit_price")]
    public decimal UnitPrice { get; set; }

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

    [JsonPropertyName("total_amount")]
    public decimal TotalAmount { get; set; }

    [JsonPropertyName("tax_amount")]
    public decimal TaxAmount { get; set; }

    [JsonPropertyName("grand_total")]
    public decimal GrandTotal { get; set; }

    [JsonPropertyName("status")]
    public short Status { get; set; } = 1; // 1=Draft 2=Posted 3=Cancelled

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
