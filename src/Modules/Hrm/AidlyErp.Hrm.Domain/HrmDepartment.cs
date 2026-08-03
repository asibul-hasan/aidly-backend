using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AidlyErp.Shared.Core;

namespace AidlyErp.Hrm.Domain;

[Table("hrm_department")]
public class HrmDepartment : AuditEntity, IBranchScopedEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("department_no")]
    public long DepartmentNo { get; set; }

    [Column("department_id")]
    [StringLength(20)]
    public string? DepartmentId { get; set; }

    [Column("department_name")]
    [StringLength(150)]
    public string DepartmentName { get; set; } = string.Empty;

    [Column("parent_department_no")]
    public long? ParentDepartmentNo { get; set; }

    [Column("cost_center_no")]
    public long? CostCenterNo { get; set; }

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
        DepartmentId = null;
    }
}
