using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AidlyErp.Domain.Common;

namespace AidlyErp.Domain.Hrm;

[Table("hrm_leave_application")]
public class HrmLeaveApplication : AuditEntity, IBranchScopedEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("leave_application_no")]
    public long LeaveApplicationNo { get; set; }

    /// <summary>Alias — the schema column is <c>leave_application_no</c>.</summary>
    [NotMapped]
    public long ApplicationNo { get => LeaveApplicationNo; set => LeaveApplicationNo = value; }

    [Column("employee_no")]
    public long EmployeeNo { get; set; }

    [Column("leave_type_no")]
    public long LeaveTypeNo { get; set; }

    [Column("from_date")]
    public DateTime FromDate { get; set; }

    [Column("to_date")]
    public DateTime ToDate { get; set; }

    [Column("total_days")]
    public decimal TotalDays { get; set; }

    [Column("is_half_day")]
    public short IsHalfDay { get; set; } = 0;

    [Column("leave_year")]
    public int LeaveYear { get; set; }

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

    /// <summary>Not a column — the Java HRM schema is branch-scoped; company is reached
    /// via the branch. Mapping it made every HRM query select a non-existent column.</summary>
    [NotMapped]
    public long? CompanyNo { get; set; }

    [Column("reliever_employee_no")]
    public long? RelieverEmployeeNo { get; set; }

    [Column("attachment_path")]
    [StringLength(500)]
    public string? AttachmentPath { get; set; }

    /// <summary>Approver's reason — mandatory on rejection ("Leave Rejection Reasons Report").</summary>
    [Column("decision_remarks")]
    public string? DecisionRemarks { get; set; }

    /// <summary>Alias — the schema column is <c>decision_remarks</c>.</summary>
    [NotMapped]
    public string? RejectionReason { get => DecisionRemarks; set => DecisionRemarks = value; }
}
