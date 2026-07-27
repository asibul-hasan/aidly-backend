using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AidlyErp.Domain.Common;

namespace AidlyErp.Domain.Pur;

[Table("pur_supplier")]
public class PurSupplier : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("supplier_no")]
    public long SupplierNo { get; set; }

    [Column("supplier_code")]
    [StringLength(30)]
    public string SupplierCode { get; set; } = string.Empty;

    [Column("supplier_name")]
    [StringLength(200)]
    public string SupplierName { get; set; } = string.Empty;

    [Column("company_name")]
    [StringLength(200)]
    public string? CompanyName { get; set; }

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

    [Column("tax_number")]
    [StringLength(50)]
    public string? TaxNumber { get; set; }

    [Column("currency_no")]
    public long? CurrencyNo { get; set; }

    [Column("opening_balance")]
    public decimal OpeningBalance { get; set; } = 0m;

    [Column("current_balance")]
    public decimal CurrentBalance { get; set; } = 0m;

    [Column("gl_account_no")]
    public long? GlAccountNo { get; set; }

    [Column("status")]
    [StringLength(20)]
    public string Status { get; set; } = "ACTIVE";

    [Column("company_no")]
    public long CompanyNo { get; set; }

    [Column("branch_no")]
    public long? BranchNo { get; set; }
}

[Table("pur_supplier_product")]
public class PurSupplierProduct : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("sup_prod_no")]
    public long SupProdNo { get; set; }

    [Column("supplier_no")]
    public long SupplierNo { get; set; }

    [Column("product_no")]
    public long ProductNo { get; set; }

    [Column("supplier_product_code")]
    [StringLength(50)]
    public string? SupplierProductCode { get; set; }

    [Column("last_purchase_price")]
    public decimal? LastPurchasePrice { get; set; }
}

[Table("pur_supplier_ledger")]
public class PurSupplierLedger : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("supplier_ledger_no")]
    public long SupplierLedgerNo { get; set; }

    [Column("supplier_no")]
    public long SupplierNo { get; set; }

    [Column("trans_date")]
    public DateTime TransDate { get; set; }

    [Column("trans_type")]
    [StringLength(40)]
    public string TransType { get; set; } = string.Empty;

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
    public long LedgerNo { get => SupplierLedgerNo; set => SupplierLedgerNo = value; }
}

[Table("pur_order")]
public class PurOrder : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("order_no")]
    public long OrderNo { get; set; }

    [Column("order_id")]
    [StringLength(40)]
    public string OrderId { get; set; } = string.Empty;

    [Column("supplier_no")]
    public long SupplierNo { get; set; }

    [Column("warehouse_no")]
    public long WarehouseNo { get; set; }

    [Column("order_date")]
    public DateTime OrderDate { get; set; }

    [Column("expected_delivery_date")]
    public DateTime? ExpectedDeliveryDate { get; set; }

    [Column("total_amount")]
    public decimal TotalAmount { get; set; } = 0m;

    [Column("tax_amount")]
    public decimal TaxAmount { get; set; } = 0m;

    [Column("grand_total")]
    public decimal GrandTotal { get; set; } = 0m;

    [Column("status")]
    public short Status { get; set; } = 1; // 1=Draft 2=Submitted 3=Approved 4=Cancelled

    [Column("approval_request_no")]
    public long? ApprovalRequestNo { get; set; }

    [Column("remarks")]
    public string? Remarks { get; set; }

    [Column("company_no")]
    public long CompanyNo { get; set; }

    [Column("branch_no")]
    public long BranchNo { get; set; }
}

[Table("pur_order_dtl")]
public class PurOrderDtl : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("order_dtl_no")]
    public long OrderDtlNo { get; set; }

    [Column("order_no")]
    public long OrderNo { get; set; }

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

    [Column("remarks")]
    [StringLength(250)]
    public string? Remarks { get; set; }

    [NotMapped]
    public long ProductNo { get => ItemNo; set => ItemNo = value; }

    [NotMapped]
    public decimal Qty { get => Quantity; set => Quantity = value; }

    [NotMapped]
    public decimal TotalPrice { get => LineTotal; set => LineTotal = value; }
}
