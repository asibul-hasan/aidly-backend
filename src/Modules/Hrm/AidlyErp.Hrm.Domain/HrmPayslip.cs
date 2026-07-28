using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AidlyErp.Shared.Core;

namespace AidlyErp.Hrm.Domain;

[Table("hrm_payslip")]
public class HrmPayslip : AuditEntity, IBranchScopedEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("payslip_no")]
    public long PayslipNo { get; set; }

    [Column("payroll_run_no")]
    public long PayrollRunNo { get; set; }

    [Column("employee_no")]
    public long EmployeeNo { get; set; }

    [Column("salary_structure_no")]
    public long? SalaryStructureNo { get; set; }

    [Column("designation_no")]
    public long? DesignationNo { get; set; }

    [Column("grade_no")]
    public long? GradeNo { get; set; }

    [Column("grade_step_no")]
    public long? GradeStepNo { get; set; }

    [Column("basic_salary")]
    public decimal BasicSalary { get; set; } = 0;

    [Column("present_days")]
    public decimal PresentDays { get; set; } = 0;

    [Column("absent_days")]
    public decimal AbsentDays { get; set; } = 0;

    [Column("leave_days")]
    public decimal LeaveDays { get; set; } = 0;

    [Column("lwp_days")]
    public decimal LwpDays { get; set; } = 0;

    [Column("payable_days")]
    public decimal PayableDays { get; set; } = 0;

    [Column("ot_hours")]
    public decimal OtHours { get; set; } = 0;

    [Column("ot_amount")]
    public decimal OtAmount { get; set; } = 0;

    [Column("gross_earning")]
    public decimal GrossEarning { get; set; } = 0;

    [Column("total_deduction")]
    public decimal TotalDeduction { get; set; } = 0;

    [Column("taxable_income")]
    public decimal TaxableIncome { get; set; } = 0;

    [Column("employer_contribution")]
    public decimal EmployerContribution { get; set; } = 0;

    [Column("net_pay")]
    public decimal NetPay { get; set; } = 0;

    [Column("pay_method")]
    public short? PayMethod { get; set; }

    [Column("payment_status")]
    public short PaymentStatus { get; set; } = 1;

    [Column("calculation_snapshot", TypeName = "jsonb")]
    public string? CalculationSnapshot { get; set; }

    [Column("branch_no")]
    public long? BranchNo { get; set; }

    /// <summary>Not a column — the Java HRM schema is branch-scoped; company is reached
    /// via the branch. Mapping it made every HRM query select a non-existent column.</summary>
    [NotMapped]
    public long? CompanyNo { get; set; }

    /// <summary>Not a column in this schema — kept so callers and DTOs are unaffected, but never persisted.</summary>
    [NotMapped]
    public DateTime? PayslipDate { get; set; }

    /// <summary>Not a column in this schema — kept so callers and DTOs are unaffected, but never persisted.</summary>
    [NotMapped]
    public decimal? TotalDays { get; set; }

    /// <summary>Not a column in this schema — kept so callers and DTOs are unaffected, but never persisted.</summary>
    [NotMapped]
    public short Status { get; set; } = 1;

    [NotMapped]
    public decimal GrossSalary { get => GrossEarning; set => GrossEarning = value; }
    [NotMapped]
    public decimal TotalDeductions { get => TotalDeduction; set => TotalDeduction = value; }
    [NotMapped]
    public decimal NetSalary { get => NetPay; set => NetPay = value; }
    [NotMapped]
    public decimal TotalEarnings { get => GrossEarning; set => GrossEarning = value; }
    [NotMapped]
    public decimal NetPayable { get => NetPay; set => NetPay = value; }
}

[Table("hrm_payslip_dtl")]
public class HrmPayslipDtl : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("payslip_dtl_no")]
    public long PayslipDtlNo { get; set; }

    [Column("payslip_no")]
    public long PayslipNo { get; set; }

    [Column("component_no")]
    public long ComponentNo { get; set; }

    [Column("component_type")]
    public short ComponentType { get; set; } // 1=Earning, 2=Deduction, 3=EmployerContribution, 4=StatutoryDeduction

    [Column("component_name")]
    [StringLength(100)]
    public string? ComponentName { get; set; }

    [Column("calc_type")]
    public short? CalcType { get; set; }

    [Column("calc_value")]
    public decimal? CalcValue { get; set; }

    [Column("formula_expression")]
    [StringLength(500)]
    public string? FormulaExpression { get; set; }

    [Column("base_amount")]
    public decimal? BaseAmount { get; set; }

    [Column("amount")]
    public decimal Amount { get; set; } = 0;

    [Column("display_order")]
    public int? DisplayOrder { get; set; }

    [Column("affects_net")]
    public short? AffectsNet { get; set; }

    [Column("is_taxable")]
    public short? IsTaxable { get; set; }

    [Column("is_statutory")]
    public short? IsStatutory { get; set; }

    [Column("branch_no")]
    public long? BranchNo { get; set; }

    [NotMapped]
    public short LineType { get => ComponentType; set => ComponentType = value; }
    [NotMapped]
    public decimal? CalculationBase { get => BaseAmount; set => BaseAmount = value; }
    [NotMapped]
    public decimal? Percentage { get => CalcValue; set => CalcValue = value; }
}
