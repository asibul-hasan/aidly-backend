using System.Text.Json.Serialization;

namespace AidlyErp.Pur.Application.Dto;

public class Pur1001SupplierDto
{
    [JsonPropertyName("supplier_no")]
    public long? SupplierNo { get; set; }

    [JsonPropertyName("supplier_code")]
    public string? SupplierCode { get; set; }

    [JsonPropertyName("supplier_name")]
    public string? SupplierName { get; set; }

    [JsonPropertyName("company_name")]
    public string? CompanyName { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("phone")]
    public string? Phone { get; set; }

    [JsonPropertyName("mobile")]
    public string? Mobile { get; set; }

    [JsonPropertyName("address")]
    public string? Address { get; set; }

    [JsonPropertyName("tax_number")]
    public string? TaxNumber { get; set; }

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

public class Pur1002PriceRowDto
{
    [JsonPropertyName("supplier_no")]
    public long SupplierNo { get; set; }

    [JsonPropertyName("product_no")]
    public long ProductNo { get; set; }

    [JsonPropertyName("unit_price")]
    public decimal UnitPrice { get; set; }
}

public class Pur1101LineDto
{
    [JsonPropertyName("order_dtl_no")]
    public long? OrderDtlNo { get; set; }

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

    [JsonPropertyName("line_total")]
    public decimal LineTotal { get; set; }

    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }
}

public class Pur1101OrderDto
{
    [JsonPropertyName("order_no")]
    public long? OrderNo { get; set; }

    [JsonPropertyName("order_id")]
    public string? OrderId { get; set; }

    [JsonPropertyName("order_date")]
    public DateTime OrderDate { get; set; }

    [JsonPropertyName("supplier_no")]
    public long SupplierNo { get; set; }

    [JsonPropertyName("supplier_name")]
    public string? SupplierName { get; set; }

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
    public short Status { get; set; } = 1; // 1=Draft 2=Submitted 3=Approved 4=Cancelled

    [JsonPropertyName("approval_request_no")]
    public long? ApprovalRequestNo { get; set; }

    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }

    [JsonPropertyName("is_active")]
    public short IsActive { get; set; } = 1;

    [JsonPropertyName("row_version")]
    public long? RowVersion { get; set; }

    [JsonPropertyName("lines")]
    public List<Pur1101LineDto> Lines { get; set; } = new();
}

public class Pur1102LineDto
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

    [JsonPropertyName("quantity")]
    public decimal Quantity { get; set; }

    [JsonPropertyName("unit_price")]
    public decimal UnitPrice { get; set; }

    [JsonPropertyName("line_total")]
    public decimal LineTotal { get; set; }

    [JsonPropertyName("discount_amount")]
    public decimal DiscountAmount { get; set; }

    [JsonPropertyName("tax_amount")]
    public decimal TaxAmount { get; set; }

    [JsonPropertyName("net_amount")]
    public decimal NetAmount { get; set; }

    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }
}

public class Pur1102InvoiceDto
{
    [JsonPropertyName("invoice_no")]
    public long? InvoiceNo { get; set; }

    [JsonPropertyName("invoice_id")]
    public string? InvoiceId { get; set; }

    [JsonPropertyName("invoice_date")]
    public DateTime InvoiceDate { get; set; }

    [JsonPropertyName("supplier_no")]
    public long SupplierNo { get; set; }

    [JsonPropertyName("supplier_name")]
    public string? SupplierName { get; set; }

    [JsonPropertyName("warehouse_no")]
    public long WarehouseNo { get; set; }

    [JsonPropertyName("warehouse_name")]
    public string? WarehouseName { get; set; }

    [JsonPropertyName("order_no")]
    public long? OrderNo { get; set; }

    [JsonPropertyName("sub_total")]
    public decimal SubTotal { get; set; }

    [JsonPropertyName("tax_amount")]
    public decimal TaxAmount { get; set; }

    [JsonPropertyName("grand_total")]
    public decimal GrandTotal { get; set; }

    [JsonPropertyName("paid_amount")]
    public decimal PaidAmount { get; set; }

    [JsonPropertyName("due_amount")]
    public decimal DueAmount { get; set; }

    [JsonPropertyName("payment_status")]
    public short PaymentStatus { get; set; } = 1;

    [JsonPropertyName("status")]
    public short Status { get; set; } = 1; // 1=Draft 2=Posted 3=Cancelled

    [JsonPropertyName("due_date")]
    public DateTime? DueDate { get; set; }

    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }

    [JsonPropertyName("branch_no")]
    public long? BranchNo { get; set; }

    [JsonPropertyName("is_active")]
    public short IsActive { get; set; } = 1;

    [JsonPropertyName("row_version")]
    public long? RowVersion { get; set; }

    [JsonPropertyName("lines")]
    public List<Pur1102LineDto> Lines { get; set; } = new();
}

public class Pur1102LookupDto
{
    [JsonPropertyName("suppliers")]
    public List<object> Suppliers { get; set; } = new();

    [JsonPropertyName("warehouses")]
    public List<object> Warehouses { get; set; } = new();

