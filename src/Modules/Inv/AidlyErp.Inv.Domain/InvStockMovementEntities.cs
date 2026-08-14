using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AidlyErp.Shared.Core;

namespace AidlyErp.Inv.Domain;

[Table("inv_stock_adjustment")]
public class InvStockAdjustment : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("adjustment_no")]
    public long AdjustmentNo { get; set; }

    [Column("company_no")]
    public long CompanyNo { get; set; }

    [Column("branch_no")]
    public long BranchNo { get; set; }

    [Column("adjustment_id")]
    [StringLength(40)]
    public string AdjustmentId { get; set; } = string.Empty;

    [Column("adjustment_date")]
    public DateTime AdjustmentDate { get; set; }

    [Column("warehouse_no")]
    public long WarehouseNo { get; set; }

    /// <summary>1=Correction,2=Opening,3=Damage,4=Loss/Theft,5=CountVariance,6=Revaluation.</summary>
    [Column("adjustment_type")]
    public short AdjustmentType { get; set; } = 1;

    [Column("reason_code")]
    public short? ReasonCode { get; set; }

    [Column("reason_text")]
    [StringLength(250)]
    public string? ReasonText { get; set; }

    /// <summary>1=Draft,2=Submitted,3=Approved/Posted,4=Cancelled.</summary>
    [Column("status")]
    public short Status { get; set; } = 1;

    [Column("fin_year_no")]
    public long FinYearNo { get; set; }

    [Column("fin_period_no")]
    public long? FinPeriodNo { get; set; }

    [Column("total_in_value")]
    public decimal TotalInValue { get; set; } = 0m;

    [Column("total_out_value")]
    public decimal TotalOutValue { get; set; } = 0m;

    [Column("gl_voucher_no")]
    public long? GlVoucherNo { get; set; }

    [Column("submitted_by")]
    public long? SubmittedBy { get; set; }

    [Column("submitted_at")]
    public DateTime? SubmittedAt { get; set; }

    [Column("approved_by")]
    public long? ApprovedBy { get; set; }

    [Column("approved_at")]
    public DateTime? ApprovedAt { get; set; }

    [Column("posted_at")]
    public DateTime? PostedAt { get; set; }

    [Column("remarks")]
    public string? Remarks { get; set; }

    [NotMapped]
    public long AdjNo { get => AdjustmentNo; set => AdjustmentNo = value; }

    [NotMapped]
    public string AdjId { get => AdjustmentId; set => AdjustmentId = value; }

    [NotMapped]
    public DateTime AdjDate { get => AdjustmentDate; set => AdjustmentDate = value; }

    [NotMapped]
    public string? Reason { get => ReasonText; set => ReasonText = value; }
}

[Table("inv_stock_adjustment_dtl")]
public class InvStockAdjustmentDtl
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("adjustment_dtl_no")]
    public long AdjustmentDtlNo { get; set; }

    [Column("adjustment_no")]
    public long AdjustmentNo { get; set; }

    [Column("line_no")]
    public int LineNo { get; set; }

    [Column("product_no")]
    public long ProductNo { get; set; }

    [Column("variant_no")]
    public long? VariantNo { get; set; }

    [Column("batch_no")]
    public long? BatchNo { get; set; }

    [Column("rack_no")]
    public long? RackNo { get; set; }

    [Column("uom_no")]
    public long UomNo { get; set; }

    /// <summary>+1 increase, -1 decrease.</summary>
    [Column("direction")]
    public short Direction { get; set; } = 1;

    [Column("qty")]
    public decimal Qty { get; set; } = 0m;

    [Column("qty_base")]
    public decimal QtyBase { get; set; } = 0m;

    [Column("unit_cost")]
    public decimal UnitCost { get; set; } = 0m;

    [Column("line_value")]
    public decimal LineValue { get; set; } = 0m;

    [Column("system_qty")]
    public decimal? SystemQty { get; set; }

    [Column("remarks")]
    [StringLength(250)]
    public string? Remarks { get; set; }

    [Column("row_version")]
    public long RowVersion { get; set; } = 1;

    [NotMapped]
    public long AdjDtlNo { get => AdjustmentDtlNo; set => AdjustmentDtlNo = value; }

    [NotMapped]
    public long AdjNo { get => AdjustmentNo; set => AdjustmentNo = value; }

    [NotMapped]
    public long ItemNo { get => ProductNo; set => ProductNo = value; }
}

[Table("inv_stock_transfer")]
public class InvStockTransfer : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("transfer_no")]
    public long TransferNo { get; set; }

    [Column("company_no")]
    public long CompanyNo { get; set; }

    [Column("transfer_id")]
    [StringLength(40)]
    public string TransferId { get; set; } = string.Empty;

    [Column("transfer_date")]
    public DateTime TransferDate { get; set; }

    [Column("from_branch_no")]
    public long FromBranchNo { get; set; }

    [Column("from_warehouse_no")]
    public long FromWarehouseNo { get; set; }

    [Column("to_branch_no")]
    public long ToBranchNo { get; set; }

    [Column("to_warehouse_no")]
    public long ToWarehouseNo { get; set; }

    [Column("transit_warehouse_no")]
    public long? TransitWarehouseNo { get; set; }

    /// <summary>1=Draft,2=Submitted,3=Dispatched,4=Received,5=ShortReceived,6=Cancelled.</summary>
    [Column("status")]
    public short Status { get; set; } = 1;

    [Column("total_qty")]
    public decimal TotalQty { get; set; } = 0m;

    [Column("total_value")]
    public decimal TotalValue { get; set; } = 0m;

    [Column("dispatched_by")]
    public long? DispatchedBy { get; set; }

    [Column("dispatched_at")]
    public DateTime? DispatchedAt { get; set; }

    [Column("received_by")]
    public long? ReceivedBy { get; set; }

    [Column("received_at")]
    public DateTime? ReceivedAt { get; set; }

    [Column("remarks")]
    public string? Remarks { get; set; }

    [NotMapped]
    public long BranchNo { get => FromBranchNo; set => FromBranchNo = value; }
}

[Table("inv_stock_transfer_dtl")]
public class InvStockTransferDtl
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("transfer_dtl_no")]
    public long TransferDtlNo { get; set; }

    [Column("transfer_no")]
    public long TransferNo { get; set; }

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

    [Column("qty_sent")]
    public decimal QtySent { get; set; } = 0m;

    [Column("qty_sent_base")]
    public decimal QtySentBase { get; set; } = 0m;

    [Column("qty_received")]
    public decimal? QtyReceived { get; set; }

    [Column("qty_received_base")]
    public decimal? QtyReceivedBase { get; set; }

    [Column("unit_cost")]
    public decimal UnitCost { get; set; } = 0m;

    [Column("line_value")]
    public decimal LineValue { get; set; } = 0m;

    [Column("remarks")]
    [StringLength(250)]
    public string? Remarks { get; set; }

    [Column("row_version")]
    public long RowVersion { get; set; } = 1;

    [NotMapped]
    public long ItemNo { get => ProductNo; set => ProductNo = value; }

    [NotMapped]
    public decimal Qty { get => QtySent; set => QtySent = value; }
}
