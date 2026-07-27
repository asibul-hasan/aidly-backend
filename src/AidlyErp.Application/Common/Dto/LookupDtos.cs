using System.Text.Json.Serialization;

namespace AidlyErp.Application.Common.Dto;

/// <summary>Generic lookup row — no, code, name.</summary>
public class LookupDto
{
    [JsonPropertyName("no")]
    public long No { get; set; }

    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

public class BranchLookupDto
{
    [JsonPropertyName("branch_no")]
    public long BranchNo { get; set; }

    [JsonPropertyName("branch_id")]
    public string? BranchId { get; set; }

    [JsonPropertyName("branch_name")]
    public string? BranchName { get; set; }
}

public class CurrencyLookupDto
{
    [JsonPropertyName("currency_no")]
    public long CurrencyNo { get; set; }

    [JsonPropertyName("currency_code")]
    public string? CurrencyCode { get; set; }

    [JsonPropertyName("currency_name")]
    public string? CurrencyName { get; set; }

    [JsonPropertyName("symbol")]
    public string? Symbol { get; set; }

    [JsonPropertyName("decimal_places")]
    public int DecimalPlaces { get; set; }

    [JsonPropertyName("exchange_rate")]
    public decimal ExchangeRate { get; set; }

    [JsonPropertyName("is_base_currency")]
    public short IsBaseCurrency { get; set; }
}

public class DepartmentLookupDto
{
    [JsonPropertyName("department_no")]
    public long DepartmentNo { get; set; }

    [JsonPropertyName("department_id")]
    public string? DepartmentId { get; set; }

    [JsonPropertyName("department_name")]
    public string? DepartmentName { get; set; }
}

public class DesignationLookupDto
{
    [JsonPropertyName("designation_no")]
    public long DesignationNo { get; set; }

    [JsonPropertyName("designation_id")]
    public string? DesignationId { get; set; }

    [JsonPropertyName("designation_name")]
    public string? DesignationName { get; set; }

    [JsonPropertyName("department_no")]
    public long? DepartmentNo { get; set; }

    [JsonPropertyName("grade_no")]
    public long? GradeNo { get; set; }

    [JsonPropertyName("grade_level")]
    public string? GradeLevel { get; set; }

    [JsonPropertyName("min_salary")]
    public decimal? MinSalary { get; set; }

    [JsonPropertyName("max_salary")]
    public decimal? MaxSalary { get; set; }
}

public class EmployeeLookupDto
{
    [JsonPropertyName("employee_no")]
    public long EmployeeNo { get; set; }

    [JsonPropertyName("employee_id")]
    public string? EmployeeId { get; set; }

    [JsonPropertyName("full_name")]
    public string? FullName { get; set; }

    [JsonPropertyName("full_name_with_id")]
    public string? FullNameWithId { get; set; }

    [JsonPropertyName("department_no")]
    public long? DepartmentNo { get; set; }

    [JsonPropertyName("department_name")]
    public string? DepartmentName { get; set; }

    [JsonPropertyName("designation_no")]
    public long? DesignationNo { get; set; }

    [JsonPropertyName("designation_name")]
    public string? DesignationName { get; set; }

    [JsonPropertyName("branch_no")]
    public long? BranchNo { get; set; }

    [JsonPropertyName("user_no")]
    public long? UserNo { get; set; }
}

public class GradeLookupDto
{
    [JsonPropertyName("grade_no")]
    public long GradeNo { get; set; }

    [JsonPropertyName("grade_id")]
    public string? GradeId { get; set; }

    [JsonPropertyName("grade_name")]
    public string? GradeName { get; set; }

    [JsonPropertyName("rank_order")]
    public int? RankOrder { get; set; }

    [JsonPropertyName("min_salary")]
    public decimal? MinSalary { get; set; }

    [JsonPropertyName("max_salary")]
    public decimal? MaxSalary { get; set; }
}

public class GradeStepLookupDto
{
    [JsonPropertyName("grade_step_no")]
    public long GradeStepNo { get; set; }

    [JsonPropertyName("grade_no")]
    public long GradeNo { get; set; }

    [JsonPropertyName("step")]
    public string Step { get; set; } = string.Empty;

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }
}