    [JsonPropertyName("products")]
    public List<object> Products { get; set; } = new();
}

public class Pur1103LineDto
{
    [JsonPropertyName("return_dtl_no")]
    public long? ReturnDtlNo { get; set; }

    [JsonPropertyName("item_no")]
    public long ItemNo { get; set; }

    [JsonPropertyName("uom_no")]
    public long? UomNo { get; set; }

    [JsonPropertyName("quantity")]
    public decimal Quantity { get; set; }

    [JsonPropertyName("unit_price")]
    public decimal UnitPrice { get; set; }

    [JsonPropertyName("line_total")]
    public decimal LineTotal { get; set; }
}

public class Pur1103ReturnDto
{
    [JsonPropertyName("return_no")]
    public long? ReturnNo { get; set; }

    [JsonPropertyName("return_id")]
    public string? ReturnId { get; set; }

    [JsonPropertyName("return_date")]
    public DateTime ReturnDate { get; set; }

    [JsonPropertyName("supplier_no")]
    public long SupplierNo { get; set; }

    [JsonPropertyName("supplier_name")]
    public string? SupplierName { get; set; }

    [JsonPropertyName("warehouse_no")]
    public long WarehouseNo { get; set; }

    [JsonPropertyName("invoice_no")]
    public long? InvoiceNo { get; set; }

    [JsonPropertyName("grand_total")]
    public decimal GrandTotal { get; set; }

    [JsonPropertyName("status")]
    public short Status { get; set; } = 1;

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    [JsonPropertyName("lines")]
    public List<Pur1103LineDto> Lines { get; set; } = new();
}

public class Pur1104AllocDto
{
    [JsonPropertyName("alloc_no")]
    public long? AllocNo { get; set; }

    [JsonPropertyName("payment_no")]
    public long? PaymentNo { get; set; }

    [JsonPropertyName("invoice_no")]
    public long InvoiceNo { get; set; }

    [JsonPropertyName("allocated_amount")]
    public decimal AllocatedAmount { get; set; }
}

public class Pur1104OpenInvoiceDto
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

public class Pur1104PaymentDto
{
    [JsonPropertyName("payment_no")]
    public long? PaymentNo { get; set; }

    [JsonPropertyName("payment_id")]
    public string? PaymentId { get; set; }

    [JsonPropertyName("payment_date")]
    public DateTime PaymentDate { get; set; }

    [JsonPropertyName("supplier_no")]
    public long SupplierNo { get; set; }

    [JsonPropertyName("supplier_name")]
    public string? SupplierName { get; set; }

    [JsonPropertyName("payment_method")]
    public short PaymentMethod { get; set; } = 1; // 1=Cash 2=Bank 3=Cheque 4=Card

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("allocated_amount")]
    public decimal AllocatedAmount { get; set; }

    [JsonPropertyName("unallocated_amount")]
    public decimal UnallocatedAmount { get; set; }

    [JsonPropertyName("status")]
    public short Status { get; set; } = 1;

    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }

    [JsonPropertyName("allocations")]
    public List<Pur1104AllocDto> Allocations { get; set; } = new();
}

public class Pur1105LineDto
{
    [JsonPropertyName("receipt_dtl_no")]
    public long? ReceiptDtlNo { get; set; }

    [JsonPropertyName("item_no")]
    public long ItemNo { get; set; }

    [JsonPropertyName("uom_no")]
    public long? UomNo { get; set; }

    [JsonPropertyName("quantity")]
    public decimal Quantity { get; set; }
}

public class Pur1105ReceiptDto
{
    [JsonPropertyName("receipt_no")]
    public long? ReceiptNo { get; set; }

    [JsonPropertyName("receipt_id")]
    public string? ReceiptId { get; set; }

    [JsonPropertyName("receipt_date")]
    public DateTime ReceiptDate { get; set; }

    [JsonPropertyName("supplier_no")]
    public long SupplierNo { get; set; }

    [JsonPropertyName("warehouse_no")]
    public long WarehouseNo { get; set; }

    [JsonPropertyName("status")]
    public short Status { get; set; } = 1;

    [JsonPropertyName("lines")]
    public List<Pur1105LineDto> Lines { get; set; } = new();
}

public class Pur1106AllocDto
{
    [JsonPropertyName("alloc_no")]
    public long? AllocNo { get; set; }

    [JsonPropertyName("landed_cost_no")]
    public long? LandedCostNo { get; set; }

    [JsonPropertyName("invoice_no")]
    public long InvoiceNo { get; set; }

    [JsonPropertyName("allocated_amount")]
    public decimal AllocatedAmount { get; set; }
}

public class Pur1106LandedCostDto
{
    [JsonPropertyName("landed_cost_no")]
    public long? LandedCostNo { get; set; }

    [JsonPropertyName("cost_date")]
    public DateTime CostDate { get; set; }

    [JsonPropertyName("cost_type")]
    public string CostType { get; set; } = "FREIGHT";

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("status")]
    public short Status { get; set; } = 1;

    [JsonPropertyName("allocations")]
    public List<Pur1106AllocDto> Allocations { get; set; } = new();
}
