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

    [Column("warehouse_name_nls")]
    [StringLength(150)]
    public string? WarehouseNameNls { get; set; }

    /// <summary>1=Store 2=Outlet 3=Transit. Inter-warehouse transfers stage stock in the type-3 store.</summary>
    [Column("warehouse_type")]
    public short WarehouseType { get; set; } = 1;

    [Column("manager_employee_no")]
    public long? ManagerEmployeeNo { get; set; }

    [Column("is_default")]
    public short IsDefault { get; set; } = 0;

    /// <summary>When 0, the posting engine refuses to drive this warehouse's stock below zero.</summary>
    [Column("allow_negative_stock")]
    public short AllowNegativeStock { get; set; } = 0;

    /// <summary>Marks a warehouse a POS terminal is allowed to sell from.</summary>
    [Column("is_sale_point")]
    public short IsSalePoint { get; set; } = 0;

    [Column("remarks")]
    public string? Remarks { get; set; }
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

    [Column("rack_id")]
    [StringLength(30)]
    public string RackId { get; set; } = string.Empty;

    [Column("rack_name")]
    [StringLength(100)]
    public string? RackName { get; set; }

    [Column("aisle")]
    [StringLength(20)]
    public string? Aisle { get; set; }

    [Column("shelf")]
    [StringLength(20)]
    public string? Shelf { get; set; }

    [Column("bin")]
    [StringLength(20)]
    public string? Bin { get; set; }

    [Column("rack")]
    [StringLength(20)]
    public string? Rack { get; set; }

    [NotMapped]
    public string RackCode { get => RackId; set => RackId = value; }
}

[Table("inv_stock")]
public class InvStock
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("stock_no")]
    public long StockNo { get; set; }

    [Column("company_no")]
    public long CompanyNo { get; set; }

    [Column("branch_no")]
    public long BranchNo { get; set; }

    [Column("warehouse_no")]
    public long WarehouseNo { get; set; }

    [Column("product_no")]
    public long ProductNo { get; set; }

    [Column("variant_no")]
    public long? VariantNo { get; set; }

    [Column("batch_no")]
    public long? BatchNo { get; set; }

    [Column("qty_on_hand")]
    public decimal QtyOnHand { get; set; } = 0m;

    [Column("qty_reserved")]
    public decimal QtyReserved { get; set; } = 0m;

    /// <summary>DB-generated: qty_on_hand - qty_reserved.</summary>
    [Column("qty_available")]
    public decimal? QtyAvailable { get; set; }

    [Column("avg_cost")]
    public decimal AvgCost { get; set; } = 0m;

    [Column("last_cost")]
    public decimal LastCost { get; set; } = 0m;

    /// <summary>DB-generated: qty_on_hand * avg_cost.</summary>
    [Column("stock_value")]
    public decimal? StockValue { get; set; }

    [Column("last_movement_at")]
    public DateTime? LastMovementAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("row_version")]
    public long RowVersion { get; set; } = 1;

    [NotMapped]
    public long ItemNo { get => ProductNo; set => ProductNo = value; }

    [NotMapped]
    public decimal Qty { get => QtyOnHand; set => QtyOnHand = value; }

    [NotMapped]
    public decimal QtyAllocated { get => QtyReserved; set => QtyReserved = value; }
}

[Table("inv_stock_ledger")]
public class InvStockLedger
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("stock_ledger_no")]
    public long StockLedgerNo { get; set; }

    [Column("company_no")]
    public long CompanyNo { get; set; }

    [Column("branch_no")]
    public long BranchNo { get; set; }

    [Column("warehouse_no")]
    public long WarehouseNo { get; set; }

    [Column("product_no")]
    public long ProductNo { get; set; }

    [Column("variant_no")]
    public long? VariantNo { get; set; }

    [Column("batch_no")]
    public long? BatchNo { get; set; }

    [Column("movement_date")]
    public DateTime MovementDate { get; set; }

    [Column("fin_year_no")]
    public long FinYearNo { get; set; }

    [Column("fin_period_no")]
    public long? FinPeriodNo { get; set; }

    /// <summary>1=Opening,2=Purchase,3=PurReturn,4=Sale,5=SalReturn,6=TransferOut,7=TransferIn,8=AdjIn,9=AdjOut,10=CountVariance,11=Assemble,12=Disassemble.</summary>
    [Column("movement_type")]
    public short MovementType { get; set; }

    /// <summary>+1 = IN, -1 = OUT.</summary>
    [Column("direction")]
    public short Direction { get; set; }

    [Column("qty_base")]
    public decimal QtyBase { get; set; } = 0m;

    [Column("unit_cost")]
    public decimal UnitCost { get; set; } = 0m;

    [Column("total_cost")]
    public decimal TotalCost { get; set; } = 0m;

    [Column("balance_after")]
    public decimal BalanceAfter { get; set; } = 0m;

    [Column("avg_cost_after")]
    public decimal AvgCostAfter { get; set; } = 0m;

    [Column("ref_doc_type")]
    public short RefDocType { get; set; }

    [Column("ref_doc_no")]
    [StringLength(40)]
    public string RefDocNo { get; set; } = string.Empty;

    [Column("ref_doc_pk")]
    public long? RefDocPk { get; set; }

    [Column("ref_line_no")]
    public long? RefLineNo { get; set; }

    [Column("is_reversal")]
    public short IsReversal { get; set; } = 0;

    [Column("reversed_ledger_no")]
    public long? ReversedLedgerNo { get; set; }

    [Column("remarks")]
    [StringLength(250)]
    public string? Remarks { get; set; }

    [Column("created_by")]
    public long CreatedBy { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [NotMapped]
    public long LedgerNo { get => StockLedgerNo; set => StockLedgerNo = value; }

    [NotMapped]
    public long ItemNo { get => ProductNo; set => ProductNo = value; }

    [NotMapped]
    public DateTime TransDate { get => MovementDate; set => MovementDate = value; }
}

