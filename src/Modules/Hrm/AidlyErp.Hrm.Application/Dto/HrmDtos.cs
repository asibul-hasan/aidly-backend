using System.Text.Json.Serialization;

namespace AidlyErp.Hrm.Application.Dto;

public class Hrm1003ShiftDto
{
    [JsonPropertyName("shift_no")]
    public long? ShiftNo { get; set; }

    [JsonPropertyName("shift_id")]
    public string? ShiftId { get; set; }

    [JsonPropertyName("shift_name")]
    public string? ShiftName { get; set; }

    [JsonPropertyName("effective_date")]
    public DateOnly? EffectiveDate { get; set; }

    [JsonPropertyName("start_time")]
    public TimeOnly? StartTime { get; set; }

    [JsonPropertyName("end_time")]
    public TimeOnly? EndTime { get; set; }

    [JsonPropertyName("grace_minutes")]
    public int? GraceMinutes { get; set; }

    [JsonPropertyName("half_day_minutes")]
    public int? HalfDayMinutes { get; set; }

    [JsonPropertyName("break_minutes")]
    public int? BreakMinutes { get; set; }

    [JsonPropertyName("weekly_off_mask")]
    public short? WeeklyOffMask { get; set; }

    [JsonPropertyName("is_night_shift")]
    public short? IsNightShift { get; set; }

    [JsonPropertyName("branch_no")]
    public long? BranchNo { get; set; }

    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }

    [JsonPropertyName("is_active")]
    public short? IsActive { get; set; }

    [JsonPropertyName("row_version")]
    public long? RowVersion { get; set; }
}

public class Hrm1004GradeStepDto
{
    [JsonPropertyName("grade_step_no")]
    public long? GradeStepNo { get; set; }

    [JsonPropertyName("grade_no")]
    public long GradeNo { get; set; }

    [JsonPropertyName("step")]
    public string? StepName { get; set; }

    [JsonPropertyName("amount")]
    public decimal? Amount { get; set; }

    [JsonPropertyName("is_active")]
    public short? IsActive { get; set; }

    [JsonPropertyName("row_version")]
    public long? RowVersion { get; set; }
}

public class Hrm1004GradeDto
{
    [JsonPropertyName("grade_no")]
    public long? GradeNo { get; set; }

    [JsonPropertyName("grade_id")]
    public string? GradeId { get; set; }

    [JsonPropertyName("grade_name")]
    public string? GradeName { get; set; }

    [JsonPropertyName("rank_order")]
    public int? RankOrder { get; set; }

    [JsonPropertyName("min_salary")]
    public decimal MinSalary { get; set; }

    [JsonPropertyName("max_salary")]
    public decimal MaxSalary { get; set; }

    [JsonPropertyName("is_overtime_eligible")]
    public short? IsOvertimeEligible { get; set; }

    [JsonPropertyName("branch_no")]
    public long? BranchNo { get; set; }

    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }

    [JsonPropertyName("is_active")]
    public short? IsActive { get; set; }

    [JsonPropertyName("row_version")]
    public long? RowVersion { get; set; }

    [JsonPropertyName("steps")]
    public List<Hrm1004GradeStepDto> Steps { get; set; } = new();
}

public class Hrm1005LeaveTypeDto
{
    [JsonPropertyName("leave_type_no")]
    public long? LeaveTypeNo { get; set; }

    [JsonPropertyName("leave_type_id")]
    public string? LeaveTypeId { get; set; }

    [JsonPropertyName("leave_type_name")]
    public string? LeaveTypeName { get; set; }

    [JsonPropertyName("is_paid")]
    public short? IsPaid { get; set; }

    [JsonPropertyName("statutory_category")]
    public string? StatutoryCategory { get; set; }

    [JsonPropertyName("accrual_method")]
    public short? AccrualMethod { get; set; }

    [JsonPropertyName("requires_approval")]
    public short? RequiresApproval { get; set; }

    [JsonPropertyName("is_encashable")]
    public short? IsEncashable { get; set; }

    [JsonPropertyName("default_days_per_year")]
    public decimal? DefaultDaysPerYear { get; set; }

    [JsonPropertyName("max_carry_forward_days")]
    public decimal? MaxCarryForwardDays { get; set; }

    [JsonPropertyName("is_carry_forward_allowed")]
    public short? IsCarryForwardAllowed { get; set; }

    [JsonPropertyName("eligibility_service_months")]
    public int? EligibilityServiceMonths { get; set; }

    [JsonPropertyName("documentation_required")]
    public short? DocumentationRequired { get; set; }

    [JsonPropertyName("carry_forward_validity_months")]
    public int? CarryForwardValidityMonths { get; set; }

    [JsonPropertyName("is_lapsable")]
    public short? IsLapsable { get; set; }

    [JsonPropertyName("min_duration_days")]
    public decimal? MinDurationDays { get; set; }

    [JsonPropertyName("max_duration_days")]
    public decimal? MaxDurationDays { get; set; }

    [JsonPropertyName("branch_no")]
    public long? BranchNo { get; set; }

    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }

    [JsonPropertyName("is_active")]
    public short? IsActive { get; set; }

    [JsonPropertyName("row_version")]
    public long? RowVersion { get; set; }
}

public class Hrm1006HolidayDto
{
    [JsonPropertyName("holiday_no")]
    public long? HolidayNo { get; set; }

    [JsonPropertyName("holiday_name")]
    public string? HolidayName { get; set; }

