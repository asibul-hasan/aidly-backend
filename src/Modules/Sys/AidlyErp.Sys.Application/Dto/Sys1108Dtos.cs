using System.Text.Json.Serialization;

namespace AidlyErp.Sys.Application.Dto;

/// <summary>An approver on a workflow step. Identified by employee.</summary>
public class Sys1108ApproverDto
{
    [JsonPropertyName("approver_no")]
    public long? ApproverNo { get; set; }

    [JsonPropertyName("emp_no")]
    public long? EmpNo { get; set; }

    [JsonPropertyName("is_active")]
    public short? IsActive { get; set; } = 1;
}

/// <summary>One step in an approval workflow, with its approvers.</summary>
public class Sys1108StepDto
{
    [JsonPropertyName("step_no")]
    public long? StepNo { get; set; }

    [JsonPropertyName("step_number")]
    public short? StepNumber { get; set; }

    [JsonPropertyName("step_type")]
    public short? StepType { get; set; } = 1;

    [JsonPropertyName("next_step_no")]
    public short? NextStepNo { get; set; }

    [JsonPropertyName("step_name")]
    public string? StepName { get; set; }

    [JsonPropertyName("is_final")]
    public short? IsFinal { get; set; } = 0;

    [JsonPropertyName("is_active")]
    public short? IsActive { get; set; } = 1;

    [JsonPropertyName("approvers")]
    public List<Sys1108ApproverDto> Approvers { get; set; } = new();
}

/// <summary>
/// An approval scope (<c>sys_approval_scope</c>) — the workflow that applies to a given
/// menu/branch/department combination, with its ordered steps.
/// </summary>
public class Sys1108ScopeDto
{
    [JsonPropertyName("scope_no")]
    public long? ScopeNo { get; set; }

    [JsonPropertyName("workflow_name")]
    public string? WorkflowName { get; set; }

    [JsonPropertyName("branch_no")]
    public long? BranchNo { get; set; }

    [JsonPropertyName("department_no")]
    public long? DepartmentNo { get; set; }

    [JsonPropertyName("menu_no")]
    public long? MenuNo { get; set; }

    [JsonPropertyName("is_active")]
    public short? IsActive { get; set; } = 1;

    [JsonPropertyName("row_version")]
    public long? RowVersion { get; set; }

    [JsonPropertyName("steps")]
    public List<Sys1108StepDto> Steps { get; set; } = new();
}
