using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AidlyErp.Shared.Core;

namespace AidlyErp.Hrm.Domain;

[Table("hrm_candidate")]
public class HrmCandidate : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("candidate_no")]
    public long CandidateNo { get; set; }

    [Column("candidate_id")]
    [StringLength(30)]
    public string CandidateId { get; set; } = string.Empty;

    [Column("full_name")]
    [StringLength(150)]
    public string FullName { get; set; } = string.Empty;

    [Column("email")]
    [StringLength(150)]
    public string? Email { get; set; }

    [Column("mobile_number")]
    [StringLength(20)]
    public string MobileNumber { get; set; } = string.Empty;

    [Column("source")]
    public short? Source { get; set; }

    [Column("requisition_no")]
    public long? RequisitionNo { get; set; }

    /// <summary>1 = applied, and onward through the recruitment pipeline.</summary>
    [Column("application_status")]
    public short ApplicationStatus { get; set; } = 1;

    [Column("resume_file_no")]
    public long? ResumeFileNo { get; set; }

    [Column("branch_no")]
    public long? BranchNo { get; set; }

    [Column("remarks")]
    public string? Remarks { get; set; }

    /// <summary>Alias — the schema column is <c>full_name</c>.</summary>
    [NotMapped]
    public string CandidateName { get => FullName; set => FullName = value; }

    /// <summary>Alias — the schema column is <c>application_status</c>.</summary>
    [NotMapped]
    public short Status { get => ApplicationStatus; set => ApplicationStatus = value; }

    /// <summary>Alias — the schema column is <c>mobile_number</c>.</summary>
    [NotMapped]
    public string? Phone { get => MobileNumber; set => MobileNumber = value ?? string.Empty; }

    protected override void NullifyBusinessId() => CandidateId = null!;
}

[Table("hrm_job_requisition")]
public class HrmJobRequisition : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("requisition_no")]
    public long RequisitionNo { get; set; }

    [Column("requisition_id")]
    [StringLength(30)]
    public string RequisitionId { get; set; } = string.Empty;

    [Column("department_no")]
    public long DepartmentNo { get; set; }

    [Column("designation_no")]
    public long DesignationNo { get; set; }

    [Column("grade_no")]
    public long? GradeNo { get; set; }

    [Column("no_of_vacancies")]
    public int NoOfVacancies { get; set; } = 1;

    [Column("employment_type")]
    public short? EmploymentType { get; set; }

    [Column("budget_min", TypeName = "numeric(20,4)")]
    public decimal? BudgetMin { get; set; }

    [Column("budget_max", TypeName = "numeric(20,4)")]
    public decimal? BudgetMax { get; set; }

    [Column("job_description")]
    public string? JobDescription { get; set; }

    [Column("required_by_date")]
    public DateOnly? RequiredByDate { get; set; }

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

    /// <summary>Alias — the schema column is <c>no_of_vacancies</c>.</summary>
    [NotMapped]
    public int NoOfPositions { get => NoOfVacancies; set => NoOfVacancies = value; }

    /// <summary>Alias — the schema column is <c>no_of_vacancies</c>.</summary>
    [NotMapped]
    public int Vacancies { get => NoOfVacancies; set => NoOfVacancies = value; }

    /// <summary>
    /// Title of the position. The Java schema carries this on the linked designation, so it is
    /// unmapped here; populate it from <c>designation_no</c> when projecting to a DTO.
    /// </summary>
    [NotMapped]
    public string? PositionTitle { get; set; }

    /// <summary>Alias — the schema column is <c>job_description</c>.</summary>
    [NotMapped]
    public string? Remarks { get => JobDescription; set => JobDescription = value; }

    protected override void NullifyBusinessId() => RequisitionId = null!;
}

[Table("hrm_offer")]
public class HrmOffer : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("offer_no")]
    public long OfferNo { get; set; }

    [Column("offer_id")]
    [StringLength(30)]
    public string OfferId { get; set; } = string.Empty;

    [Column("candidate_no")]
    public long CandidateNo { get; set; }

    [Column("designation_no")]
    public long? DesignationNo { get; set; }

    [Column("grade_no")]
    public long? GradeNo { get; set; }

    [Column("offered_salary", TypeName = "numeric(20,4)")]
    public decimal OfferedSalary { get; set; }

    [Column("joining_date")]
    public DateOnly JoiningDate { get; set; }

    [Column("status")]
    public short Status { get; set; } = 1;

    [Column("offer_file_no")]
    public long? OfferFileNo { get; set; }

    [Column("onboarded_employee_no")]
    public long? OnboardedEmployeeNo { get; set; }

    [Column("notes")]
    public string? Notes { get; set; }

    [Column("branch_no")]
    public long? BranchNo { get; set; }

    /// <summary>Alias — the schema column is <c>notes</c>.</summary>
    [NotMapped]
    public string? Remarks { get => Notes; set => Notes = value; }

    /// <summary>
    /// Department of the offered position. Derived from the requisition/designation in the Java
    /// schema rather than stored on the offer, so it is unmapped.
    /// </summary>
    [NotMapped]
    public long? DepartmentNo { get; set; }

    /// <summary>
    /// Date the offer was issued. Not a column in the Java schema — <c>created_at</c> serves that
    /// purpose; exposed here so callers reading an "offer date" get a sensible value.
    /// </summary>
    [NotMapped]
    public DateTime OfferDate
    {
        get => _offerDate ?? CreatedAt ?? DateTime.MinValue;
        set => _offerDate = value;
    }

    private DateTime? _offerDate;

    protected override void NullifyBusinessId() => OfferId = null!;
}