    [JsonPropertyName("holiday_date")]
    public DateTime HolidayDate { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("is_active")]
    public short IsActive { get; set; } = 1;
}

public class Hrm1007SalaryComponentDto
{
    [JsonPropertyName("component_no")]
    public long? ComponentNo { get; set; }

    [JsonPropertyName("component_code")]
    public string? ComponentCode { get; set; }

    [JsonPropertyName("component_name")]
    public string? ComponentName { get; set; }

    [JsonPropertyName("component_type")]
    public short ComponentType { get; set; } = 1; // 1=Earning 2=Deduction

    [JsonPropertyName("is_taxable")]
    public short IsTaxable { get; set; } = 1;

    [JsonPropertyName("is_active")]
    public short IsActive { get; set; } = 1;
}

public class Hrm1008SettingsDto
{
    [JsonPropertyName("country_code")]
    public string? CountryCode { get; set; }

    [JsonPropertyName("currency_code")]
    public string? CurrencyCode { get; set; }

    [JsonPropertyName("weekend_days")]
    public string? WeekendDays { get; set; }

    [JsonPropertyName("fiscal_year_start")]
    public string? FiscalYearStart { get; set; }

    [JsonPropertyName("overtime_multiplier")]
    public decimal? OvertimeMultiplier { get; set; }

    [JsonPropertyName("overtime_formula")]
    public string? OvertimeFormula { get; set; }

    [JsonPropertyName("daily_rate_formula")]
    public string? DailyRateFormula { get; set; }

    [JsonPropertyName("lwp_formula")]
    public string? LwpFormula { get; set; }

    [JsonPropertyName("tax_regime")]
    public string? TaxRegime { get; set; }

    [JsonPropertyName("enable_provident_fund")]
    public short? EnableProvidentFund { get; set; }

    [JsonPropertyName("enable_gratuity")]
    public short? EnableGratuity { get; set; }

    [JsonPropertyName("enable_festival_bonus")]
    public short? EnableFestivalBonus { get; set; }

    [JsonPropertyName("maternity_weeks")]
    public int? MaternityWeeks { get; set; }
}

public class Hrm1009TaxSlabLineDto
{
    [JsonPropertyName("tax_slab_no")]
    public long? TaxSlabNo { get; set; }

    [JsonPropertyName("slab_order")]
    public int SlabOrder { get; set; }

    [JsonPropertyName("from_amount")]
    public decimal FromAmount { get; set; }

    [JsonPropertyName("to_amount")]
    public decimal? ToAmount { get; set; }

    [JsonPropertyName("rate_percent")]
    public decimal RatePercent { get; set; }

    [JsonPropertyName("fixed_amount")]
    public decimal? FixedAmount { get; set; }

    [JsonPropertyName("is_active")]
    public short IsActive { get; set; } = 1;

    [JsonPropertyName("row_version")]
    public long? RowVersion { get; set; }
}

public class Hrm1009TaxSlabDto
{
    [JsonPropertyName("tax_slab_no")]
    public long? TaxSlabNo { get; set; }

    [JsonPropertyName("country_code")]
    public string? CountryCode { get; set; }

    [JsonPropertyName("fiscal_year")]
    public string? FiscalYear { get; set; }

    [JsonPropertyName("taxpayer_class")]
    public string? TaxpayerClass { get; set; }

    [JsonPropertyName("slab_count")]
    public int SlabCount { get; set; }

    [JsonPropertyName("is_active")]
    public short IsActive { get; set; } = 1;

    [JsonPropertyName("row_version")]
    public long? RowVersion { get; set; }

    [JsonPropertyName("slabs")]
    public List<Hrm1009TaxSlabLineDto> Slabs { get; set; } = new();
}

public class Hrm1101AttendanceDto
{
    [JsonPropertyName("attendance_no")]
    public long? AttendanceNo { get; set; }

    [JsonPropertyName("employee_no")]
    public long EmployeeNo { get; set; }

    [JsonPropertyName("employee_name")]
    public string? EmployeeName { get; set; }

    [JsonPropertyName("atten_date")]
    public DateTime AttenDate { get; set; }

    [JsonPropertyName("in_time")]
    public TimeOnly? InTime { get; set; }

    [JsonPropertyName("out_time")]
    public TimeOnly? OutTime { get; set; }

    [JsonPropertyName("status")]
    public short Status { get; set; } = 1; // 1=Present, 2=Absent, 3=Late, 4=Leave, 5=Holiday

    [JsonPropertyName("ot_hours")]
    public decimal OtHours { get; set; } = 0m;

    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }
}

public class Hrm1102PunchDto
{
    [JsonPropertyName("employee_no")]
    public long? EmployeeNo { get; set; }

    [JsonPropertyName("att_date")]
    public DateTime? AttDate { get; set; }

    [JsonPropertyName("in_time")]
    public TimeOnly? InTime { get; set; }

    [JsonPropertyName("out_time")]
    public TimeOnly? OutTime { get; set; }

    [JsonPropertyName("shift_no")]
    public long? ShiftNo { get; set; }
}

public class Hrm1102SyncRequestDto
{
    [JsonPropertyName("branch_no")]
    public long? BranchNo { get; set; }

    [JsonPropertyName("punches")]
    public List<Hrm1102PunchDto> Punches { get; set; } = new();
}

public class Hrm1102SyncResultDto
{
    [JsonPropertyName("created")]
    public int Created { get; set; }

    [JsonPropertyName("updated")]
    public int Updated { get; set; }

    [JsonPropertyName("skipped")]
    public int Skipped { get; set; }

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("messages")]
    public List<String> Messages { get; set; } = new();
}

