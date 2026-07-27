using System.Text.Json.Serialization;

namespace AidlyErp.Application.Sys.Dto;

/// <summary>Row in the SYS1105 Activity Log Viewer.</summary>
public class Sys1105AuditLogDto
{
    [JsonPropertyName("audit_log_no")]
    public long AuditLogNo { get; set; }

    [JsonPropertyName("branch_no")]
    public long? BranchNo { get; set; }

    [JsonPropertyName("user_no")]
    public long? UserNo { get; set; }

    [JsonPropertyName("session_no")]
    public long? SessionNo { get; set; }

    [JsonPropertyName("table_name")]
    public string? TableName { get; set; }

    [JsonPropertyName("record_pk")]
    public string? RecordPk { get; set; }

    [JsonPropertyName("action_type")]
    public string? ActionType { get; set; }

    [JsonPropertyName("ip_address")]
    public string? IpAddress { get; set; }

    [JsonPropertyName("action_at")]
    public DateTime? ActionAt { get; set; }
}
