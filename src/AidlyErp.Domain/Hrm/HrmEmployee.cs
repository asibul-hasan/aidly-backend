using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AidlyErp.Domain.Common;

namespace AidlyErp.Domain.Hrm;

[Table("hrm_employee")]
public class HrmEmployee : AuditEntity, IBranchScopedEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("employee_no")]
    public long EmployeeNo { get; set; }

    [Column("employee_id")]
    [StringLength(30)]
    public string EmployeeId { get; set; } = string.Empty;

    [Column("first_name")]
    [StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Column("middle_name")]
    [StringLength(100)]
    public string? MiddleName { get; set; }

    [Column("last_name")]
    [StringLength(100)]
    public string? LastName { get; set; }

    [Column("date_of_birth")]
    public DateTime DateOfBirth { get; set; }

    [Column("gender")]
    public short? Gender { get; set; }

    [Column("marital_status")]
    public short? MaritalStatus { get; set; }

    [Column("blood_group")]
    [StringLength(5)]
    public string? BloodGroup { get; set; }

    [Column("religion")]
    public short? Religion { get; set; }

    [Column("nationality")]
    public short? Nationality { get; set; }

    [Column("photo_url")]
    public string? PhotoUrl { get; set; }

    [Column("signature_url")]
    public string? SignatureUrl { get; set; }

    [Column("nid_number")]
    [StringLength(50)]
    public string? NidNumber { get; set; }

    [Column("passport_number")]
    [StringLength(30)]
    public string? PassportNumber { get; set; }

    [Column("birth_certificate_no")]
    [StringLength(30)]
    public string? BirthCertificateNo { get; set; }

    [Column("tin_number")]
    [StringLength(30)]
    public string? TinNumber { get; set; }

    [Column("taxpayer_class")]
    [StringLength(30)]
    public string TaxpayerClass { get; set; } = "GENERAL";

    [Column("department_no")]
    public long DepartmentNo { get; set; }

    [Column("designation_no")]
    public long DesignationNo { get; set; }

    [Column("grade_no")]
    public long? GradeNo { get; set; }

    [Column("reporting_to")]
    public long? ReportingTo { get; set; }

    [Column("branch_no")]
    public long? BranchNo { get; set; }

    /// <summary>Not a column — the Java HRM schema is branch-scoped; company is reached
    /// via the branch. Mapping it made every HRM query select a non-existent column.</summary>
    [NotMapped]
    public long? CompanyNo { get; set; }

    [Column("joining_date")]
    public DateTime JoiningDate { get; set; }

    [Column("confirmation_date")]
    public DateTime? ConfirmationDate { get; set; }

    [Column("employment_type")]
    public short? EmploymentType { get; set; }

    [Column("mobile_number")]
    [StringLength(20)]
    public string MobileNumber { get; set; } = string.Empty;

    [Column("alternative_mobile_number")]
    [StringLength(20)]
    public string? AlternativeMobileNumber { get; set; }

    [Column("official_email")]
    [StringLength(150)]
    public string? OfficialEmail { get; set; }

    [Column("personal_email")]
    [StringLength(150)]
    public string? PersonalEmail { get; set; }

    [Column("emergency_contact_name")]
    [StringLength(150)]
    public string EmergencyContactName { get; set; } = string.Empty;

    [Column("emergency_contact_number")]
    [StringLength(20)]
    public string EmergencyContactNumber { get; set; } = string.Empty;

    [Column("emergency_contact_relation")]
    [StringLength(50)]
    public string? EmergencyContactRelation { get; set; }

    [Column("present_address")]
    public string PresentAddress { get; set; } = string.Empty;

    [Column("permanent_address")]
    public string? PermanentAddress { get; set; }

    [Column("present_post_code")]
    [StringLength(20)]
    public string? PresentPostCode { get; set; }

    [Column("permanent_post_code")]
    [StringLength(20)]
    public string? PermanentPostCode { get; set; }

    [Column("pay_schedule")]
    public short? PaySchedule { get; set; }

    [Column("salary")]
    public decimal Salary { get; set; }

    [Column("bank_account_no")]
    [StringLength(30)]
    public string? BankAccountNo { get; set; }

    [Column("bank_name")]
    [StringLength(100)]
    public string? BankName { get; set; }

    [Column("bank_branch")]
    [StringLength(100)]
    public string? BankBranch { get; set; }

    [Column("routing_number")]
    [StringLength(30)]
    public string? RoutingNumber { get; set; }

    [Column("mfs_number")]
    [StringLength(20)]
    public string? MfsNumber { get; set; }

    [Column("mfs_provider")]
    [StringLength(30)]
    public string? MfsProvider { get; set; }

    [Column("contract_start_date")]
    public DateTime? ContractStartDate { get; set; }

    [Column("contract_type")]
    public short? ContractType { get; set; }

    [Column("contract_duration")]
    public int? ContractDuration { get; set; }

    [Column("contract_end_date")]
    public DateTime? ContractEndDate { get; set; }

    [Column("contract_amount")]
    public decimal? ContractAmount { get; set; }

    [Column("contract_notes")]
    public string? ContractNotes { get; set; }

    [Column("highest_education")]
    public string? HighestEducation { get; set; }

    [Column("education_subject")]
    public string? EducationSubject { get; set; }

    [Column("is_experienced")]
    public short IsExperienced { get; set; } = 0;

    [Column("experience_duration")]
    [StringLength(100)]
    public string? ExperienceDuration { get; set; }

    [Column("remarks")]
    public string? Remarks { get; set; }

    [NotMapped]
    public short IsCreateUser { get; set; } = 0;

    // -----------------------------------------------------------------------
    // Derived convenience members.
    //
    // The Java schema has no employee_name / email / phone / status columns — it stores the name
    // in three parts, two email addresses, a mobile number, and uses is_active for employment
    // status. These project onto those real columns so callers can read a single value; they are
    // [NotMapped] because there is nothing to persist them to.
    // -----------------------------------------------------------------------

    /// <summary>
    /// First + middle + last name, single-spaced and trimmed. Assigning splits the value back
    /// across the three stored columns: one token → first name; two → first + last;
    /// three or more → first, middle (everything between), last.
    /// </summary>
    [NotMapped]
    public string EmployeeName
    {
        get => string.Join(' ', new[] { FirstName, MiddleName, LastName }
            .Where(part => !string.IsNullOrWhiteSpace(part)));
        set
        {
            var parts = (value ?? string.Empty)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            FirstName = parts.Length > 0 ? parts[0] : string.Empty;
            MiddleName = parts.Length > 2 ? string.Join(' ', parts[1..^1]) : null;
            LastName = parts.Length > 1 ? parts[^1] : null;
        }
    }

    /// <summary>Official email, falling back to the personal address. Assigning writes the official address.</summary>
    [NotMapped]
    public string? Email
    {
        get => string.IsNullOrWhiteSpace(OfficialEmail) ? PersonalEmail : OfficialEmail;
        set => OfficialEmail = value;
    }

    /// <summary>Primary contact number — the schema column is <c>mobile_number</c>.</summary>
    [NotMapped]
    public string? Phone
    {
        get => MobileNumber;
        set => MobileNumber = value;
    }

    [NotMapped]
    public string EmployeeCode { get => EmployeeId; set => EmployeeId = value; }

    [NotMapped]
    public string FullName => EmployeeName;

    [NotMapped]
    public decimal BasicSalary { get => Salary; set => Salary = value; }

    /// <summary>Employment status — mirrors <c>is_active</c> (1 = in service, 0 = not).</summary>
    [NotMapped]
    public short Status { get => IsActive; set => IsActive = value; }

    /// <summary>Alias for <see cref="Status"/>; the Java field for this concept is <c>is_active</c>.</summary>
    [NotMapped]
    public short EmploymentStatus { get => IsActive; set => IsActive = value; }

    protected override void NullifyBusinessId()
    {
        EmployeeId = "~" + (EmployeeNo != 0 ? EmployeeNo : DateTime.UtcNow.Ticks);
    }
}
