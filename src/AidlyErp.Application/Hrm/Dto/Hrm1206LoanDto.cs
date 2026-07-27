using System.Text.Json.Serialization;

namespace AidlyErp.Application.Hrm.Dto;

public class Hrm1206LoanDto
{
    [JsonPropertyName("loan_no")]
    public long? LoanNo { get; set; }

    [JsonPropertyName("loan_id")]
    public string? LoanId { get; set; }

    [JsonPropertyName("employee_no")]
    public long EmployeeNo { get; set; }

    [JsonPropertyName("loan_type")]
    public short? LoanType { get; set; }

    [JsonPropertyName("principal_amount")]
    public decimal PrincipalAmount { get; set; }

    [JsonPropertyName("installment_count")]
    public int InstallmentCount { get; set; } = 1;

    [JsonPropertyName("installment_amount")]
    public decimal InstallmentAmount { get; set; }

    [JsonPropertyName("recovered_amount")]
    public decimal RecoveredAmount { get; set; }

    [JsonPropertyName("outstanding_amount")]
    public decimal OutstandingAmount { get; set; }

    [JsonPropertyName("disbursement_date")]
    public DateTime? DisbursementDate { get; set; }

    [JsonPropertyName("recovery_start")]
    public DateTime? RecoveryStart { get; set; }

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    [JsonPropertyName("status")]
    public short Status { get; set; }

    [JsonPropertyName("approved_by")]
    public long? ApprovedBy { get; set; }

    [JsonPropertyName("approved_at")]
    public DateTime? ApprovedAt { get; set; }

    [JsonPropertyName("branch_no")]
    public long? BranchNo { get; set; }

    [JsonPropertyName("is_active")]
    public short IsActive { get; set; } = 1;
}