public class Hrm1102AttendanceRowDto
{
    [JsonPropertyName("attendance_no")]
    public long AttendanceNo { get; set; }

    [JsonPropertyName("employee_no")]
    public long EmployeeNo { get; set; }

    [JsonPropertyName("att_date")]
    public DateTime AttDate { get; set; }

    [JsonPropertyName("in_time")]
    public TimeOnly? InTime { get; set; }

    [JsonPropertyName("out_time")]
    public TimeOnly? OutTime { get; set; }

    [JsonPropertyName("status")]
    public short Status { get; set; }

    [JsonPropertyName("late_minutes")]
    public int LateMinutes { get; set; }

    [JsonPropertyName("worked_hours")]
    public decimal WorkedHours { get; set; }

    [JsonPropertyName("source")]
    public short Source { get; set; }

    [JsonPropertyName("is_locked")]
    public short? IsLocked { get; set; }
}

public class Hrm1103AdjustmentDto
{
    [JsonPropertyName("adjustment_no")]
    public long? AdjustmentNo { get; set; }

    [JsonPropertyName("employee_no")]
    public long EmployeeNo { get; set; }

    [JsonPropertyName("atten_date")]
    public DateTime AttenDate { get; set; }

    [JsonPropertyName("new_status")]
    public string NewStatus { get; set; } = "PRESENT";

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}

public class Hrm1104RosterLineDto
{
    [JsonPropertyName("employee_no")]
    public long EmployeeNo { get; set; }

    [JsonPropertyName("shift_no")]
    public long ShiftNo { get; set; }

    [JsonPropertyName("roster_date")]
    public DateTime RosterDate { get; set; }
}

public class Hrm1104RosterDto
{
    [JsonPropertyName("roster_no")]
    public long? RosterNo { get; set; }

    [JsonPropertyName("roster_name")]
    public string? RosterName { get; set; }

    [JsonPropertyName("start_date")]
    public DateTime StartDate { get; set; }

    [JsonPropertyName("end_date")]
    public DateTime EndDate { get; set; }

    [JsonPropertyName("lines")]
    public List<Hrm1104RosterLineDto> Lines { get; set; } = new();
}

public class Hrm1105OvertimeDto
{
    [JsonPropertyName("ot_no")]
    public long? OtNo { get; set; }

    [JsonPropertyName("employee_no")]
    public long EmployeeNo { get; set; }

    [JsonPropertyName("employee_name")]
    public string? EmployeeName { get; set; }

    [JsonPropertyName("ot_date")]
    public DateTime OtDate { get; set; }

    [JsonPropertyName("ot_hours")]
    public decimal OtHours { get; set; }

    [JsonPropertyName("ot_rate")]
    public decimal OtRate { get; set; }

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("status")]
    public short Status { get; set; } = 1;
}

public class Hrm1106MovementDto
{
    [JsonPropertyName("movement_no")]
    public long? MovementNo { get; set; }

    [JsonPropertyName("employee_no")]
    public long EmployeeNo { get; set; }

    [JsonPropertyName("movement_date")]
    public DateTime MovementDate { get; set; }

    [JsonPropertyName("movement_type")]
    public string MovementType { get; set; } = "OFFICIAL_DUTY";

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}

public class Hrm1201StructureLineDto
{
    [JsonPropertyName("structure_dtl_no")]
    public long? StructureDtlNo { get; set; }

    [JsonPropertyName("component_no")]
    public long? ComponentNo { get; set; }

    [JsonPropertyName("component_name")]
    public string? ComponentName { get; set; }

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }
}

public class Hrm1201SalaryStructureDto
{
    [JsonPropertyName("salary_structure_no")]
    public long? SalaryStructureNo { get; set; }

    [JsonPropertyName("employee_no")]
    public long EmployeeNo { get; set; }

    [JsonPropertyName("employee_name")]
    public string? EmployeeName { get; set; }

    [JsonPropertyName("designation_no")]
    public long? DesignationNo { get; set; }

    [JsonPropertyName("grade_no")]
    public long? GradeNo { get; set; }

    [JsonPropertyName("grade_step_no")]
    public long? GradeStepNo { get; set; }

    [JsonPropertyName("effective_from")]
    public DateTime? EffectiveFrom { get; set; }

    [JsonPropertyName("effective_to")]
    public DateTime? EffectiveTo { get; set; }

    [JsonPropertyName("basic_salary")]
    public decimal BasicSalary { get; set; }

    [JsonPropertyName("gross_salary")]
    public decimal GrossSalary { get; set; }

    [JsonPropertyName("status")]
    public short Status { get; set; } = 1; // 1=Active 2=Superseded 3=Draft

    [JsonPropertyName("branch_no")]
    public long? BranchNo { get; set; }

    [JsonPropertyName("revision_reason")]
    public string? RevisionReason { get; set; }

    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }

    [JsonPropertyName("is_active")]
    public short IsActive { get; set; } = 1;

    [JsonPropertyName("row_version")]
    public long? RowVersion { get; set; }

    [JsonPropertyName("lines")]
    public List<Hrm1201StructureLineDto> Lines { get; set; } = new();

    // computed/source metadata for revision workflow
    [JsonPropertyName("source_structure_no")]
    public long? SourceStructureNo { get; set; }

    [JsonPropertyName("source_effective_from")]
    public DateTime? SourceEffectiveFrom { get; set; }

    [JsonPropertyName("source_basic_salary")]
    public decimal? SourceBasicSalary { get; set; }

    [JsonPropertyName("source_gross_salary")]
    public decimal? SourceGrossSalary { get; set; }

    [JsonPropertyName("source_revision_reason")]
    public string? SourceRevisionReason { get; set; }

    [JsonPropertyName("source_status")]
    public short? SourceStatus { get; set; }
}

