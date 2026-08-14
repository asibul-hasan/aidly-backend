using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AidlyErp.Shared.Core;

namespace AidlyErp.Hrm.Domain;

[Table("hrm_designation")]
public class HrmDesignation : AuditEntity, IBranchScopedEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("designation_no")]
    public long DesignationNo { get; set; }

    [Column("designation_id")]
    [StringLength(20)]
    public string? DesignationId { get; set; }

    [Column("designation_name")]
    [StringLength(150)]
    public string DesignationName { get; set; } = string.Empty;

    [Column("department_no")]
    public long DepartmentNo { get; set; }

    [Column("job_category")]
    public short? JobCategory { get; set; }

    [Column("grade_no")]
    public long? GradeNo { get; set; }

    [Column("grade_level")]
    [StringLength(20)]
    public string? GradeLevel { get; set; }

    [Column("order_sl")]
    public int? OrderSl { get; set; }

    [Column("min_salary")]
    public decimal? MinSalary { get; set; }

    [Column("max_salary")]
    public decimal? MaxSalary { get; set; }

    [Column("is_overtime_eligible")]
    public short? IsOvertimeEligible { get; set; } = 0;

    [Column("branch_no")]
    public long? BranchNo { get; set; }

    /// <summary>Not a column — the Java HRM schema is branch-scoped; company is reached
    /// via the branch. Mapping it made every HRM query select a non-existent column.</summary>
    [NotMapped]
    public long? CompanyNo { get; set; }

    [Column("remarks")]
    public string? Remarks { get; set; }

    protected override void NullifyBusinessId()
    {
        DesignationId = null;
    }
}
