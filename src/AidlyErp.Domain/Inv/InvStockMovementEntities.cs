using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AidlyErp.Domain.Common;

namespace AidlyErp.Domain.Inv;

[Table("inv_stock_adjustment")]
public class InvStockAdjustment : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("adj_no")]
    public long AdjNo { get; set; }

    [Column("adj_id")]
    [StringLength(40)]
    public string AdjId { get; set; } = string.Empty;

    [Column("warehouse_no")]
    public long WarehouseNo { get; set; }

    [Column("adj_date")]
    public DateTime AdjDate { get; set; }

    [Column("reason")]
    [StringLength(250)]
    public string? Reason { get; set; }

    [Column("remarks")]
    public string? Remarks { get; set; }

    [Column("status")]
    public short Status { get; set; } = 1;

    [Column("branch_no")]
    public long BranchNo { get; set; }

    [Column("company_no")]
    public long CompanyNo { get; set; }

    [NotMapped]
    public long AdjustmentNo { get => AdjNo; set => AdjNo = value; }

    [NotMapped]
    public string AdjustmentId { get => AdjId; set => AdjId = value; }

    [NotMapped]
    public DateTime AdjustmentDate { get => AdjDate; set => AdjDate = value; }
}

[Table("inv_stock_adjustment_dtl")]
public class InvStockAdjustmentDtl : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("adj_dtl_no")]
    public long AdjDtlNo { get; set; }

    [Column("adj_no")]
    public long AdjNo { get; set; }

    [Column("product_no")]
    public long ProductNo { get; set; }

    [Column("uom_no")]
    public long? UomNo { get; set; }

    [Column("adjustment_type")]
    public short AdjustmentType { get; set; } = 1;

    [Column("qty_diff")]
    public decimal QtyDiff { get; set; } = 0m;

    [Column("unit_cost")]
    public decimal UnitCost { get; set; } = 0m;

    [Column("reason")]
    [StringLength(250)]
    public string? Reason { get; set; }

    [NotMapped]
    public long AdjustmentNo { get => AdjNo; set => AdjNo = value; }

    [NotMapped]
    public long ItemNo { get => ProductNo; set => ProductNo = value; }

    [NotMapped]
    public decimal Qty { get => QtyDiff; set => QtyDiff = value; }
}

[Table("inv_stock_transfer")]
public class InvStockTransfer : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("transfer_no")]
    public long TransferNo { get; set; }

    [Column("transfer_id")]
    [StringLength(40)]
    public string TransferId { get; set; } = string.Empty;

    [Column("from_warehouse_no")]
    public long FromWarehouseNo { get; set; }

    [Column("to_warehouse_no")]
    public long ToWarehouseNo { get; set; }

    [Column("transfer_date")]
    public DateTime TransferDate { get; set; }

    [Column("remarks")]
    public string? Remarks { get; set; }

    [Column("status")]
    public short Status { get; set; } = 1;

    [Column("branch_no")]
    public long BranchNo { get; set; }

    [Column("company_no")]
    public long CompanyNo { get; set; }
}

[Table("inv_stock_transfer_dtl")]
public class InvStockTransferDtl : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("transfer_dtl_no")]
    public long TransferDtlNo { get; set; }

    [Column("transfer_no")]
    public long TransferNo { get; set; }

    [Column("product_no")]
    public long ProductNo { get; set; }

    [Column("uom_no")]
    public long? UomNo { get; set; }

    [Column("qty")]
    public decimal Qty { get; set; } = 0m;

    [NotMapped]
    public long ItemNo { get => ProductNo; set => ProductNo = value; }
}
