using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AidlyErp.Shared.Core;

namespace AidlyErp.Hrm.Domain;

[Table("hrm_attendance_adjustment")]
public class HrmAttendanceAdjustment : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("adjustment_no")]
    public long AdjustmentNo { get; set; }

    [Column("employee_no")]
    public long EmployeeNo { get; set; }

    [Column("att_date")]
    public DateOnly AttDate { get; set; }

    /// <summary>Attendance status being requested (numeric code, same domain as <c>HrmAttendance.Status</c>).</summary>
    [Column("requested_status")]
    public short RequestedStatus { get; set; }

    [Column("requested_in")]
    public TimeOnly? RequestedIn { get; set; }

    [Column("requested_out")]
    public TimeOnly? RequestedOut { get; set; }

    [Column("reason")]
    public string? Reason { get; set; }

    /// <summary>1 = pending, and onward through the approval workflow.</summary>
    [Column("status")]
    public short Status { get; set; } = 1;

    [Column("approval_request_no")]
    public long? ApprovalRequestNo { get; set; }

    [Column("approved_by")]
    public long? ApprovedBy { get; set; }

    [Column("approved_at")]
    public DateTime? ApprovedAt { get; set; }

    [Column("branch_no")]
    public long? BranchNo { get; set; }

    // --- Aliases matching the wording used by the adjustment service ---

    [NotMapped]
    public short NewStatus { get => RequestedStatus; set => RequestedStatus = value; }

    [NotMapped]
    public TimeOnly? NewInTime { get => RequestedIn; set => RequestedIn = value; }

    [NotMapped]
    public TimeOnly? NewOutTime { get => RequestedOut; set => RequestedOut = value; }

    /// <summary>
    /// The attendance row this adjustment targets. Not a stored column — the Java schema keys the
    /// adjustment by (employee_no, att_date), so this resolves through those two fields.
    /// </summary>
    [NotMapped]
    public long? AttendanceNo { get; set; }
}

[Table("hrm_leave_balance")]
public class HrmLeaveBalance : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("leave_balance_no")]
    public long LeaveBalanceNo { get; set; }

    [Column("employee_no")]
    public long EmployeeNo { get; set; }

    [Column("leave_type_no")]
    public long LeaveTypeNo { get; set; }

    [Column("leave_year")]
    public int LeaveYear { get; set; }

    [Column("opening_balance", TypeName = "numeric(18,4)")]
    public decimal? OpeningBalance { get; set; }

    [Column("entitled_days", TypeName = "numeric(18,4)")]
    public decimal EntitledDays { get; set; } = 0m;

    [Column("accrued_days", TypeName = "numeric(18,4)")]
    public decimal AccruedDays { get; set; } = 0m;

    [Column("consumed_days", TypeName = "numeric(18,4)")]
    public decimal ConsumedDays { get; set; } = 0m;

    [Column("pending_days", TypeName = "numeric(18,4)")]
    public decimal PendingDays { get; set; } = 0m;

    [Column("encashed_days", TypeName = "numeric(18,4)")]
    public decimal EncashedDays { get; set; } = 0m;

    [Column("lapsed_days", TypeName = "numeric(18,4)")]
    public decimal LapsedDays { get; set; } = 0m;

    [Column("carried_forward", TypeName = "numeric(18,4)")]
    public decimal CarriedForward { get; set; } = 0m;

    [Column("branch_no")]
    public long? BranchNo { get; set; }

    /// <summary>
    /// Days still available to take. Mirrors Java's <c>@Transient getAvailableDays()</c>:
    /// opening + accrued + carried-forward − consumed − pending − encashed − lapsed.
    /// </summary>
    [NotMapped]
    public decimal AvailableDays =>
        (OpeningBalance ?? 0m) + AccruedDays + CarriedForward
        - ConsumedDays - PendingDays - EncashedDays - LapsedDays;

    // --- Aliases ---

    /// <summary>Alias — the schema column is <c>leave_balance_no</c>.</summary>
    [NotMapped]
    public long BalanceNo { get => LeaveBalanceNo; set => LeaveBalanceNo = value; }

    /// <summary>Alias — the schema column is <c>consumed_days</c>.</summary>
    [NotMapped]
    public decimal UsedDays { get => ConsumedDays; set => ConsumedDays = value; }

    /// <summary>Alias — the schema column is <c>consumed_days</c>.</summary>
    [NotMapped]
    public decimal TakenDays { get => ConsumedDays; set => ConsumedDays = value; }

    /// <summary>Alias — the schema column is <c>leave_year</c>.</summary>
    [NotMapped]
    public int FinYear { get => LeaveYear; set => LeaveYear = value; }

    /// <summary>
    /// Remaining days. Reads through to <see cref="AvailableDays"/>; assigning adjusts
    /// <see cref="EntitledDays"/> so callers that maintain a running balance still work.
    /// </summary>
    [NotMapped]
    public decimal BalanceDays
    {
        get => AvailableDays;
        set => EntitledDays = value + ConsumedDays;
    }
}