public class Hrm1202PayslipLineDto
{
    [JsonPropertyName("payslip_dtl_no")]
    public long? PayslipDtlNo { get; set; }

    [JsonPropertyName("component_no")]
    public long ComponentNo { get; set; }

    [JsonPropertyName("component_name")]
    public string? ComponentName { get; set; }

    [JsonPropertyName("component_type")]
    public short? ComponentType { get; set; }

    [JsonPropertyName("calc_type")]
    public short? CalcType { get; set; }

    [JsonPropertyName("calc_value")]
    public decimal? CalcValue { get; set; }

    [JsonPropertyName("formula_expression")]
    public string? FormulaExpression { get; set; }

    [JsonPropertyName("base_amount")]
    public decimal? BaseAmount { get; set; }

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("display_order")]
    public int? DisplayOrder { get; set; }

    [JsonPropertyName("affects_net")]
    public short? AffectsNet { get; set; }

    [JsonPropertyName("is_taxable")]
    public short? IsTaxable { get; set; }

    [JsonPropertyName("is_statutory")]
    public short? IsStatutory { get; set; }
}

public class Hrm1202PayslipDto
{
    [JsonPropertyName("payslip_no")]
    public long PayslipNo { get; set; }

    [JsonPropertyName("payroll_run_no")]
    public long PayrollRunNo { get; set; }

    [JsonPropertyName("employee_no")]
    public long EmployeeNo { get; set; }

    [JsonPropertyName("salary_structure_no")]
    public long? SalaryStructureNo { get; set; }

    [JsonPropertyName("designation_no")]
    public long? DesignationNo { get; set; }

    [JsonPropertyName("grade_no")]
    public long? GradeNo { get; set; }

    [JsonPropertyName("grade_step_no")]
    public long? GradeStepNo { get; set; }

    [JsonPropertyName("basic_salary")]
    public decimal BasicSalary { get; set; }

    [JsonPropertyName("present_days")]
    public decimal PresentDays { get; set; }

    [JsonPropertyName("absent_days")]
    public decimal AbsentDays { get; set; }

    [JsonPropertyName("leave_days")]
    public decimal LeaveDays { get; set; }

    [JsonPropertyName("lwp_days")]
    public decimal LwpDays { get; set; }

    [JsonPropertyName("payable_days")]
    public decimal PayableDays { get; set; }

    [JsonPropertyName("ot_hours")]
    public decimal OtHours { get; set; }

    [JsonPropertyName("ot_amount")]
    public decimal OtAmount { get; set; }

    [JsonPropertyName("gross_earning")]
    public decimal GrossEarning { get; set; }

    [JsonPropertyName("total_deduction")]
    public decimal TotalDeduction { get; set; }

    [JsonPropertyName("taxable_income")]
    public decimal TaxableIncome { get; set; }

    [JsonPropertyName("employer_contribution")]
    public decimal EmployerContribution { get; set; }

    [JsonPropertyName("net_pay")]
    public decimal NetPay { get; set; }

    [JsonPropertyName("pay_method")]
    public short? PayMethod { get; set; }

    [JsonPropertyName("payment_status")]
    public short PaymentStatus { get; set; }

    [JsonPropertyName("calculation_snapshot")]
    public string? CalculationSnapshot { get; set; }

    [JsonPropertyName("lines")]
    public List<Hrm1202PayslipLineDto> Lines { get; set; } = new();
}

public class Hrm1202PayrollRunDto
{
    [JsonPropertyName("payroll_run_no")]
    public long? PayrollRunNo { get; set; }

    [JsonPropertyName("payroll_run_id")]
    public string? PayrollRunId { get; set; }

    [JsonPropertyName("pay_period")]
    public string? PayPeriod { get; set; }

    [JsonPropertyName("run_type")]
    public short? RunType { get; set; }

    [JsonPropertyName("period_start")]
    public DateTime PeriodStart { get; set; }

    [JsonPropertyName("period_end")]
    public DateTime PeriodEnd { get; set; }

    [JsonPropertyName("bonus_run_no")]
    public long? BonusRunNo { get; set; }

    [JsonPropertyName("bonus_run_name")]
    public string? BonusRunName { get; set; }

    [JsonPropertyName("bonus_period")]
    public string? BonusPeriod { get; set; }

    [JsonPropertyName("status")]
    public short Status { get; set; } = 1;

    [JsonPropertyName("total_gross")]
    public decimal TotalGross { get; set; }

    [JsonPropertyName("total_deduction")]
    public decimal TotalDeduction { get; set; }

    [JsonPropertyName("total_net")]
    public decimal TotalNet { get; set; }

    [JsonPropertyName("calculation_policy_no")]
    public long? CalculationPolicyNo { get; set; }

    [JsonPropertyName("employee_count")]
    public int EmployeeCount { get; set; }

    [JsonPropertyName("calculation_started_at")]
    public DateTime? CalculationStartedAt { get; set; }

    [JsonPropertyName("calculation_completed_at")]
    public DateTime? CalculationCompletedAt { get; set; }

    [JsonPropertyName("approved_by")]
    public long? ApprovedBy { get; set; }

