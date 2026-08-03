using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AidlyErp.Shared.Core;

namespace AidlyErp.Hrm.Domain;

[Table("hrm_leave_type")]
public class HrmLeaveType : AuditEntity, IBranchScopedEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("leave_type_no")]
    public long LeaveTypeNo { get; set; }

    [Column("leave_type_id")]
    [StringLength(20)]
    public string? LeaveTypeId { get; set; }

    [Column("leave_type_name")]
    [StringLength(100)]
    public string LeaveTypeName { get; set; } = string.Empty;

    [Column("is_paid")]
    public short IsPaid { get; set; } = 1;

    [Column("statutory_category")]
    [StringLength(20)]
    public string StatutoryCategory { get; set; } = "NONE";

    [Column("branch_no")]
    public long? BranchNo { get; set; }

    /// <summary>Not a column — the Java HRM schema is branch-scoped; company is reached
    /// via the branch. Mapping it made every HRM query select a non-existent column.</summary>
    [NotMapped]
    public long? CompanyNo { get; set; }

    [Column("accrual_method")]
    public short? AccrualMethod { get; set; }

    [Column("requires_approval")]
    public short? RequiresApproval { get; set; }

    [Column("is_encashable")]
    public short? IsEncashable { get; set; }

    [Column("default_days_per_year")]
    public decimal? DefaultDaysPerYear { get; set; }

    [Column("max_carry_forward_days")]
    public decimal? MaxCarryForwardDays { get; set; }

    [Column("is_carry_forward_allowed")]
    public short? IsCarryForwardAllowed { get; set; }

    [Column("eligibility_service_months")]
    public int? EligibilityServiceMonths { get; set; }

    [Column("documentation_required")]
    public short? DocumentationRequired { get; set; }

    [Column("carry_forward_validity_months")]
    public int? CarryForwardValidityMonths { get; set; }

    [Column("is_lapsable")]
    public short? IsLapsable { get; set; }

    [Column("min_duration_days")]
    public decimal? MinDurationDays { get; set; }

    [Column("max_duration_days")]
    public decimal? MaxDurationDays { get; set; }

    [Column("remarks")]
    public string? Remarks { get; set; }

    /// <summary>Alias — the schema column is <c>is_carry_forward_allowed</c>.</summary>
    [NotMapped]
    public short? IsCarryForward
    {
        get => IsCarryForwardAllowed;
        set => IsCarryForwardAllowed = value;
    }

    /// <summary>Alias — the schema column is <c>max_duration_days</c> (per-application cap).</summary>
    [NotMapped]
    public decimal? MaxDays
    {
        get => MaxDurationDays;
        set => MaxDurationDays = value;
    }

    protected override void NullifyBusinessId()
    {
        LeaveTypeId = null;
    }
}
