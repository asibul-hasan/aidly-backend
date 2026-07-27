using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AidlyErp.Domain.Auth;

[Table("sys_session")]
public class SysSession
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("session_no")]
    public long SessionNo { get; set; }

    [Column("user_no")]
    public long UserNo { get; set; }

    [Column("active_company_no")]
    public long? ActiveCompanyNo { get; set; }

    [Column("active_branch_no")]
    public long? ActiveBranchNo { get; set; }

    [Column("access_scope")]
    public short? AccessScope { get; set; }

    [Column("session_uuid")]
    public Guid SessionUuid { get; set; } = Guid.NewGuid();

    [Column("session_token_hash")]
    [StringLength(255)]
    public string SessionTokenHash { get; set; } = string.Empty;

    [Column("token_version")]
    public int? TokenVersion { get; set; }

    [Column("device_uuid")]
    [StringLength(80)]
    public string? DeviceUuid { get; set; }

    [Column("last_seen_at")]
    public DateTime? LastSeenAt { get; set; }

    [Column("branch_no")]
    public long? BranchNo { get; set; }

    [Column("ip_address")]
    [StringLength(45)]
    public string? IpAddress { get; set; }

    [Column("user_agent")]
    public string? UserAgent { get; set; }

    [Column("login_at")]
    public DateTime LoginAt { get; set; } = DateTime.UtcNow;

    [Column("logout_at")]
    public DateTime? LogoutAt { get; set; }

    [Column("expired_at")]
    public DateTime? ExpiredAt { get; set; }

    [Column("is_revoked")]
    public short IsRevoked { get; set; } = 0;

    [Column("revoked_at")]
    public DateTime? RevokedAt { get; set; }

    [ConcurrencyCheck]
    [Column("row_version")]
    public long RowVersion { get; set; } = 1L;

    public bool IsLive()
    {
        return IsRevoked == 0 && !LogoutAt.HasValue && (!ExpiredAt.HasValue || ExpiredAt.Value > DateTime.UtcNow);
    }
}
