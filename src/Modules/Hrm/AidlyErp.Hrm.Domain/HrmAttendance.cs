using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AidlyErp.Shared.Core;
using AidlyErp.Shared.Core.Security;

namespace AidlyErp.Hrm.Domain;

[Table("hrm_attendance")]
public class HrmAttendance : AuditEntity, IBranchScopedEntity, IEmployeeOwnedEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("attendance_no")]
    public long AttendanceNo { get; set; }

    [Column("employee_no")]
    public long EmployeeNo { get; set; }

    /// <summary>Marker for the EMPLOYEE data scope; the filter binds the concrete column above.</summary>
    long? IEmployeeOwnedEntity.EmployeeNo => EmployeeNo;

    [Column("att_date")]
    public DateTime AttDate { get; set; }

    [Column("shift_no")]
    public long? ShiftNo { get; set; }

    // Java maps these as LocalTime; TimeOnly is the exact .NET counterpart (TimeSpan is a
    // duration, not a time of day, and would not round-trip a PostgreSQL `time` column cleanly).
    [Column("in_time")]
    public TimeOnly? InTime { get; set; }

    [Column("out_time")]
    public TimeOnly? OutTime { get; set; }

    [Column("status")]
    public short Status { get; set; }

    [Column("late_minutes")]
    public int? LateMinutes { get; set; } = 0;

    [Column("early_out_minutes")]
    public int? EarlyOutMinutes { get; set; } = 0;

    [Column("worked_hours")]
    public decimal? WorkedHours { get; set; } = 0;

    [Column("ot_hours")]
    public decimal? OtHours { get; set; } = 0;

    [Column("source")]
    public short? Source { get; set; } = 1;

    [Column("leave_application_no")]
    public long? LeaveApplicationNo { get; set; }

    [Column("is_locked")]
    public short? IsLocked { get; set; } = 0;

    [Column("branch_no")]
    public long? BranchNo { get; set; }

    /// <summary>Not a column — the Java HRM schema is branch-scoped; company is reached
    /// via the branch. Mapping it made every HRM query select a non-existent column.</summary>
    [NotMapped]
    public long? CompanyNo { get; set; }

    [NotMapped]
    public DateTime AttendanceDate { get => AttDate; set => AttDate = value; }

    [NotMapped]
    public DateTime AttenDate { get => AttDate; set => AttDate = value; }

    /// <summary>Alias — the schema column is <c>worked_hours</c>.</summary>
    [NotMapped]
    public decimal? WorkingHours { get => WorkedHours; set => WorkedHours = value; }

    /// <summary>Not a column in this schema — kept so callers and DTOs are unaffected, but never persisted.</summary>
    [NotMapped]
    public string? Remarks { get; set; }
}
