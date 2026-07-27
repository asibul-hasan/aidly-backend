using System.Text.Json.Serialization;

namespace AidlyErp.Application.Sys.Dto;

/// <summary>A session row in the SYS1109 monitor, joined to the owning user.</summary>
public class Sys1109SessionDto
{
    [JsonPropertyName("session_no")]
    public long SessionNo { get; set; }

    [JsonPropertyName("user_no")]
    public long? UserNo { get; set; }

    [JsonPropertyName("user_id")]
    public string? UserId { get; set; }

    [JsonPropertyName("user_name")]
    public string? UserName { get; set; }

    [JsonPropertyName("ip_address")]
    public string? IpAddress { get; set; }

    [JsonPropertyName("user_agent")]
    public string? UserAgent { get; set; }

    [JsonPropertyName("active_branch_no")]
    public long? ActiveBranchNo { get; set; }

    [JsonPropertyName("active_company_no")]
    public long? ActiveCompanyNo { get; set; }

    [JsonPropertyName("login_at")]
    public DateTime? LoginAt { get; set; }

    [JsonPropertyName("last_seen_at")]
    public DateTime? LastSeenAt { get; set; }

    [JsonPropertyName("expired_at")]
    public DateTime? ExpiredAt { get; set; }

    [JsonPropertyName("logout_at")]
    public DateTime? LogoutAt { get; set; }

    [JsonPropertyName("is_revoked")]
    public short? IsRevoked { get; set; }

    /// <summary>Derived: not revoked, not logged out, and not past its expiry.</summary>
    [JsonPropertyName("is_live")]
    public bool? IsLive { get; set; }
}

/// <summary>A login attempt row in the SYS1109 monitor.</summary>
public class Sys1109LoginAttemptDto
{
    [JsonPropertyName("login_attempt_no")]
    public long LoginAttemptNo { get; set; }

    [JsonPropertyName("user_id")]
    public string? UserId { get; set; }

    [JsonPropertyName("user_name")]
    public string? UserName { get; set; }

    [JsonPropertyName("ip_address")]
    public string? IpAddress { get; set; }

    [JsonPropertyName("is_success")]
    public short? IsSuccess { get; set; }

    [JsonPropertyName("fail_reason")]
    public string? FailReason { get; set; }

    [JsonPropertyName("attempted_at")]
    public DateTime? AttemptedAt { get; set; }
}