[Table("hrm_leave_ledger")]
public class HrmLeaveLedger : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("ledger_no")]
    public long LedgerNo { get; set; }

    [Column("employee_no")]
    public long EmployeeNo { get; set; }

    [Column("leave_type_no")]
    public long LeaveTypeNo { get; set; }

    [Column("trans_date")]
    public DateTime TransDate { get; set; }

    [Column("days")]
    public decimal Days { get; set; }

    [Column("trans_type")]
    [StringLength(30)]
    public string TransType { get; set; } = string.Empty;
}

[Table("hrm_leave_policy_setup")]
public class HrmLeavePolicySetup : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("policy_no")]
    public long PolicyNo { get; set; }

    /// <summary>Not a column — the Java HRM schema is branch-scoped; company is reached
    /// via the branch. Mapping it made every HRM query select a non-existent column.</summary>
    [NotMapped]
    public long? CompanyNo { get; set; }

    [Column("policy_name")]
    [StringLength(100)]
    public string PolicyName { get; set; } = string.Empty;

    [Column("leave_type_no")]
    public long LeaveTypeNo { get; set; }

    [Column("employee_group_no")]
    public long? EmployeeGroupNo { get; set; }

    [Column("accrual_method")]
    public short? AccrualMethod { get; set; }

    [Column("default_days")]
    public decimal DefaultDays { get; set; }

    [Column("work_days_per_leave_day")]
    public int? WorkDaysPerLeaveDay { get; set; }

    [Column("proration_method")]
    public short? ProrationMethod { get; set; }

    [Column("eligibility_service_months")]
    public int? EligibilityServiceMonths { get; set; }

    [Column("is_carry_forward_allowed")]
    public short? IsCarryForwardAllowed { get; set; }

    [Column("max_carry_forward_days")]
    public decimal? MaxCarryForwardDays { get; set; }

    [Column("carry_forward_validity_months")]
    public int? CarryForwardValidityMonths { get; set; }

    [Column("is_encashable")]
    public short? IsEncashable { get; set; }

    [Column("max_encashable_days")]
    public decimal? MaxEncashableDays { get; set; }

    [Column("encashment_formula")]
    [StringLength(100)]
    public string? EncashmentFormula { get; set; }

    [Column("is_lapsable")]
    public short? IsLapsable { get; set; }

    [Column("max_dept_leave_percentage")]
    public decimal? MaxDeptLeavePercentage { get; set; }

    [Column("specialized_config")]
    public string? SpecializedConfig { get; set; }

    [Column("branch_no")]
    public long? BranchNo { get; set; }
}

[Table("hrm_leave_application_rule")]
public class HrmLeaveApplicationRule : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("rule_no")]
    public long RuleNo { get; set; }

    [Column("policy_no")]
    public long PolicyNo { get; set; }

    [Column("leave_type_no")]
    public long LeaveTypeNo { get; set; }

    [Column("min_duration_per_app")]
    public decimal? MinDurationPerApp { get; set; }

    [Column("max_duration_per_app")]
    public decimal? MaxDurationPerApp { get; set; }

    [Column("notice_period_days")]
    public int? NoticePeriodDays { get; set; }

    [Column("max_retroactive_days")]
    public int? MaxRetroactiveDays { get; set; }

    [Column("exclude_weekends")]
    public short? ExcludeWeekends { get; set; }

    [Column("exclude_holidays")]
    public short? ExcludeHolidays { get; set; }

    [Column("allow_negative_balance")]
    public short? AllowNegativeBalance { get; set; }

    [Column("max_overdraft_days")]
    public decimal? MaxOverdraftDays { get; set; }

    [Column("is_shift_critical")]
    public short? IsShiftCritical { get; set; }

    [Column("is_approval_required")]
    public short? IsApprovalRequired { get; set; }

    [Column("is_reliever_mandatory")]
    public short? IsRelieverMandatory { get; set; }

    [Column("attachment_required_after_days")]
    public decimal? AttachmentRequiredAfterDays { get; set; }

    [Column("sla_timeout_hours")]
    public int? SlaTimeoutHours { get; set; }

    [Column("escalation_role_no")]
    public long? EscalationRoleNo { get; set; }

    [Column("branch_no")]
    public long? BranchNo { get; set; }
}

