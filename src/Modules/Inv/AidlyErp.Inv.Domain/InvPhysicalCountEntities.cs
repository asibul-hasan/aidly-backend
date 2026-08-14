using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AidlyErp.Shared.Core;

namespace AidlyErp.Inv.Domain;

/// <summary>
/// A stocktake sheet. Posting one does not write stock directly — it produces an
/// <see cref="InvStockAdjustment"/> of type Count and puts that through the posting engine, so a
/// count reaches the ledger by the same path as every other movement.
/// </summary>
[Table("inv_physical_count")]
public class InvPhysicalCount : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("count_no")]
    public long CountNo { get; set; }

    [Column("company_no")]
    public long CompanyNo { get; set; }

    [Column("branch_no")]
    public long BranchNo { get; set; }

    [Column("count_id")]
    [StringLength(40)]
    public string CountId { get; set; } = string.Empty;

    [Column("count_date")]
    public DateTime CountDate { get; set; }

    [Column("warehouse_no")]
    public long WarehouseNo { get; set; }

    /// <summary>1=Full 2=Cycle 3=Spot.</summary>
    [Column("count_type")]
    public short CountType { get; set; } = 1;

    /// <summary>1=Draft 2=Counting 3=Review 4=Posted 5=Cancelled.</summary>
    [Column("status")]
    public short Status { get; set; } = 1;

    /// <summary>Advisory: movements on counted cells should be held while the sheet is open.</summary>
    [Column("freeze_stock")]
    public short FreezeStock { get; set; }

    /// <summary>The adjustment posting created; the audit trail from sheet to ledger.</summary>
    [Column("variance_adjustment_no")]
    public long? VarianceAdjustmentNo { get; set; }

    [Column("remarks")]
    public string? Remarks { get; set; }
}

[Table("inv_physical_count_dtl")]
public class InvPhysicalCountDtl
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("count_dtl_no")]
    public long CountDtlNo { get; set; }

    [Column("count_no")]
    public long CountNo { get; set; }

    [Column("line_no")]
    public int LineNo { get; set; }

    [Column("product_no")]
    public long ProductNo { get; set; }

    [Column("variant_no")]
    public long? VariantNo { get; set; }

    [Column("batch_no")]
    public long? BatchNo { get; set; }

    /// <summary>
    /// What the system held when the sheet was opened. Frozen deliberately: recomputing it at post
    /// time would fold any movement since the count into the variance and blame it on the counter.
    /// </summary>
    [Column("system_qty")]
    public decimal SystemQty { get; set; }

    [Column("counted_qty")]
    public decimal CountedQty { get; set; }

    /// <summary>Database-generated as counted − system; never written by the application.</summary>
    [Column("variance_qty")]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public decimal VarianceQty { get; private set; }

    [Column("unit_cost")]
    public decimal UnitCost { get; set; }

    [Column("remarks")]
    [StringLength(250)]
    public string? Remarks { get; set; }

    [Column("row_version")]
    public long RowVersion { get; set; } = 1;
}
