using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AidlyErp.Shared.Core;

namespace AidlyErp.Sys.Domain;

[Table("sys_approval_workflow")]
public class ApprovalWorkflow : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("workflow_no")]
    public long WorkflowNo { get; set; }

    [Column("company_no")]
    public long CompanyNo { get; set; }

    [Column("workflow_name")]
    [StringLength(100)]
    public string WorkflowName { get; set; } = string.Empty;

    [Column("doc_type")]
    [StringLength(50)]
    public string DocType { get; set; } = string.Empty;
}

[Table("sys_approval_step")]
public class ApprovalStep : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("step_no")]
    public long StepNo { get; set; }

    [Column("scope_no")]
    public long ScopeNo { get; set; }

    [Column("step_number")]
    public short StepNumber { get; set; } = 1;

    [Column("step_name")]
    [StringLength(150)]
    public string? StepName { get; set; }

    [Column("step_type")]
    public short StepType { get; set; } = 1;

    [Column("next_step_no")]
    public short? NextStepNo { get; set; }

    [Column("is_final")]
    public short IsFinal { get; set; } = 0;

    // Backward-compat alias for existing code referencing WorkflowNo
    [NotMapped]
    public long WorkflowNo { get => ScopeNo; set => ScopeNo = value; }
}

/// <summary>
/// An approver on a workflow step (<c>sys_approval_step_approver</c>). Approvers are identified
/// by <b>employee</b>, not by user or role — the Java schema has only <c>emp_no</c> here.
/// </summary>
[Table("sys_approval_step_approver")]
public class StepApprover : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("approver_no")]
    public long ApproverNo { get; set; }

    [Column("step_no")]
    public long StepNo { get; set; }

    [Column("emp_no")]
    public long? EmpNo { get; set; }
}

[Table("sys_approval_scope")]
public class ApprovalScope : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("scope_no")]
    public long ScopeNo { get; set; }

    [Column("workflow_name")]
    [StringLength(200)]
    public string? WorkflowName { get; set; }

    [Column("company_no")]
    public long CompanyNo { get; set; }

    [Column("branch_no")]
    public long? BranchNo { get; set; }

    [Column("department_no")]
    public long? DepartmentNo { get; set; }

    [Column("menu_no")]
    public long MenuNo { get; set; }
}

[Table("sys_approval_request")]
public class ApprovalRequest : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("approval_request_no")]
    public long ApprovalRequestNo { get; set; }

    [Column("company_no")]
    public long CompanyNo { get; set; }

    [Column("branch_no")]
    public long? BranchNo { get; set; }

    [Column("document_type")]
    [StringLength(30)]
    public string DocumentType { get; set; } = string.Empty;

    [Column("document_no")]
    [StringLength(40)]
    public string DocumentNo { get; set; } = string.Empty;

    [Column("document_pk")]
    public long? DocumentPk { get; set; }

    [Column("amount")]
    public decimal Amount { get; set; }

    [Column("scope_no")]
    public long? ScopeNo { get; set; }

    [Column("current_step")]
    public short CurrentStep { get; set; } = 1;

    /// <summary>1=Pending, 2=Approved, 3=Rejected, 4=Cancelled.</summary>
    [Column("status")]
    public short Status { get; set; } = 1;

    [Column("requested_by")]
    public long RequestedBy { get; set; }

    [Column("requested_at")]
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    [Column("completed_at")]
    public DateTime? CompletedAt { get; set; }

    // Backward-compat aliases
    [NotMapped]
    public string DocType { get => DocumentType; set => DocumentType = value; }

    [NotMapped]
    public long RequestNo { get => ApprovalRequestNo; set => ApprovalRequestNo = value; }
}

