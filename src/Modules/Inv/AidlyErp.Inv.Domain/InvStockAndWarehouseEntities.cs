using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AidlyErp.Shared.Core;

namespace AidlyErp.Inv.Domain;

[Table("inv_warehouse")]
public class InvWarehouse : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("warehouse_no")]
    public long WarehouseNo { get; set; }

    /// <summary>The business key is <c>warehouse_id</c> in the schema, not <c>warehouse_code</c>.</summary>
    [Column("warehouse_id")]
    [StringLength(30)]
    public string WarehouseId { get; set; } = string.Empty;

    [Column("warehouse_name")]
    [StringLength(150)]
    public string WarehouseName { get; set; } = string.Empty;

    [Column("address")]
    [StringLength(250)]
    public string? Address { get; set; }

    [Column("branch_no")]
    public long BranchNo { get; set; }

    [Column("company_no")]
    public long CompanyNo { get; set; }
}

[Table("inv_rack")]
public class InvRack : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("rack_no")]
    public long RackNo { get; set; }

    [Column("warehouse_no")]
    public long WarehouseNo { get; set; }

    [Column("rack_code")]
    [StringLength(30)]
    public string RackCode { get; set; } = string.Empty;

    [Column("rack_name")]
    [StringLength(100)]
    public string RackName { get; set; } = string.Empty;
}

[Table("inv_stock")]
public class InvStock : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("stock_no")]
    public long StockNo { get; set; }

    [Column("product_no")]
    public long ProductNo { get; set; }

    [Column("warehouse_no")]
    public long WarehouseNo { get; set; }

    [Column("rack_no")]
    public long? RackNo { get; set; }

    [Column("qty")]
    public decimal Qty { get; set; } = 0m;

    [Column("qty_allocated")]
    public decimal QtyAllocated { get; set; } = 0m;

    [Column("qty_on_order")]
    public decimal QtyOnOrder { get; set; } = 0m;

    [Column("avg_cost")]
    public decimal AvgCost { get; set; } = 0m;

    [Column("branch_no")]
    public long BranchNo { get; set; }

    [Column("company_no")]
    public long CompanyNo { get; set; }

    [NotMapped]
    public long ItemNo { get => ProductNo; set => ProductNo = value; }

    [NotMapped]
    public decimal QtyOnHand { get => Qty; set => Qty = value; }

    [NotMapped]
    public decimal AvgUnitCost { get => AvgCost; set => AvgCost = value; }
}

[Table("inv_stock_ledger")]
public class InvStockLedger : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("ledger_no")]
    public long LedgerNo { get; set; }

    [Column("product_no")]
    public long ProductNo { get; set; }

    [Column("variant_no")]
    public long? VariantNo { get; set; }

    [Column("batch_no")]
    public long? BatchNo { get; set; }

    [Column("warehouse_no")]
    public long WarehouseNo { get; set; }

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

    [Column("movement_type")]
    [StringLength(20)]
    public string? MovementType { get; set; }

    [Column("qty")]
    public decimal Qty { get; set; } = 0m;

    [Column("qty_in")]
    public decimal QtyIn { get; set; } = 0m;

    [Column("qty_out")]
    public decimal QtyOut { get; set; } = 0m;

    [Column("unit_cost")]
    public decimal UnitCost { get; set; } = 0m;

    [Column("total_cost")]
    public decimal TotalCost { get; set; } = 0m;

    [Column("balance_qty")]
    public decimal BalanceQty { get; set; } = 0m;

    [Column("balance_avg_cost")]
    public decimal BalanceAvgCost { get; set; } = 0m;

    [Column("fin_year_no")]
    public long? FinYearNo { get; set; }

    [Column("fin_period_no")]
    public long? FinPeriodNo { get; set; }

    [Column("branch_no")]
    public long BranchNo { get; set; }

    [Column("company_no")]
    public long CompanyNo { get; set; }

    [NotMapped]
    public long ItemNo { get => ProductNo; set => ProductNo = value; }

    [NotMapped]
    public DateTime TxnDate { get => TransDate; set => TransDate = value; }

    [NotMapped]
    public string RefDocType { get => TransType; set => TransType = value; }

    [NotMapped]
    public long RefDocNo { get => DocNo; set => DocNo = value; }

    [NotMapped]
    public string? RefDocId { get => DocId; set => DocId = value; }
}

[Table("inv_batch")]
public class InvBatch : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("batch_no")]
    public long BatchNo { get; set; }

    [Column("product_no")]
    public long ProductNo { get; set; }

    [Column("batch_number")]
    [StringLength(100)]
    public string BatchNumber { get; set; } = string.Empty;

    [Column("mfg_date")]
    public DateTime? MfgDate { get; set; }

    [Column("expiry_date")]
    public DateTime? ExpiryDate { get; set; }

    [Column("qty")]
    public decimal Qty { get; set; } = 0m;

    [NotMapped]
    public string BatchCode { get => BatchNumber; set => BatchNumber = value; }

    [NotMapped]
    public DateTime? ExpDate { get => ExpiryDate; set => ExpiryDate = value; }

    [NotMapped]
    public decimal QtyOnHand { get => Qty; set => Qty = value; }
}

[Table("inv_reorder")]
public class InvReorder : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("reorder_no")]
    public long ReorderNo { get; set; }

    [Column("product_no")]
    public long ProductNo { get; set; }

    [Column("warehouse_no")]
    public long WarehouseNo { get; set; }

    [Column("min_qty")]
    public decimal MinQty { get; set; }

    [Column("max_qty")]
    public decimal MaxQty { get; set; }

    [Column("reorder_qty")]
    public decimal ReorderQty { get; set; }

    [NotMapped]
    public long ItemNo { get => ProductNo; set => ProductNo = value; }
}

[Table("inv_valuation_layer")]
public class InvValuationLayer : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("layer_no")]
    public long LayerNo { get; set; }

    [Column("product_no")]
    public long ProductNo { get; set; }

    [Column("warehouse_no")]
    public long WarehouseNo { get; set; }

    [Column("doc_type")]
    [StringLength(30)]
    public string? DocType { get; set; }

    [Column("doc_no")]
    public long? DocNo { get; set; }

    [Column("receipt_date")]
    public DateTime ReceiptDate { get; set; }

    [Column("qty_received")]
    public decimal QtyReceived { get; set; }

    [Column("qty_remaining")]
    public decimal QtyRemaining { get; set; }

    [Column("unit_cost")]
    public decimal UnitCost { get; set; }

    [Column("total_cost")]
    public decimal TotalCost { get; set; }

    [Column("company_no")]
    public long CompanyNo { get; set; }

    [Column("branch_no")]
    public long BranchNo { get; set; }

    [NotMapped]
    public long ItemNo { get => ProductNo; set => ProductNo = value; }
}
