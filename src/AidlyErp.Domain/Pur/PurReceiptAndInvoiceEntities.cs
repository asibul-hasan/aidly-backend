using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AidlyErp.Domain.Common;

namespace AidlyErp.Domain.Pur;

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

    [Column("status")]
    public short Status { get; set; } = 1;

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

    [Column("item_no")]
    public long ItemNo { get; set; }

    [Column("uom_no")]
    public long? UomNo { get; set; }

    [Column("quantity")]
    public decimal Quantity { get; set; } = 0m;

    [NotMapped]
    public long ProductNo { get => ItemNo; set => ItemNo = value; }

    [NotMapped]
    public decimal QtyReceived { get => Quantity; set => Quantity = value; }
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

    [Column("supplier_no")]
    public long SupplierNo { get; set; }

    [Column("warehouse_no")]
    public long WarehouseNo { get; set; }

    [Column("order_no")]
    public long? OrderNo { get; set; }

    [Column("invoice_date")]
    public DateTime InvoiceDate { get; set; }

    [Column("sub_total")]
    public decimal SubTotal { get; set; } = 0m;

    [Column("tax_amount")]
    public decimal TaxAmount { get; set; } = 0m;

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

    [Column("remarks")]
    public string? Remarks { get; set; }

    [Column("posted_by")]
    public long? PostedBy { get; set; }

    [Column("posted_at")]
    public DateTime? PostedAt { get; set; }

    [Column("branch_no")]
    public long BranchNo { get; set; }

    [Column("company_no")]
    public long CompanyNo { get; set; }

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

    [Column("item_no")]
    public long ItemNo { get; set; }

    [Column("uom_no")]
    public long? UomNo { get; set; }

    [Column("quantity")]
    public decimal Quantity { get; set; } = 0m;

    [Column("unit_price")]
    public decimal UnitPrice { get; set; } = 0m;

    [Column("line_total")]
    public decimal LineTotal { get; set; } = 0m;

    [Column("discount_amount")]
    public decimal DiscountAmount { get; set; } = 0m;

    [Column("tax_amount")]
    public decimal TaxAmount { get; set; } = 0m;

    [Column("net_amount")]
    public decimal NetAmount { get; set; } = 0m;

    [Column("remarks")]
    [StringLength(250)]
    public string? Remarks { get; set; }

    [NotMapped]
    public long ProductNo { get => ItemNo; set => ItemNo = value; }

    [NotMapped]
    public decimal Qty { get => Quantity; set => Quantity = value; }
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

    [Column("supplier_no")]
    public long SupplierNo { get; set; }

    [Column("warehouse_no")]
    public long WarehouseNo { get; set; }

    [Column("invoice_no")]
    public long? InvoiceNo { get; set; }

    [Column("return_date")]
    public DateTime ReturnDate { get; set; }

    [Column("grand_total")]
    public decimal GrandTotal { get; set; } = 0m;

    [Column("status")]
    public short Status { get; set; } = 1;

    [Column("reason")]
    [StringLength(250)]
    public string? Reason { get; set; }

    [Column("company_no")]
    public long CompanyNo { get; set; }

    [Column("branch_no")]
    public long BranchNo { get; set; }
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

    [Column("item_no")]
    public long ItemNo { get; set; }

    [Column("uom_no")]
    public long? UomNo { get; set; }

    [Column("quantity")]
    public decimal Quantity { get; set; } = 0m;

    [Column("unit_price")]
    public decimal UnitPrice { get; set; } = 0m;

    [Column("line_total")]
    public decimal LineTotal { get; set; } = 0m;

    [NotMapped]
    public long ProductNo { get => ItemNo; set => ItemNo = value; }

    [NotMapped]
    public decimal Qty { get => Quantity; set => Quantity = value; }
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

    [Column("allocated_amount")]
    public decimal AllocatedAmount { get; set; } = 0m;

    [Column("unallocated_amount")]
    public decimal UnallocatedAmount { get; set; } = 0m;

    [Column("status")]
    public short Status { get; set; } = 1;

    [Column("remarks")]
    public string? Remarks { get; set; }

    [Column("company_no")]
    public long CompanyNo { get; set; }

    [Column("branch_no")]
    public long BranchNo { get; set; }
}

[Table("pur_payment_alloc")]
public class PurPaymentAlloc : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("alloc_no")]
    public long AllocNo { get; set; }

    [Column("payment_no")]
    public long PaymentNo { get; set; }

    [Column("invoice_no")]
    public long InvoiceNo { get; set; }

    [Column("allocated_amount")]
    public decimal AllocatedAmount { get; set; } = 0m;
}

[Table("pur_landed_cost")]
public class PurLandedCost : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("landed_cost_no")]
    public long LandedCostNo { get; set; }

    [Column("cost_date")]
    public DateTime CostDate { get; set; }

    [Column("cost_type")]
    [StringLength(50)]
    public string CostType { get; set; } = "FREIGHT";

    [Column("amount")]
    public decimal Amount { get; set; } = 0m;

    [Column("status")]
    public short Status { get; set; } = 1;

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
    [Column("alloc_no")]
    public long AllocNo { get; set; }

    [Column("landed_cost_no")]
    public long LandedCostNo { get; set; }

    [Column("invoice_no")]
    public long InvoiceNo { get; set; }

    [Column("allocated_amount")]
    public decimal AllocatedAmount { get; set; } = 0m;

    [NotMapped]
    public long CostNo { get => LandedCostNo; set => LandedCostNo = value; }

    [NotMapped]
    public decimal AllocatedCost { get => AllocatedAmount; set => AllocatedAmount = value; }
}