    [JsonPropertyName("approved_at")]
    public DateTime? ApprovedAt { get; set; }

    [JsonPropertyName("posted_event_no")]
    public long? PostedEventNo { get; set; }

    [JsonPropertyName("paid_at")]
    public DateTime? PaidAt { get; set; }

    [JsonPropertyName("paid_by")]
    public long? PaidBy { get; set; }

    [JsonPropertyName("branch_no")]
    public long? BranchNo { get; set; }

    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }

    [JsonPropertyName("is_active")]
    public short IsActive { get; set; } = 1;

    [JsonPropertyName("row_version")]
    public long? RowVersion { get; set; }

    [JsonPropertyName("posting_status")]
    public short? PostingStatus { get; set; }

    [JsonPropertyName("posting_event_type")]
    public string? PostingEventType { get; set; }

    [JsonPropertyName("payslips")]
    public List<Hrm1202PayslipDto> Payslips { get; set; } = new();
}

public class Hrm1203RunLiteDto
{
    [JsonPropertyName("payroll_run_no")]
    public long PayrollRunNo { get; set; }

    [JsonPropertyName("payroll_run_id")]
    public string? PayrollRunId { get; set; }

    [JsonPropertyName("pay_period")]
    public string? PayPeriod { get; set; }

    [JsonPropertyName("run_type")]
    public short? RunType { get; set; }

    [JsonPropertyName("status")]
    public short? Status { get; set; }

    [JsonPropertyName("employee_count")]
    public int? EmployeeCount { get; set; }

    [JsonPropertyName("total_net")]
    public decimal? TotalNet { get; set; }
}

public class Hrm1203SheetRowDto
{
    [JsonPropertyName("payslip_no")]
    public long PayslipNo { get; set; }

    [JsonPropertyName("employee_no")]
    public long EmployeeNo { get; set; }

    [JsonPropertyName("designation_no")]
    public long? DesignationNo { get; set; }

    [JsonPropertyName("grade_no")]
    public long? GradeNo { get; set; }

    [JsonPropertyName("grade_step_no")]
    public long? GradeStepNo { get; set; }

    [JsonPropertyName("basic_salary")]
    public decimal BasicSalary { get; set; }

    [JsonPropertyName("present_days")]
    public decimal PresentDays { get; set; }

    [JsonPropertyName("absent_days")]
    public decimal AbsentDays { get; set; }

    [JsonPropertyName("leave_days")]
    public decimal LeaveDays { get; set; }

    [JsonPropertyName("lwp_days")]
    public decimal LwpDays { get; set; }

    [JsonPropertyName("payable_days")]
    public decimal PayableDays { get; set; }

    [JsonPropertyName("ot_hours")]
    public decimal OtHours { get; set; }

    [JsonPropertyName("ot_amount")]
    public decimal OtAmount { get; set; }

    [JsonPropertyName("gross_earning")]
    public decimal GrossEarning { get; set; }

    [JsonPropertyName("total_deduction")]
    public decimal TotalDeduction { get; set; }

    [JsonPropertyName("taxable_income")]
    public decimal TaxableIncome { get; set; }

    [JsonPropertyName("employer_contribution")]
    public decimal EmployerContribution { get; set; }

    [JsonPropertyName("net_pay")]
    public decimal NetPay { get; set; }

    [JsonPropertyName("pay_method")]
    public short? PayMethod { get; set; }

    [JsonPropertyName("payment_status")]
    public short? PaymentStatus { get; set; }
}

public class Hrm1204PayslipLiteDto
{
    [JsonPropertyName("payslip_no")]
    public long PayslipNo { get; set; }

    [JsonPropertyName("employee_no")]
    public long EmployeeNo { get; set; }

    [JsonPropertyName("net_pay")]
    public decimal NetPay { get; set; }

    [JsonPropertyName("payment_status")]
    public short? PaymentStatus { get; set; }
}

public class Hrm1204RunLiteDto
{
    [JsonPropertyName("payroll_run_no")]
    public long PayrollRunNo { get; set; }

    [JsonPropertyName("payroll_run_id")]
    public string? PayrollRunId { get; set; }

    [JsonPropertyName("pay_period")]
    public string? PayPeriod { get; set; }

    [JsonPropertyName("run_type")]
    public short? RunType { get; set; }

    [JsonPropertyName("status")]
    public short? Status { get; set; }

    [JsonPropertyName("employee_count")]
    public int? EmployeeCount { get; set; }

    [JsonPropertyName("total_net")]
    public decimal TotalNet { get; set; }
}

public class Hrm1204PayslipLineDto
{
    [JsonPropertyName("component_no")]
    public long? ComponentNo { get; set; }

    [JsonPropertyName("component_name")]
    public string? ComponentName { get; set; }

    [JsonPropertyName("component_type")]
    public short? ComponentType { get; set; }

    [JsonPropertyName("calc_type")]
    public short? CalcType { get; set; }

    [JsonPropertyName("calc_value")]
    public decimal? CalcValue { get; set; }

    [JsonPropertyName("formula_expression")]
    public string? FormulaExpression { get; set; }

    [JsonPropertyName("base_amount")]
    public decimal? BaseAmount { get; set; }

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("display_order")]
    public int? DisplayOrder { get; set; }

    [JsonPropertyName("affects_net")]
    public short? AffectsNet { get; set; }

    [JsonPropertyName("is_taxable")]
    public short? IsTaxable { get; set; }

    [JsonPropertyName("is_statutory")]
    public short? IsStatutory { get; set; }
}