[Table("inv_batch")]
public class InvBatch : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("batch_no")]
    public long BatchNo { get; set; }

    [Column("company_no")]
    public long CompanyNo { get; set; }

    [Column("product_no")]
    public long ProductNo { get; set; }

    [Column("variant_no")]
    public long? VariantNo { get; set; }

    [Column("batch_code")]
    [StringLength(60)]
    public string BatchCode { get; set; } = string.Empty;

    [Column("mfg_date")]
    public DateTime? MfgDate { get; set; }

    [Column("expiry_date")]
    public DateTime? ExpiryDate { get; set; }

    [Column("supplier_no")]
    public long? SupplierNo { get; set; }

    [Column("received_cost")]
    public decimal ReceivedCost { get; set; } = 0m;

    [Column("mrp")]
    public decimal? Mrp { get; set; }

    [Column("remarks")]
    public string? Remarks { get; set; }

    [NotMapped]
    public string BatchNumber { get => BatchCode; set => BatchCode = value; }

    [NotMapped]
    public DateTime? ExpDate { get => ExpiryDate; set => ExpiryDate = value; }
}

[Table("inv_reorder")]
public class InvReorder : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("reorder_no")]
    public long ReorderNo { get; set; }

    [Column("company_no")]
    public long CompanyNo { get; set; }

    [Column("warehouse_no")]
    public long WarehouseNo { get; set; }

    [Column("product_no")]
    public long ProductNo { get; set; }

    [Column("variant_no")]
    public long? VariantNo { get; set; }

    [Column("reorder_level")]
    public decimal ReorderLevel { get; set; } = 0m;

    [Column("reorder_qty")]
    public decimal ReorderQty { get; set; } = 0m;

    [Column("min_stock")]
    public decimal MinStock { get; set; } = 0m;

    [Column("max_stock")]
    public decimal? MaxStock { get; set; }

    [Column("preferred_supplier_no")]
    public long? PreferredSupplierNo { get; set; }

    [Column("lead_time_days")]
    public int? LeadTimeDays { get; set; }

    [NotMapped]
    public long ItemNo { get => ProductNo; set => ProductNo = value; }

    [NotMapped]
    public decimal MinQty { get => MinStock; set => MinStock = value; }

    [NotMapped]
    public decimal MaxQty { get => MaxStock ?? 0m; set => MaxStock = value; }
}

[Table("inv_valuation_layer")]
public class InvValuationLayer
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("layer_no")]
    public long LayerNo { get; set; }

    [Column("company_no")]
    public long CompanyNo { get; set; }

    [Column("warehouse_no")]
    public long WarehouseNo { get; set; }

    [Column("product_no")]
    public long ProductNo { get; set; }

    [Column("variant_no")]
    public long? VariantNo { get; set; }

    [Column("batch_no")]
    public long? BatchNo { get; set; }

    [Column("receipt_ledger_no")]
    public long ReceiptLedgerNo { get; set; }

    [Column("receipt_date")]
    public DateTime ReceiptDate { get; set; }

    [Column("original_qty")]
    public decimal OriginalQty { get; set; }

    [Column("remaining_qty")]
    public decimal RemainingQty { get; set; }

    [Column("unit_cost")]
    public decimal UnitCost { get; set; }

    [Column("is_exhausted")]
    public short IsExhausted { get; set; } = 0;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("row_version")]
    public long RowVersion { get; set; } = 1;

    [NotMapped]
    public long ItemNo { get => ProductNo; set => ProductNo = value; }

    [NotMapped]
    public long? DocNo { get => ReceiptLedgerNo; set => ReceiptLedgerNo = value ?? 0; }

    [NotMapped]
    public decimal QtyReceived { get => OriginalQty; set => OriginalQty = value; }

    [NotMapped]
    public decimal QtyRemaining { get => RemainingQty; set => RemainingQty = value; }
}
