using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AidlyErp.Shared.Core;

namespace AidlyErp.Hrm.Domain;

[Table("hrm_bonus_run")]
public class HrmBonusRun : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("bonus_run_no")]
    public long BonusRunNo { get; set; }

    /// <summary>Not a column — the Java HRM schema is branch-scoped; company is reached
    /// via the branch. Mapping it made every HRM query select a non-existent column.</summary>
    [NotMapped]
    public long? CompanyNo { get; set; }

    [Column("branch_no")]
    public long BranchNo { get; set; }

    [Column("bonus_name")]
    [StringLength(100)]
    public string BonusTitle { get; set; } = string.Empty;

    [Column("bonus_id")]
    [StringLength(30)]
    public string? BonusId { get; set; }

    [Column("bonus_type")]
    public short? BonusType { get; set; }

    [Column("bonus_date")]
    public DateTime? BonusDate { get; set; }

    [Column("bonus_period")]
    [StringLength(60)]
    public string PayPeriod { get; set; } = string.Empty;

    [Column("calculation_type")]
    public short? CalculationType { get; set; }

    [Column("applicability_type")]
    public short? ApplicabilityType { get; set; }

    [Column("percentage_of_basic", TypeName = "numeric(9,4)")]
    public decimal? PercentageOfBasic { get; set; }

    [Column("fixed_amount", TypeName = "numeric(20,4)")]
    public decimal? FixedAmount { get; set; }

    [Column("total_amount")]
    public decimal TotalAmount { get; set; } = 0m;

    [Column("employee_count")]
    public int EmployeeCount { get; set; } = 0;

    [Column("status")]
    public short Status { get; set; } = 1;

    [Column("approval_request_no")]
    public long? ApprovalRequestNo { get; set; }

    [Column("approved_by")]
    public long? ApprovedBy { get; set; }

    [Column("approved_at")]
    public DateTime? ApprovedAt { get; set; }

    [NotMapped]
    public string BonusName { get => BonusTitle; set => BonusTitle = value; }

    [NotMapped]
    public List<long> SelectedDesignationNos { get; set; } = new();

    [NotMapped]
    public List<long> SelectedEmployeeNos { get; set; } = new();

    protected override void NullifyBusinessId() => BonusId = null;
}

[Table("hrm_bonus_line")]
public class HrmBonusLine : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("bonus_line_no")]
    public long BonusLineNo { get; set; }

    [Column("bonus_run_no")]
    public long BonusRunNo { get; set; }

    [Column("employee_no")]
    public long EmployeeNo { get; set; }

    [Column("base_salary", TypeName = "numeric(20,4)")]
    public decimal BaseSalary { get; set; }

    [Column("bonus_amount", TypeName = "numeric(20,4)")]
    public decimal BonusAmount { get; set; }

    [Column("branch_no")]
    public long? BranchNo { get; set; }

    [NotMapped]
    public decimal Amount { get => BonusAmount; set => BonusAmount = value; }
}

[Table("hrm_bonus_scope_designation")]
public class HrmBonusScopeDesignation : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("bonus_scope_designation_no")]
    public long ScopeDesigNo { get; set; }

    [Column("bonus_run_no")]
    public long BonusRunNo { get; set; }

    [Column("designation_no")]
    public long DesignationNo { get; set; }

    [Column("branch_no")]
    public long? BranchNo { get; set; }
}

[Table("hrm_bonus_scope_employee")]
public class HrmBonusScopeEmployee : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("bonus_scope_employee_no")]
    public long ScopeEmpNo { get; set; }

    [Column("bonus_run_no")]
    public long BonusRunNo { get; set; }

    [Column("employee_no")]
    public long EmployeeNo { get; set; }

    [Column("branch_no")]
    public long? BranchNo { get; set; }
}

[Table("hrm_loan_advance")]
public class HrmLoanAdvance : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("loan_no")]
    public long LoanNo { get; set; }

    [Column("loan_id")]
    [StringLength(30)]
    public string? LoanId { get; set; }

    [Column("employee_no")]
    public long EmployeeNo { get; set; }

    [Column("loan_type")]
    public short? LoanType { get; set; } // 1=Loan, 2=Advance

    [Column("principal_amount")]
    public decimal PrincipalAmount { get; set; } = 0;

    [Column("installment_count")]
    public int InstallmentCount { get; set; } = 1;

    [Column("installment_amount")]
    public decimal InstallmentAmount { get; set; } = 0;

    [Column("recovered_amount")]
    public decimal RecoveredAmount { get; set; } = 0;

    [Column("outstanding_amount")]
    public decimal OutstandingAmount { get; set; } = 0;

    [Column("disbursement_date")]
    public DateTime? DisbursementDate { get; set; }

    [Column("recovery_start")]
    public DateTime? RecoveryStart { get; set; }

    [Column("reason")]
    public string? Reason { get; set; }

    [Column("status")]
    public short Status { get; set; } = 1; // 1=Draft, 2=Approved, 3=Disbursed, 4=Closed, 5=Cancelled

    [Column("approved_by")]
    public long? ApprovedBy { get; set; }

    [Column("approved_at")]
    public DateTime? ApprovedAt { get; set; }

    [Column("approval_request_no")]
    public long? ApprovalRequestNo { get; set; }

    [Column("branch_no")]
    public long? BranchNo { get; set; }

    [NotMapped]
    public decimal MonthlyInstallment { get => InstallmentAmount; set => InstallmentAmount = value; }
    [NotMapped]
    public decimal RemainingAmount { get => OutstandingAmount; set => OutstandingAmount = value; }
    [NotMapped]
    public decimal PaidAmount { get => RecoveredAmount; set => RecoveredAmount = value; }
}

