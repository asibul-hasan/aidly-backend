using System.Text.Json.Serialization;

namespace AidlyErp.Application.Sys.Dto;

/// <summary>
/// One row of the SYS1202 enrolment grid: a menu from the global catalogue, plus whether this
/// company is enrolled in it and for how long.
/// </summary>
public class Sys1202MenuRow
{
    [JsonPropertyName("menu_no")]
    public long? MenuNo { get; set; }

    [JsonPropertyName("form_id")]
    public string? FormId { get; set; }

    [JsonPropertyName("form_name")]
    public string? FormName { get; set; }

    [JsonPropertyName("module_name")]
    public string? ModuleName { get; set; }

    [JsonPropertyName("module_code")]
    public string? ModuleCode { get; set; }

    [JsonPropertyName("submodule_name")]
    public string? SubmoduleName { get; set; }

    [JsonPropertyName("module_serial")]
    public int? ModuleSerial { get; set; }

    /// <summary>True when an enrolment row exists for this company.</summary>
    [JsonPropertyName("enrolled")]
    public bool? Enrolled { get; set; }

    [JsonPropertyName("enroll_menu_no")]
    public long? EnrollMenuNo { get; set; }

    /// <summary>1 = perpetual; the date window is then ignored.</summary>
    [JsonPropertyName("is_lifetime")]
    public short? IsLifetime { get; set; }

    [JsonPropertyName("enroll_start_date")]
    public DateOnly? EnrollStartDate { get; set; }

    [JsonPropertyName("enroll_end_date")]
    public DateOnly? EnrollEndDate { get; set; }
}