public class Hrm1204PayslipDto
{
    [JsonPropertyName("payslip_no")]
    public long PayslipNo { get; set; }

    [JsonPropertyName("payroll_run_no")]
    public long PayrollRunNo { get; set; }

    [JsonPropertyName("pay_period")]
    public string? PayPeriod { get; set; }

    [JsonPropertyName("period_start")]
    public DateTime? PeriodStart { get; set; }

    [JsonPropertyName("period_end")]
    public DateTime? PeriodEnd { get; set; }

    [JsonPropertyName("employee_no")]
    public long EmployeeNo { get; set; }

    [JsonPropertyName("salary_structure_no")]
    public long? SalaryStructureNo { get; set; }

    [JsonPropertyName("designation_no")]
    public long? DesignationNo { get; set; }

    [JsonPropertyName("grade_no")]
    public long? GradeNo { get; set; }

    [JsonPropertyName("grade_step_no")]
    public long? GradeStepNo { get; set; }

    [JsonPropertyName("basic_salary")]
    public decimal BasicSalary { get; set; }

    [JsonPropertyName("present_days")]
    public decimal? PresentDays { get; set; }

    [JsonPropertyName("absent_days")]
    public decimal? AbsentDays { get; set; }

    [JsonPropertyName("leave_days")]
    public decimal? LeaveDays { get; set; }

    [JsonPropertyName("lwp_days")]
    public decimal? LwpDays { get; set; }

    [JsonPropertyName("payable_days")]
    public decimal? PayableDays { get; set; }

    [JsonPropertyName("ot_hours")]
    public decimal? OtHours { get; set; }

    [JsonPropertyName("ot_amount")]
    public decimal? OtAmount { get; set; }

    [JsonPropertyName("gross_earning")]
    public decimal GrossEarning { get; set; }

    [JsonPropertyName("total_deduction")]
    public decimal TotalDeduction { get; set; }

    [JsonPropertyName("taxable_income")]
    public decimal? TaxableIncome { get; set; }

    [JsonPropertyName("employer_contribution")]
    public decimal? EmployerContribution { get; set; }

    [JsonPropertyName("net_pay")]
    public decimal NetPay { get; set; }

    [JsonPropertyName("pay_method")]
    public short? PayMethod { get; set; }

    [JsonPropertyName("payment_status")]
    public short? PaymentStatus { get; set; }

    [JsonPropertyName("calculation_snapshot")]
    public string? CalculationSnapshot { get; set; }

    [JsonPropertyName("lines")]
    public List<Hrm1204PayslipLineDto> Lines { get; set; } = new();
}

public class Hrm1205BonusLineDto
{
    [JsonPropertyName("employee_no")]
    public long EmployeeNo { get; set; }

    [JsonPropertyName("employee_name")]
    public string? EmployeeName { get; set; }

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }
}

public class Hrm1205BonusRunDto
{
    [JsonPropertyName("bonus_run_no")]
    public long? BonusRunNo { get; set; }

    [JsonPropertyName("bonus_name")]
    public string? BonusName { get; set; }

    [JsonPropertyName("pay_period")]
    public string? PayPeriod { get; set; }

    [JsonPropertyName("total_amount")]
    public decimal TotalAmount { get; set; }

    [JsonPropertyName("status")]
    public short Status { get; set; } = 1;

    [JsonPropertyName("lines")]
    public List<Hrm1205BonusLineDto> Lines { get; set; } = new();
}

public class Hrm1207SettlementDto
{
    [JsonPropertyName("settlement_no")]
    public long? SettlementNo { get; set; }

    [JsonPropertyName("settlement_id")]
    public string? SettlementId { get; set; }

    [JsonPropertyName("employee_no")]
    public long EmployeeNo { get; set; }

    [JsonPropertyName("employee_id")]
    public string? EmployeeId { get; set; }

    [JsonPropertyName("employee_name")]
    public string? EmployeeName { get; set; }

    [JsonPropertyName("separation_type")]
    public short? SeparationType { get; set; }

    [JsonPropertyName("last_working_day")]
    public DateTime? LastWorkingDay { get; set; }

    [JsonPropertyName("last_salary")]
    public decimal? LastSalary { get; set; }

    [JsonPropertyName("leave_encashment")]
    public decimal? LeaveEncashment { get; set; }

    [JsonPropertyName("gratuity")]
    public decimal? Gratuity { get; set; }

    [JsonPropertyName("bonus_payable")]
    public decimal? BonusPayable { get; set; }

    [JsonPropertyName("loan_recovery")]
    public decimal? LoanRecovery { get; set; }

    [JsonPropertyName("other_deduction")]
    public decimal? OtherDeduction { get; set; }

    [JsonPropertyName("net_payable")]
    public decimal NetPayable { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public decimal NetSettlement { get => NetPayable; set => NetPayable = value; }

    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }

    [JsonPropertyName("status")]
    public short Status { get; set; } = 1; // 1=Draft 2=Approved 3=Paid 4=Cancelled

    [JsonPropertyName("approved_by")]
    public long? ApprovedBy { get; set; }

    [JsonPropertyName("approved_at")]
    public DateTime? ApprovedAt { get; set; }

    [JsonPropertyName("branch_no")]
    public long? BranchNo { get; set; }

    // Preview fields
    [JsonPropertyName("current_pay_period")]
    public string? CurrentPayPeriod { get; set; }

    [JsonPropertyName("current_payroll_status")]
    public string? CurrentPayrollStatus { get; set; }