[Table("hrm_overtime")]
public class HrmOvertime : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("overtime_no")]
    public long OvertimeNo { get; set; }

    [Column("overtime_id")]
    [StringLength(30)]
    public string OvertimeId { get; set; } = string.Empty;

    [Column("employee_no")]
    public long EmployeeNo { get; set; }

    [Column("ot_date")]
    public DateOnly OtDate { get; set; }

    [Column("hours", TypeName = "numeric(9,2)")]
    public decimal Hours { get; set; } = 0m;

    [Column("rate_multiplier", TypeName = "numeric(9,2)")]
    public decimal RateMultiplier { get; set; } = 1.5m;

    [Column("hourly_rate", TypeName = "numeric(20,4)")]
    public decimal HourlyRate { get; set; } = 0m;

    [Column("ot_amount", TypeName = "numeric(20,4)")]
    public decimal OtAmount { get; set; } = 0m;

    [Column("reason")]
    public string? Reason { get; set; }

    [Column("status")]
    public short Status { get; set; } = 1;

    [Column("approved_by")]
    public long? ApprovedBy { get; set; }

    [Column("approved_at")]
    public DateTime? ApprovedAt { get; set; }

    [Column("approval_request_no")]
    public long? ApprovalRequestNo { get; set; }

    [Column("branch_no")]
    public long? BranchNo { get; set; }

    /// <summary>Alias — the schema column is <c>hours</c>.</summary>
    [NotMapped]
    public decimal OtHours { get => Hours; set => Hours = value; }

    /// <summary>Alias — the schema column is <c>rate_multiplier</c>.</summary>
    [NotMapped]
    public decimal OtRate { get => RateMultiplier; set => RateMultiplier = value; }

    protected override void NullifyBusinessId() => OvertimeId = null!;
}

[Table("hrm_shift_roster")]
public class HrmShiftRoster : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("roster_no")]
    public long RosterNo { get; set; }

    [Column("roster_id")]
    [StringLength(30)]
    public string RosterId { get; set; } = string.Empty;

    [Column("roster_name")]
    [StringLength(120)]
    public string RosterName { get; set; } = string.Empty;

    [Column("from_date")]
    public DateOnly FromDate { get; set; }

    [Column("to_date")]
    public DateOnly ToDate { get; set; }

    [Column("status")]
    public short Status { get; set; } = 1;

    [Column("branch_no")]
    public long? BranchNo { get; set; }

    /// <summary>Alias — the schema column is <c>from_date</c>.</summary>
    [NotMapped]
    public DateOnly EffectiveFrom { get => FromDate; set => FromDate = value; }

    /// <summary>Alias — the schema column is <c>to_date</c>.</summary>
    [NotMapped]
    public DateOnly EffectiveTo { get => ToDate; set => ToDate = value; }

    /// <summary>
    /// Convenience only. A roster header covers many employees/shifts — the real assignment lives
    /// on <see cref="HrmShiftRosterLine"/>. Kept unmapped so header-level callers still compile.
    /// </summary>
    [NotMapped]
    public long? EmployeeNo { get; set; }

    /// <summary>Convenience only — see <see cref="EmployeeNo"/>.</summary>
    [NotMapped]
    public long? ShiftNo { get; set; }

    /// <summary>Lines passed from the DTO for save operations — not persisted on the header.</summary>
    [NotMapped]
    public List<HrmShiftRosterLine>? Lines { get; set; }

    protected override void NullifyBusinessId() => RosterId = null!;
}

[Table("hrm_shift_roster_line")]
public class HrmShiftRosterLine : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("roster_line_no")]
    public long RosterLineNo { get; set; }

    [Column("roster_no")]
    public long RosterNo { get; set; }

    [Column("employee_no")]
    public long EmployeeNo { get; set; }

    [Column("shift_no")]
    public long ShiftNo { get; set; }

    [Column("weekly_off")]
    public short? WeeklyOff { get; set; }

    [Column("branch_no")]
    public long? BranchNo { get; set; }
}
