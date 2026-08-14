using System.Text.Json.Serialization;

namespace AidlyErp.Sys.Application.Dto;

// ═══════════════════════════════════════════════════════════════════════════
// SYS_1301 — Document ID Generator setup
// ═══════════════════════════════════════════════════════════════════════════

public class Sys1301IdGeneratorDto
{
    [JsonPropertyName("doc_sequence_no")]
    public long? DocSequenceNo { get; set; }

    [JsonPropertyName("menu_no")]
    public long? MenuNo { get; set; }

    [JsonPropertyName("form_id")]
    public string? FormId { get; set; }

    [JsonPropertyName("form_name")]
    public string? FormName { get; set; }

    [JsonPropertyName("submodule_name")]
    public string? SubmoduleName { get; set; }

    [JsonPropertyName("module_name")]
    public string? ModuleName { get; set; }

    [JsonPropertyName("doc_type")]
    public string? DocType { get; set; }

    [JsonPropertyName("doc_sub_type")]
    public string? DocSubType { get; set; }

    [JsonPropertyName("fin_year_no")]
    public long? FinYearNo { get; set; }

    [JsonPropertyName("branch_no")]
    public long? BranchNo { get; set; }

    [JsonPropertyName("pattern")]
    public string? Pattern { get; set; }

    [JsonPropertyName("prefix")]
    public string? Prefix { get; set; }

    [JsonPropertyName("suffix")]
    public string? Suffix { get; set; }

    [JsonPropertyName("starting_no")]
    public long? StartingNo { get; set; }

    /// <summary>The next number this series will hand out. Read-only to the form — editing it
    /// would let a user rewind a live counter onto numbers already issued.</summary>
    [JsonPropertyName("next_no")]
    public long NextNo { get; set; }

    [JsonPropertyName("padding")]
    public short? Padding { get; set; }

    /// <summary>1=Yearly 2=Never 3=Monthly.</summary>
    [JsonPropertyName("reset_policy")]
    public short? ResetPolicy { get; set; }

    [JsonPropertyName("is_active")]
    public short? IsActive { get; set; } = 1;

    [JsonPropertyName("row_version")]
    public long? RowVersion { get; set; }
}

public class Sys1301MenuOptionDto
{
    [JsonPropertyName("menu_no")]
    public long MenuNo { get; set; }

    [JsonPropertyName("form_id")]
    public string? FormId { get; set; }

    [JsonPropertyName("form_name")]
    public string? FormName { get; set; }

    [JsonPropertyName("submodule_no")]
    public long? SubmoduleNo { get; set; }

    [JsonPropertyName("submodule_name")]
    public string? SubmoduleName { get; set; }

    [JsonPropertyName("module_no")]
    public long? ModuleNo { get; set; }

    [JsonPropertyName("module_name")]
    public string? ModuleName { get; set; }
}

public class Sys1301PreviewRequestDto
{
    [JsonPropertyName("pattern")]
    public string? Pattern { get; set; }

    [JsonPropertyName("doc_sub_type")]
    public string? DocSubType { get; set; }

    [JsonPropertyName("doc_date")]
    public DateTime? DocDate { get; set; }

    [JsonPropertyName("sample_sequence")]
    public long? SampleSequence { get; set; }
}

public class Sys1301PreviewResponseDto
{
    [JsonPropertyName("preview")]
    public string Preview { get; set; } = string.Empty;

    /// <summary>Comma-separated tokens the pattern uses, so the form can warn when one needs
    /// context the company has not configured.</summary>
    [JsonPropertyName("tokens_used")]
    public string TokensUsed { get; set; } = string.Empty;
}