    [JsonPropertyName("unpaid_pay_periods")]
    public List<string> UnpaidPayPeriods { get; set; } = new();

    [JsonPropertyName("unpaid_salary_amount")]
    public decimal UnpaidSalaryAmount { get; set; }

    [JsonPropertyName("unpaid_bonus_amount")]
    public decimal UnpaidBonusAmount { get; set; }

    [JsonPropertyName("pending_payment_amount")]
    public decimal PendingPaymentAmount { get; set; }

    [JsonPropertyName("pending_item_count")]
    public int PendingItemCount { get; set; }

    // Posting status
    [JsonPropertyName("posting_event_no")]
    public long? PostingEventNo { get; set; }

    [JsonPropertyName("posting_status")]
    public short? PostingStatus { get; set; }

    [JsonPropertyName("posting_event_type")]
    public string? PostingEventType { get; set; }

    [JsonPropertyName("is_active")]
    public short IsActive { get; set; } = 1;

    [JsonPropertyName("row_version")]
    public long? RowVersion { get; set; }
}

public class Hrm1301BalanceDto
{
    [JsonPropertyName("employee_no")]
    public long EmployeeNo { get; set; }

    [JsonPropertyName("leave_type_no")]
    public long LeaveTypeNo { get; set; }

    [JsonPropertyName("leave_year")]
    public int LeaveYear { get; set; }

    [JsonPropertyName("entitled_days")]
    public decimal EntitledDays { get; set; }

    [JsonPropertyName("accrued_days")]
    public decimal AccruedDays { get; set; }

    [JsonPropertyName("consumed_days")]
    public decimal ConsumedDays { get; set; }

    [JsonPropertyName("carried_forward")]
    public decimal CarriedForward { get; set; }

    [JsonPropertyName("available_days")]
    public decimal AvailableDays { get; set; }

    [JsonPropertyName("leave_type_name")]
    public string? LeaveTypeName { get; set; }
}

public class Hrm1301LeaveApplicationDto
{
    [JsonPropertyName("leave_application_no")]
    public long? LeaveApplicationNo { get; set; }

    [JsonPropertyName("employee_no")]
    public long EmployeeNo { get; set; }

    [JsonPropertyName("employee_id")]
    public string? EmployeeId { get; set; }

    [JsonPropertyName("employee_name")]
    public string? EmployeeName { get; set; }

    [JsonPropertyName("leave_type_no")]
    public long LeaveTypeNo { get; set; }

    [JsonPropertyName("leave_type_name")]
    public string? LeaveTypeName { get; set; }

    [JsonPropertyName("from_date")]
    public DateTime FromDate { get; set; }

    [JsonPropertyName("to_date")]
    public DateTime ToDate { get; set; }

    [JsonPropertyName("total_days")]
    public decimal TotalDays { get; set; }

    [JsonPropertyName("is_half_day")]
    public short IsHalfDay { get; set; }

    [JsonPropertyName("leave_year")]
    public int LeaveYear { get; set; }

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    [JsonPropertyName("status")]
    public short Status { get; set; } = 0;

    [JsonPropertyName("status_name")]
    public string? StatusName { get; set; }

    [JsonPropertyName("approved_by")]
    public long? ApprovedBy { get; set; }

    [JsonPropertyName("approved_at")]
    public DateTime? ApprovedAt { get; set; }

    [JsonPropertyName("branch_no")]
    public long? BranchNo { get; set; }

    [JsonPropertyName("is_active")]
    public short IsActive { get; set; } = 1;

    [JsonPropertyName("row_version")]
    public long? RowVersion { get; set; }

    [JsonPropertyName("reliever_employee_no")]
    public long? RelieverEmployeeNo { get; set; }

    [JsonPropertyName("reliever_name")]
    public string? RelieverName { get; set; }

    [JsonPropertyName("reliever_status")]
    public short RelieverStatus { get; set; } = 0;

    [JsonPropertyName("reliever_action_at")]
    public DateTime? RelieverActionAt { get; set; }

    [JsonPropertyName("reliever_remarks")]
    public string? RelieverRemarks { get; set; }

    [JsonPropertyName("attachment_path")]
    public string? AttachmentPath { get; set; }

    [JsonPropertyName("department_no")]
    public long? DepartmentNo { get; set; }

    [JsonPropertyName("department_name")]
    public string? DepartmentName { get; set; }

    [JsonPropertyName("designation_no")]
    public long? DesignationNo { get; set; }

    [JsonPropertyName("designation_name")]
    public string? DesignationName { get; set; }

    [JsonPropertyName("decision_remarks")]
    public string? DecisionRemarks { get; set; }

    [JsonPropertyName("can_edit")]
    public bool? CanEdit { get; set; }

    [JsonPropertyName("approval_state")]
    public string? ApprovalState { get; set; }

    [JsonPropertyName("can_act")]
    public bool? CanAct { get; set; }
}

public class Hrm1303BalanceRowDto
{
    [JsonPropertyName("employee_code")]
    public string? EmployeeCode { get; set; }

    [JsonPropertyName("employee_name")]
    public string? EmployeeName { get; set; }

    [JsonPropertyName("leave_balances")]
    public List<Hrm1301BalanceDto> LeaveBalances { get; set; } = new();
}

public class Hrm1401RequisitionDto
{
    [JsonPropertyName("requisition_no")]
    public long? RequisitionNo { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("department_no")]
    public long DepartmentNo { get; set; }

    [JsonPropertyName("vacancies")]
    public int Vacancies { get; set; }

