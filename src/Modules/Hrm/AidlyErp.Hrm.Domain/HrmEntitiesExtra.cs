using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AidlyErp.Shared.Core;

namespace AidlyErp.Hrm.Domain;

[Table("hrm_salary_component")]
public class HrmSalaryComponent : AuditEntity, IBranchScopedEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("component_no")]
    public long ComponentNo { get; set; }

    [Column("component_id")]
    [StringLength(20)]
    public string? ComponentId { get; set; }

    [Column("component_name")]
    [StringLength(100)]
    public string ComponentName { get; set; } = string.Empty;

    [Column("component_type")]
    public short ComponentType { get; set; } // 1=Earning, 2=Deduction, 3=EmployerContribution, 4=StatutoryDeduction

    [Column("calc_type")]
    public short CalcType { get; set; } = 1; // 1=Fixed, 2=PercentOfBasic, 3=PercentOfGross, 4=Formula

    [Column("calc_value")]
    public decimal? CalcValue { get; set; }

    [Column("formula_expression")]
    [StringLength(500)]
    public string? FormulaExpression { get; set; }

    [Column("base_component_no")]
    public long? BaseComponentNo { get; set; }

    [Column("is_proratable")]
    public short IsProratable { get; set; } = 1;

    [Column("is_attendance_dependent")]
    public short IsAttendanceDependent { get; set; } = 0;

    [Column("is_regular")]
    public short IsRegular { get; set; } = 1;

    [Column("is_taxable")]
    public short IsTaxable { get; set; } = 1;

    [Column("affects_net")]
    public short AffectsNet { get; set; } = 1;

    [Column("is_statutory")]
    public short IsStatutory { get; set; } = 0;

    [Column("country_code")]
    [StringLength(2)]
    public string? CountryCode { get; set; }

    [Column("gl_account_code")]
    [StringLength(30)]
    public string? GlAccountCode { get; set; }

    [Column("display_order")]
    public int? DisplayOrder { get; set; }

    [Column("rounding_mode")]
    public short RoundingMode { get; set; } = 2;

    [Column("rounding_scale")]
    public short RoundingScale { get; set; } = 2;

    [Column("branch_no")]
    public long? BranchNo { get; set; }

    /// <summary>Not a column — the Java HRM schema is branch-scoped; company is reached
    /// via the branch. Mapping it made every HRM query select a non-existent column.</summary>
    [NotMapped]
    public long? CompanyNo { get; set; }

    [Column("remarks")]
    public string? Remarks { get; set; }

    [NotMapped]
    public short Type { get => ComponentType; set => ComponentType = value; }

    [NotMapped]
    public short IsProrated { get => IsProratable; set => IsProratable = value; }

    protected override void NullifyBusinessId() => ComponentId = null;
}

[Table("hrm_shift")]
public class HrmShift : AuditEntity, IBranchScopedEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("shift_no")]
    public long ShiftNo { get; set; }

    [Column("shift_id")]
    [StringLength(20)]
    public string? ShiftId { get; set; }

    [Column("shift_name")]
    [StringLength(100)]
    public string ShiftName { get; set; } = string.Empty;

    [Column("effective_date")]
    public DateOnly? EffectiveDate { get; set; }

    [Column("start_time")]
    public TimeOnly StartTime { get; set; }

    [Column("end_time")]
    public TimeOnly EndTime { get; set; }

    [Column("grace_minutes")]
    public int? GraceMinutes { get; set; } = 0;

    [Column("half_day_minutes")]
    public int? HalfDayMinutes { get; set; }

    [Column("break_minutes")]
    public int? BreakMinutes { get; set; } = 0;

    /// <summary>Bitmask of weekly off days (bit 0 = Sunday … bit 6 = Saturday).</summary>
    [Column("weekly_off_mask")]
    public short? WeeklyOffMask { get; set; } = 0;

    [Column("is_night_shift")]
    public short? IsNightShift { get; set; } = 0;

    [Column("branch_no")]
    public long? BranchNo { get; set; }

    /// <summary>Not a column — the Java HRM schema is branch-scoped; company is reached
    /// via the branch. Mapping it made every HRM query select a non-existent column.</summary>
    [NotMapped]
    public long? CompanyNo { get; set; }

    [Column("remarks")]
    public string? Remarks { get; set; }

    protected override void NullifyBusinessId() => ShiftId = null;
}

[Table("hrm_holiday")]
public class HrmHoliday : AuditEntity, IBranchScopedEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("holiday_no")]
    public long HolidayNo { get; set; }

    [Column("holiday_date")]
    public DateOnly HolidayDate { get; set; }

    [Column("holiday_name")]
    [StringLength(150)]
    public string HolidayName { get; set; } = string.Empty;

    [Column("holiday_type")]
    public short? HolidayType { get; set; } = 1;

    /// <summary>Not a column in this schema — kept so callers and DTOs are unaffected, but never persisted.</summary>
    [NotMapped]
    public short? AlternateType { get; set; }

    [Column("is_recurring")]
    public short? IsRecurring { get; set; } = 0;

    [Column("leave_year")]
    public int? LeaveYear { get; set; }

    [Column("branch_no")]
    public long? BranchNo { get; set; }

    /// <summary>Not a column — the Java HRM schema is branch-scoped; company is reached
    /// via the branch. Mapping it made every HRM query select a non-existent column.</summary>
    [NotMapped]
    public long? CompanyNo { get; set; }

    [Column("remarks")]
    public string? Remarks { get; set; }
}

[Table("hrm_grade")]
public class HrmGrade : AuditEntity, IBranchScopedEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("grade_no")]
    public long GradeNo { get; set; }

    [Column("grade_id")]
    [StringLength(20)]
    public string? GradeId { get; set; }

    [Column("grade_name")]
    [StringLength(100)]
    public string GradeName { get; set; } = string.Empty;

    [Column("rank_order")]
    public int? RankOrder { get; set; }

    [Column("min_salary", TypeName = "numeric(20,4)")]
    public decimal? MinSalary { get; set; }

    [Column("max_salary", TypeName = "numeric(20,4)")]
    public decimal? MaxSalary { get; set; }

    [Column("is_overtime_eligible")]
    public short? IsOvertimeEligible { get; set; } = 0;

    [Column("branch_no")]
    public long? BranchNo { get; set; }

    /// <summary>Not a column — the Java HRM schema is branch-scoped; company is reached
    /// via the branch. Mapping it made every HRM query select a non-existent column.</summary>
    [NotMapped]
    public long? CompanyNo { get; set; }

    [Column("remarks")]
    public string? Remarks { get; set; }

    /// <summary>Alias used by the payroll/overtime services; the schema column is <c>is_overtime_eligible</c>.</summary>
    [NotMapped]
    public short? IsOtEligible { get => IsOvertimeEligible; set => IsOvertimeEligible = value; }

    protected override void NullifyBusinessId() => GradeId = null;
}
