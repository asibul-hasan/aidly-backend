using System.Text.Json.Serialization;

namespace AidlyErp.Sys.Application.Dto;

/// <summary>
/// File metadata for the SYS1203 File/Image Manager. Deliberately carries no bytes — the content
/// is streamed from <see cref="ServePath"/> instead of being inlined into JSON.
/// </summary>
public class Sys1203FileDto
{
    [JsonPropertyName("file_no")]
    public long FileNo { get; set; }

    [JsonPropertyName("entity_type")]
    public short? EntityType { get; set; }

    [JsonPropertyName("entity_type_label")]
    public string? EntityTypeLabel { get; set; }

    [JsonPropertyName("entity_no")]
    public long? EntityNo { get; set; }

    [JsonPropertyName("file_name")]
    public string? FileName { get; set; }

    [JsonPropertyName("content_type")]
    public string? ContentType { get; set; }

    [JsonPropertyName("file_extension")]
    public string? FileExtension { get; set; }

    [JsonPropertyName("file_size")]
    public long? FileSize { get; set; }

    [JsonPropertyName("storage_type")]
    public short? StorageType { get; set; }

    [JsonPropertyName("is_primary")]
    public short? IsPrimary { get; set; }

    [JsonPropertyName("width_px")]
    public int? WidthPx { get; set; }

    [JsonPropertyName("height_px")]
    public int? HeightPx { get; set; }

    [JsonPropertyName("checksum_sha256")]
    public string? ChecksumSha256 { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime? CreatedAt { get; set; }

    [JsonPropertyName("row_version")]
    public long? RowVersion { get; set; }

    /// <summary>Canonical relative URL to stream this file's bytes.</summary>
    [JsonPropertyName("serve_path")]
    public string? ServePath { get; set; }
}
