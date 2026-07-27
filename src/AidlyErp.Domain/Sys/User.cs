using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AidlyErp.Domain.Common;

namespace AidlyErp.Domain.Sys;

[Table("sys_user")]
public class User : AuditEntity, ICompanyScopedEntity
{
    [Key]
    [Column("user_no")]
    public long UserNo { get; set; }

    [Column("company_no")]
    public long? CompanyNo { get; set; }

    [Column("employee_no")]
    public long EmployeeNo { get; set; }

    [Column("user_id")]
    [StringLength(50)]
    public string? UserId { get; set; }

    [Column("password_hash")]
    [StringLength(255)]
    public string? PasswordHash { get; set; }

    [Column("user_name")]
    [StringLength(150)]
    public string UserName { get; set; } = string.Empty;

    [Column("email")]
    [StringLength(250)]
    public string? Email { get; set; }

    [Column("access_scope")]
    public short AccessScope { get; set; } = 1;

    [Column("default_branch_no")]
    public long? DefaultBranchNo { get; set; }

    [Column("avatar_file_no")]
    public long? AvatarFileNo { get; set; }

    [Column("is_mfa_enabled")]
    public short IsMfaEnabled { get; set; } = 0;

    [Column("mfa_secret")]
    [StringLength(255)]
    public string? MfaSecret { get; set; }

    [Column("failed_login_count")]
    public int FailedLoginCount { get; set; } = 0;

    [Column("is_locked")]
    public short IsLocked { get; set; } = 0;

    [Column("locked_until")]
    public DateTime? LockedUntil { get; set; }

    [Column("must_change_password")]
    public short MustChangePassword { get; set; } = 0;

    [Column("password_changed_at")]
    public DateTime? PasswordChangedAt { get; set; }

    [Column("last_login_at")]
    public DateTime? LastLoginAt { get; set; }

    [Column("token_version")]
    public int TokenVersion { get; set; } = 1;

    protected override void NullifyBusinessId()
    {
        UserId = null;
    }
}