    [JsonPropertyName("status")]
    public short Status { get; set; } = 1;
}

public class Hrm1402CandidateDto
{
    [JsonPropertyName("candidate_no")]
    public long? CandidateNo { get; set; }

    [JsonPropertyName("candidate_name")]
    public string? CandidateName { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("phone")]
    public string? Phone { get; set; }

    [JsonPropertyName("requisition_no")]
    public long RequisitionNo { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "APPLIED";
}

public class Hrm1404LetterDto
{
    [JsonPropertyName("letter_no")]
    public long? LetterNo { get; set; }

    [JsonPropertyName("candidate_no")]
    public long CandidateNo { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }
}

public class Hrm1404OfferDto
{
    [JsonPropertyName("offer_no")]
    public long? OfferNo { get; set; }

    [JsonPropertyName("candidate_no")]
    public long CandidateNo { get; set; }

    [JsonPropertyName("offered_salary")]
    public decimal OfferedSalary { get; set; }

    [JsonPropertyName("joining_date")]
    public DateTime JoiningDate { get; set; }
}

public class HrmDepartmentDto
{
    [JsonPropertyName("department_no")]
    public long? DepartmentNo { get; set; }

    [JsonPropertyName("department_code")]
    public string? DepartmentCode { get; set; }

    [JsonPropertyName("department_name")]
    public string? DepartmentName { get; set; }

    [JsonPropertyName("is_active")]
    public short IsActive { get; set; } = 1;
}

public class HrmDesignationDto
{
    [JsonPropertyName("designation_no")]
    public long? DesignationNo { get; set; }

    [JsonPropertyName("designation_code")]
    public string? DesignationCode { get; set; }

    [JsonPropertyName("designation_name")]
    public string? DesignationName { get; set; }

    [JsonPropertyName("is_active")]
    public short IsActive { get; set; } = 1;
}

public class HrmEmployeeDto
{
    [JsonPropertyName("employee_no")]
    public long? EmployeeNo { get; set; }

    [JsonPropertyName("employee_code")]
    public string? EmployeeCode { get; set; }

    [JsonPropertyName("employee_name")]
    public string? EmployeeName { get; set; }

    [JsonPropertyName("department_no")]
    public long DepartmentNo { get; set; }

    [JsonPropertyName("designation_no")]
    public long DesignationNo { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("phone")]
    public string? Phone { get; set; }

    [JsonPropertyName("joining_date")]
    public DateTime JoiningDate { get; set; }

    [JsonPropertyName("basic_salary")]
    public decimal BasicSalary { get; set; }

    [JsonPropertyName("is_active")]
    public short IsActive { get; set; } = 1;
}

public class HrmEmployeeListDto
{
    [JsonPropertyName("employee_no")]
    public long EmployeeNo { get; set; }

    [JsonPropertyName("employee_code")]
    public string? EmployeeCode { get; set; }

    [JsonPropertyName("employee_name")]
    public string? EmployeeName { get; set; }

    [JsonPropertyName("department_name")]
    public string? DepartmentName { get; set; }

    [JsonPropertyName("designation_name")]
    public string? DesignationName { get; set; }
}

public class HrmLeaveApplicationRuleDto
{
    [JsonPropertyName("rule_no")]
    public long? RuleNo { get; set; }

    [JsonPropertyName("leave_type_no")]
    public long LeaveTypeNo { get; set; }

    [JsonPropertyName("max_consecutive_days")]
    public int MaxConsecutiveDays { get; set; }
}

public class HrmLeavePolicySetupDto
{
    [JsonPropertyName("policy_no")]
    public long? PolicyNo { get; set; }

    [JsonPropertyName("policy_name")]
    public string? PolicyName { get; set; }

    [JsonPropertyName("leave_type_no")]
    public long LeaveTypeNo { get; set; }

    [JsonPropertyName("employee_group_no")]
    public long? EmployeeGroupNo { get; set; }

    [JsonPropertyName("accrual_method")]
    public short? AccrualMethod { get; set; }

    [JsonPropertyName("default_days")]
    public decimal? DefaultDays { get; set; }

    [JsonPropertyName("work_days_per_leave_day")]
    public int? WorkDaysPerLeaveDay { get; set; }

    [JsonPropertyName("proration_method")]
    public short? ProrationMethod { get; set; }

    [JsonPropertyName("eligibility_service_months")]
    public int? EligibilityServiceMonths { get; set; }

    [JsonPropertyName("is_carry_forward_allowed")]
    public short? IsCarryForwardAllowed { get; set; }

    [JsonPropertyName("max_carry_forward_days")]
    public decimal? MaxCarryForwardDays { get; set; }

    [JsonPropertyName("carry_forward_validity_months")]
    public int? CarryForwardValidityMonths { get; set; }

    [JsonPropertyName("is_encashable")]
    public short? IsEncashable { get; set; }

    [JsonPropertyName("max_encashable_days")]
    public decimal? MaxEncashableDays { get; set; }

    [JsonPropertyName("encashment_formula")]
    public string? EncashmentFormula { get; set; }

    [JsonPropertyName("is_lapsable")]
    public short? IsLapsable { get; set; }

    [JsonPropertyName("max_dept_leave_percentage")]
    public decimal? MaxDeptLeavePercentage { get; set; }

    [JsonPropertyName("specialized_config")]
    public string? SpecializedConfig { get; set; }

    [JsonPropertyName("branch_no")]
    public long? BranchNo { get; set; }

    [JsonPropertyName("row_version")]
    public long? RowVersion { get; set; }
}