[Table("hrm_employee_movement")]
public class HrmEmployeeMovement : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("movement_no")]
    public long MovementNo { get; set; }

    [Column("movement_id")]
    [StringLength(30)]
    public string MovementId { get; set; } = string.Empty;

    [Column("employee_no")]
    public long EmployeeNo { get; set; }

    /// <summary>Numeric movement-type code (promotion, transfer, …), per the Java schema.</summary>
    [Column("movement_type")]
    public short MovementType { get; set; }

    [Column("effective_date")]
    public DateOnly EffectiveDate { get; set; }

    [Column("from_designation_no")]
    public long? FromDesignationNo { get; set; }

    [Column("from_grade_no")]
    public long? FromGradeNo { get; set; }

    [Column("from_department_no")]
    public long? FromDepartmentNo { get; set; }

    [Column("from_branch_no")]
    public long? FromBranchNo { get; set; }

    [Column("to_designation_no")]
    public long? ToDesignationNo { get; set; }

    [Column("to_grade_no")]
    public long? ToGradeNo { get; set; }

    [Column("to_department_no")]
    public long? ToDepartmentNo { get; set; }

    [Column("to_branch_no")]
    public long? ToBranchNo { get; set; }

    [Column("new_salary", TypeName = "numeric(20,4)")]
    public decimal? NewSalary { get; set; }

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

    // --- "New*" aliases: the Java columns are named to_* ---

    [NotMapped]
    public long? NewDesignationNo { get => ToDesignationNo; set => ToDesignationNo = value; }

    [NotMapped]
    public long? NewGradeNo { get => ToGradeNo; set => ToGradeNo = value; }

    [NotMapped]
    public long? NewDepartmentNo { get => ToDepartmentNo; set => ToDepartmentNo = value; }

    [NotMapped]
    public long? NewBranchNo { get => ToBranchNo; set => ToBranchNo = value; }

    protected override void NullifyBusinessId() => MovementId = null!;
}

[Table("hrm_final_settlement")]
public class HrmFinalSettlement : AuditEntity, IBranchScopedEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("settlement_no")]
    public long SettlementNo { get; set; }

    [Column("settlement_id")]
    [StringLength(30)]
    public string? SettlementId { get; set; }

    [Column("employee_no")]
    public long EmployeeNo { get; set; }

    [Column("separation_type")]
    public short? SeparationType { get; set; }

    [Column("last_working_day")]
    public DateOnly? LastWorkingDay { get; set; }

    [Column("last_salary", TypeName = "numeric(20,4)")]
    public decimal? LastSalary { get; set; } = 0m;

    [Column("leave_encashment", TypeName = "numeric(20,4)")]
    public decimal? LeaveEncashment { get; set; } = 0m;

    [Column("gratuity", TypeName = "numeric(20,4)")]
    public decimal? Gratuity { get; set; } = 0m;

    [Column("bonus_payable", TypeName = "numeric(20,4)")]
    public decimal? BonusPayable { get; set; } = 0m;

    [Column("loan_recovery", TypeName = "numeric(20,4)")]
    public decimal? LoanRecovery { get; set; } = 0m;

    [Column("other_deduction", TypeName = "numeric(20,4)")]
    public decimal? OtherDeduction { get; set; } = 0m;

    [Column("net_payable", TypeName = "numeric(20,4)")]
    public decimal NetPayable { get; set; } = 0m;

    [Column("remarks")]
    public string? Remarks { get; set; }

    [Column("status")]
    public short Status { get; set; } = 1;

    [Column("approved_by")]
    public long? ApprovedBy { get; set; }

    [Column("approved_at")]
    public DateTime? ApprovedAt { get; set; }

    [Column("approval_request_no")]
    public long? ApprovalRequestNo { get; set; }

    /// <summary>Not a column in this schema — kept so callers and DTOs are unaffected, but never persisted.</summary>
    [NotMapped]
    public DateTime? PaidAt { get; set; }

    /// <summary>Not a column in this schema — kept so callers and DTOs are unaffected, but never persisted.</summary>
    [NotMapped]
    public long? PaidBy { get; set; }

    [Column("branch_no")]
    public long? BranchNo { get; set; }

    /// <summary>Not a column — the Java HRM schema is branch-scoped; company is reached
    /// via the branch. Mapping it made every HRM query select a non-existent column.</summary>
    [NotMapped]
    public long? CompanyNo { get; set; }

    [NotMapped]
    public decimal NetSettlement { get => NetPayable; set => NetPayable = value; }

    protected override void NullifyBusinessId() => SettlementId = null;
}

[Table("hrm_grade_step")]
public class HrmGradeStep : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("grade_step_no")]
    public long GradeStepNo { get; set; }

    [Column("grade_no")]
    public long GradeNo { get; set; }

    /// <summary>Java calls this column <c>step</c>, not <c>step_name</c>.</summary>
    [Column("step")]
    [StringLength(50)]
    public string StepName { get; set; } = string.Empty;

    /// <summary>Java calls this column <c>amount</c>. <c>basic_salary</c> is a real column on
    /// <c>hrm_payslip</c>, but not on this table.</summary>
    [Column("amount")]
    public decimal BasicSalary { get; set; }
}
