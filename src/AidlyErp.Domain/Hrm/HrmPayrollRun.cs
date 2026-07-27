using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AidlyErp.Domain.Common;

namespace AidlyErp.Domain.Hrm;

[Table("hrm_payroll_run")]
public class HrmPayrollRun : AuditEntity, IBranchScopedEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("payroll_run_no")]
    public long PayrollRunNo { get; set; }

    [Column("payroll_run_id")]
    [StringLength(30)]
    public string? PayrollRunId { get; set; }

    [Column("pay_period")]
    [StringLength(20)]
    public string PayPeriod { get; set; } = string.Empty;

    [Column("run_type")]
    public short RunType { get; set; } = 1;

    [Column("period_start")]
    public DateTime PeriodStart { get; set; }

    [Column("period_end")]
    public DateTime PeriodEnd { get; set; }

    [Column("bonus_run_no")]
    public long? BonusRunNo { get; set; }

    [Column("status")]
    public short Status { get; set; } = 1;

    [Column("total_gross")]
    public decimal TotalGross { get; set; } = 0;

    [Column("total_deduction")]
    public decimal TotalDeduction { get; set; } = 0;

    [Column("total_net")]
    public decimal TotalNet { get; set; } = 0;

    [Column("calculation_policy_no")]
    public long? CalculationPolicyNo { get; set; }

    [Column("employee_count")]
    public int EmployeeCount { get; set; } = 0;

    [Column("calculation_started_at")]
    public DateTime? CalculationStartedAt { get; set; }

    [Column("calculation_completed_at")]
    public DateTime? CalculationCompletedAt { get; set; }

    [Column("approved_by")]
    public long? ApprovedBy { get; set; }

    [Column("approved_at")]
    public DateTime? ApprovedAt { get; set; }

    [Column("approval_request_no")]
    public long? ApprovalRequestNo { get; set; }

    [Column("posted_event_no")]
    public long? PostedEventNo { get; set; }

    [Column("paid_at")]
    public DateTime? PaidAt { get; set; }

    [Column("paid_by")]
    public long? PaidBy { get; set; }

    [Column("remarks")]
    public string? Remarks { get; set; }

    [Column("branch_no")]
    public long? BranchNo { get; set; }

    /// <summary>Not a column — the Java HRM schema is branch-scoped; company is reached
    /// via the branch. Mapping it made every HRM query select a non-existent column.</summary>
    [NotMapped]
    public long? CompanyNo { get; set; }

    [Column("run_date")]
    public DateTime? RunDate { get; set; }

    [NotMapped]
    public string? RunCode { get => PayrollRunId; set => PayrollRunId = value; }

    [NotMapped]
    public int Year { get => PeriodStart != default ? PeriodStart.Year : DateTime.UtcNow.Year; set { } }

    [NotMapped]
    public int Month { get => PeriodStart != default ? PeriodStart.Month : DateTime.UtcNow.Month; set { } }

    [NotMapped]
    public int TotalEmployees { get => EmployeeCount; set => EmployeeCount = value; }

    [NotMapped]
    public string PeriodName { get => PayPeriod; set => PayPeriod = value; }

    [NotMapped]
    public DateTime StartDate { get => PeriodStart; set => PeriodStart = value; }

    [NotMapped]
    public DateTime EndDate { get => PeriodEnd; set => PeriodEnd = value; }

    [NotMapped]
    public decimal TotalEarnings { get => TotalGross; set => TotalGross = value; }

    [NotMapped]
    public decimal TotalDeductions { get => TotalDeduction; set => TotalDeduction = value; }

    [NotMapped]
    public decimal TotalNetPayable { get => TotalNet; set => TotalNet = value; }
}
