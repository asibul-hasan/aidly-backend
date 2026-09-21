using System.Text.Json.Serialization;

namespace AidlyErp.Sys.Application.Dto;

// ═══════════════════════════════════════════════════════════════════════════
// SYS_1301 — ID Generator setup. Field names match the Angular form's
// interfaces exactly (features/sys/forms/sys1301/services/model.service.ts).
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>One configured document-number series.</summary>
public class Sys1301IdGeneratorDto
{
    [JsonPropertyName("doc_sequence_no")]
    public long? DocSequenceNo { get; set; }

    /// <summary>The form this series belongs to, so the grid can group by screen.</summary>
    [JsonPropertyName("menu_no")]
    public long? MenuNo { get; set; }

    [JsonPropertyName("form_id")]
    public string? FormId { get; set; }

    [JsonPropertyName("form_name")]
    public string? FormName { get; set; }

    [JsonPropertyName("module_name")]
    public string? ModuleName { get; set; }

    [JsonPropertyName("submodule_name")]
    public string? SubmoduleName { get; set; }

    /// <summary>Distinguishes series sharing one doc_type — cash versus credit, say.</summary>
    [JsonPropertyName("doc_sub_type")]
    public string? DocSubType { get; set; }

    [JsonPropertyName("doc_type")]
    public string? DocType { get; set; }

    [JsonPropertyName("branch_no")]
    public long? BranchNo { get; set; }

    [JsonPropertyName("fin_year_no")]
    public long? FinYearNo { get; set; }

    /// <summary>Template such as <c>INV-{FY_YY_YY}-{SEQ:6}</c>. Empty falls back to prefix+padding.</summary>
    [JsonPropertyName("pattern")]
    public string? Pattern { get; set; }

    [JsonPropertyName("prefix")]
    public string? Prefix { get; set; }

    [JsonPropertyName("suffix")]
    public string? Suffix { get; set; }

    [JsonPropertyName("starting_no")]
    public long StartingNo { get; set; } = 1;

    /// <summary>Read-only to the form — the engine owns it. Shown so an operator can see
    /// where a series has reached before changing its shape.</summary>
    [JsonPropertyName("next_no")]
    public long NextNo { get; set; } = 1;

    [JsonPropertyName("padding")]
    public short Padding { get; set; } = 6;

    /// <summary>1 = restart each financial year, 2 = never.</summary>
    [JsonPropertyName("reset_policy")]
    public short ResetPolicy { get; set; } = 2;

    [JsonPropertyName("is_active")]
    public short IsActive { get; set; } = 1;

    [JsonPropertyName("row_version")]
    public long? RowVersion { get; set; }
}

/// <summary>A form the operator can attach a series to.</summary>
public class Sys1301MenuOptionDto
{
    [JsonPropertyName("menu_no")]
    public long MenuNo { get; set; }

    [JsonPropertyName("form_id")]
    public string FormId { get; set; } = string.Empty;

    [JsonPropertyName("form_name")]
    public string FormName { get; set; } = string.Empty;

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
    public string Pattern { get; set; } = string.Empty;

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

    /// <summary>Comma-separated token names the pattern uses, so the form can warn when one
    /// needs context the company has not configured.</summary>
    [JsonPropertyName("tokens_used")]
    public string TokensUsed { get; set; } = string.Empty;
}

/// <summary>A selectable option — matches the form's <c>ISysLookupOption</c>.</summary>
public class SysLookupOptionDto
{
    [JsonPropertyName("value")]
    public long Value { get; set; }

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;
}