[Table("hrm_payroll_policy")]
public class HrmPayrollPolicy : AuditEntity
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

    /// <summary>Not a column in this schema — kept so callers and DTOs are unaffected, but never persisted.</summary>
    [NotMapped]
    public decimal OvertimeRate { get; set; } = 1.5m;
}

[Table("hrm_salary_structure")]
public class HrmSalaryStructure : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("salary_structure_no")]
    public long SalaryStructureNo { get; set; }

    [Column("employee_no")]
    public long EmployeeNo { get; set; }

    [Column("designation_no")]
    public long? DesignationNo { get; set; }

    [Column("grade_no")]
    public long? GradeNo { get; set; }

    [Column("grade_step_no")]
    public long? GradeStepNo { get; set; }

    [Column("effective_from")]
    public DateTime EffectiveFrom { get; set; }

    [Column("effective_to")]
    public DateTime? EffectiveTo { get; set; }

    [Column("basic_salary")]
    public decimal BasicSalary { get; set; } = 0;

    [Column("gross_salary")]
    public decimal GrossSalary { get; set; } = 0;

    [Column("status")]
    public short Status { get; set; } = 1; // 1=Active, 2=Superseded, 3=Draft

    [Column("branch_no")]
    public long? BranchNo { get; set; }

    [Column("revision_reason")]
    [StringLength(120)]
    public string? RevisionReason { get; set; }

    [Column("remarks")]
    public string? Remarks { get; set; }

    [NotMapped]
    public long StructureNo { get => SalaryStructureNo; set => SalaryStructureNo = value; }
    [NotMapped]
    public new short IsActive { get => Status; set => Status = value; }
}

[Table("hrm_salary_structure_dtl")]
public class HrmSalaryStructureDtl : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("structure_dtl_no")]
    public long StructureDtlNo { get; set; }

    [Column("salary_structure_no")]
    public long SalaryStructureNo { get; set; }

    [Column("component_no")]
    public long ComponentNo { get; set; }

    [Column("calc_type")]
    public short? CalcType { get; set; }

    [Column("calc_value")]
    public decimal? CalcValue { get; set; }

    [Column("formula_expression")]
    [StringLength(500)]
    public string? FormulaExpression { get; set; }

    [Column("is_proratable")]
    public short? IsProratable { get; set; } = 1;

    [Column("amount")]
    public decimal Amount { get; set; } = 0;

    [Column("display_order")]
    public int? DisplayOrder { get; set; }

    [Column("branch_no")]
    public long? BranchNo { get; set; }

    [NotMapped]
    public long StructureNo { get => SalaryStructureNo; set => SalaryStructureNo = value; }
    [NotMapped]
    public decimal? Percentage { get => CalcValue; set => CalcValue = value; }
    [NotMapped]
    public decimal? FixedAmount { get => Amount; set => Amount = value ?? 0; }
    [NotMapped]
    public decimal? CalculationBase { get => Amount; set => Amount = value ?? 0; }
}

[Table("hrm_tax_slab")]
public class HrmTaxSlab : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("tax_slab_no")]
    public long TaxSlabNo { get; set; }

    [Column("country_code")]
    [StringLength(2)]
    public string CountryCode { get; set; } = "BD";

    [Column("fiscal_year")]
    [StringLength(9)]
    public string FiscalYear { get; set; } = string.Empty;

    [Column("taxpayer_class")]
    [StringLength(30)]
    public string TaxpayerClass { get; set; } = "GENERAL";

    [Column("slab_order")]
    public int SlabOrder { get; set; }

    [Column("from_amount", TypeName = "numeric(20,4)")]
    public decimal FromAmount { get; set; } = 0m;

    [Column("to_amount", TypeName = "numeric(20,4)")]
    public decimal? ToAmount { get; set; }

    [Column("rate_percent", TypeName = "numeric(9,4)")]
    public decimal RatePercent { get; set; } = 0m;

    [Column("fixed_amount", TypeName = "numeric(20,4)")]
    public decimal FixedAmount { get; set; } = 0m;

    [NotMapped]
    public decimal MinIncome { get => FromAmount; set => FromAmount = value; }

    [NotMapped]
    public decimal? MaxIncome { get => ToAmount; set => ToAmount = value; }

    [NotMapped]
    public decimal TaxRate { get => RatePercent; set => RatePercent = value; }

    [NotMapped]
    public decimal MinAmount { get => FromAmount; set => FromAmount = value; }

    [NotMapped]
    public decimal? MaxAmount { get => ToAmount; set => ToAmount = value; }
}
