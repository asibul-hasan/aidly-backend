using System.Text.Json.Serialization;

namespace AidlyErp.Application.Sys.Dto;

/// <summary>Master DTO for the SYS_1003 Financial Year Setup form.</summary>
public class Sys1003FinYearDto
{
    [JsonPropertyName("fin_year_no")]
    public long? FinYearNo { get; set; }

    [JsonPropertyName("fin_year_id")]
    public string? FinYearId { get; set; }

    [JsonPropertyName("fin_year_name")]
    public string? FinYearName { get; set; }

    [JsonPropertyName("start_date")]
    public DateOnly? StartDate { get; set; }

    [JsonPropertyName("end_date")]
    public DateOnly? EndDate { get; set; }

    [JsonPropertyName("is_closed")]
    public short? IsClosed { get; set; }

    /// <summary><c>null</c> = company-wide (applies to all branches).</summary>
    [JsonPropertyName("branch_no")]
    public long? BranchNo { get; set; }

    /// <summary>
    /// Write-only. Insert fans out one financial year per listed branch; empty or absent means
    /// a single company-wide row (<c>branch_no = NULL</c>).
    /// </summary>
    [JsonPropertyName("branch_nos")]
    public List<long>? BranchNos { get; set; }

    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }

    [JsonPropertyName("is_active")]
    public short? IsActive { get; set; }

    [JsonPropertyName("row_version")]
    public long? RowVersion { get; set; }

    /// <summary><c>null</c> leaves existing periods untouched (master-only save).</summary>
    [JsonPropertyName("periods")]
    public List<Sys1003FinYearDtlDto>? Periods { get; set; }
}

/// <summary>Child-period DTO for the SYS_1003 Financial Year Setup form.</summary>
public class Sys1003FinYearDtlDto
{
    [JsonPropertyName("fin_period_no")]
    public long? FinPeriodNo { get; set; }

    [JsonPropertyName("fin_year_no")]
    public long? FinYearNo { get; set; }

    [JsonPropertyName("fin_period_id")]
    public string? FinPeriodId { get; set; }

    [JsonPropertyName("fin_period_name")]
    public string? FinPeriodName { get; set; }

    [JsonPropertyName("start_date")]
    public DateOnly? StartDate { get; set; }

    [JsonPropertyName("end_date")]
    public DateOnly? EndDate { get; set; }

    /// <summary>1=Monthly, 2=Quarterly, 3=Half-Yearly, 4=Yearly, 5=Adjustment.</summary>
    [JsonPropertyName("period_type")]
    public short? PeriodType { get; set; }

    /// <summary>1=Open, 2=Closed, 3=Locked.</summary>
    [JsonPropertyName("period_status")]
    public short? PeriodStatus { get; set; }

    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }

    [JsonPropertyName("is_active")]
    public short? IsActive { get; set; }

    [JsonPropertyName("row_version")]
    public long? RowVersion { get; set; }
}