[Table("sys_approval_request_step")]
public class ApprovalRequestStep
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("approval_step_no")]
    public long ApprovalStepNo { get; set; }

    [Column("approval_request_no")]
    public long ApprovalRequestNo { get; set; }

    [Column("step_number")]
    public short StepNumber { get; set; }

    /// <summary>
    /// The approver, copied from the step's configured <c>sys_approval_step_approver</c> row.
    /// Nullable to match the Java <c>Long</c> — a step can be configured without an employee.
    /// </summary>
    [Column("emp_no")]
    public long? EmpNo { get; set; }

    /// <summary>1=Pending, 2=Approved, 3=Rejected.</summary>
    [Column("action")]
    public short Action { get; set; } = 1;

    [Column("acted_by")]
    public long? ActedBy { get; set; }

    [Column("acted_at")]
    public DateTime? ActedAt { get; set; }

    [Column("remarks")]
    [StringLength(250)]
    public string? Remarks { get; set; }

    [ConcurrencyCheck]
    [Column("row_version")]
    public long RowVersion { get; set; } = 1L;
}

[Table("sys_login_attempt")]
public class LoginAttempt
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("login_attempt_no")]
    public long LoginAttemptNo { get; set; }

    [Column("user_id")]
    [StringLength(50)]
    public string UserId { get; set; } = string.Empty;

    [Column("ip_address")]
    [StringLength(45)]
    public string? IpAddress { get; set; }

    [Column("is_success")]
    public short IsSuccess { get; set; } = 0;

    [Column("fail_reason")]
    [StringLength(60)]
    public string? FailReason { get; set; }

    [Column("attempted_at")]
    public DateTime AttemptedAt { get; set; } = DateTime.UtcNow;

    // --- Aliases retained for callers using the earlier .NET names ---

    [NotMapped]
    public long AttemptNo { get => LoginAttemptNo; set => LoginAttemptNo = value; }

    [NotMapped]
    public string Username { get => UserId; set => UserId = value; }

    [NotMapped]
    public DateTime AttemptTime { get => AttemptedAt; set => AttemptedAt = value; }
}

[Table("sys_platform_admin")]
public class PlatformAdmin : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("admin_no")]
    public long AdminNo { get; set; }

    [Column("username")]
    [StringLength(100)]
    public string Username { get; set; } = string.Empty;

    [Column("password_hash")]
    [StringLength(255)]
    public string PasswordHash { get; set; } = string.Empty;
}

[Table("sys_role_branch")]
public class SysRoleBranch : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("role_branch_no")]
    public long RoleBranchNo { get; set; }

    [Column("role_no")]
    public long RoleNo { get; set; }

    /// <summary>
    /// Branch the role template is published to. <c>null</c> means <b>all branches</b> — the
    /// role is company-wide.
    /// </summary>
    [Column("branch_no")]
    public long? BranchNo { get; set; }
}

/// <summary>
/// Extra company access for a user (<c>sys_user_company</c>) — lets owners of sister concerns
/// and shared head-office staff switch companies. Unique per (user, company).
/// </summary>
[Table("sys_user_company")]
public class UserCompany : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("user_company_no")]
    public long UserCompanyNo { get; set; }

    [Column("user_no")]
    public long UserNo { get; set; }

    [Column("company_no")]
    public long CompanyNo { get; set; }

    /// <summary>The company selected on login. At most one per user.</summary>
    [Column("is_default")]
    public short IsDefault { get; set; } = 0;

    /// <summary>Marks the user as an owner of this company, not merely a member.</summary>
    [Column("is_owner")]
    public short IsOwner { get; set; } = 0;

    /// <summary>Alias — the schema column is <c>user_company_no</c>.</summary>
    [NotMapped]
    public long UserCompNo { get => UserCompanyNo; set => UserCompanyNo = value; }
}

[Table("sys_user_warehouse")]
public class UserWarehouse : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("user_wh_no")]
    public long UserWhNo { get; set; }

    [Column("user_no")]
    public long UserNo { get; set; }

    [Column("warehouse_no")]
    public long WarehouseNo { get; set; }
}

[Table("sys_department")]
public class Department : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("department_no")]
    public long DepartmentNo { get; set; }

    [Column("company_no")]
    public long CompanyNo { get; set; }

    [Column("branch_no")]
    public long? BranchNo { get; set; }

    [Column("department_name")]
    [StringLength(100)]
    public string DepartmentName { get; set; } = string.Empty;
}
