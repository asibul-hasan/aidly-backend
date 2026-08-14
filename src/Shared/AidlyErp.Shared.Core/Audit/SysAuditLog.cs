using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AidlyErp.Shared.Core.Audit;

[Table("sys_audit_log")]
public class SysAuditLog
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("audit_log_no")]
    public long AuditLogNo { get; set; }

    [Column("company_no")]
    public long? CompanyNo { get; set; }

    [Column("branch_no")]
    public long? BranchNo { get; set; }

    [Column("user_no")]
    public long? UserNo { get; set; }

    [Column("session_no")]
    public long? SessionNo { get; set; }

    [Column("table_name")]
    [StringLength(150)]
    public string TableName { get; set; } = string.Empty;

    [Column("record_pk")]
    [StringLength(100)]
    public string RecordPk { get; set; } = string.Empty;

    [Column("action_type")]
    [StringLength(20)]
    public string ActionType { get; set; } = string.Empty;

    [Column("old_data", TypeName = "jsonb")]
    public string? OldData { get; set; }

    [Column("new_data", TypeName = "jsonb")]
    public string? NewData { get; set; }

    [Column("ip_address")]
    [StringLength(45)]
    public string? IpAddress { get; set; }

    [Column("action_at")]
    public DateTime ActionAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// HTTP request log — port of Java <c>core.audit.entity.SysLog</c>.
/// Captures mutating (non-GET) request traffic for audit: method, URI, status, latency, client IP.
/// </summary>
[Table("sys_log")]
public class SysLog
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("log_no")]
    public long LogNo { get; set; }

    [Column("company_no")]
    public long? CompanyNo { get; set; }

    [Column("branch_no")]
    public long? BranchNo { get; set; }

    [Column("user_no")]
    public long? UserNo { get; set; }

    [Column("session_no")]
    public long? SessionNo { get; set; }

    [Column("http_method")]
    [StringLength(10)]
    public string? HttpMethod { get; set; }

    [Column("request_uri")]
    [StringLength(500)]
    public string? RequestUri { get; set; }

    [Column("response_status")]
    public int? ResponseStatus { get; set; }

    [Column("duration_ms")]
    public long? DurationMs { get; set; }

    [Column("ip_address")]
    [StringLength(45)]
    public string? IpAddress { get; set; }

    // No user_agent / request_body / error_message here: sys_log has none of those columns and
    // the Java entity never declared them. Mapping them made every request-log insert fail with
    // 42703 — invisibly, because the writer swallows its exceptions.

    [Column("request_at")]
    public DateTime RequestAt { get; set; } = DateTime.UtcNow;
}
