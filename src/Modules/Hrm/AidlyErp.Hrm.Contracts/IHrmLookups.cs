using AidlyErp.Shared.Core.Dto;

namespace AidlyErp.Hrm.Contracts;

/// <summary>
/// Dropdown/picker rows sourced from HRM, for the cross-module lookup endpoints that SYS serves.
///
/// <para>These deliberately return the shared <c>*LookupDto</c> types rather than new read models:
/// the lookup endpoints are consumed by the frontend, and reusing the exact DTOs keeps the JSON
/// responses byte-identical while still routing the queries through HRM.</para>
/// </summary>
public interface IHrmLookups
{
    /// <summary>Departments, optionally narrowed to one branch. A <c>null</c> branch is not filtered.</summary>
    Task<List<DepartmentLookupDto>> DepartmentsAsync(long? branchNo, CancellationToken cancellationToken = default);

    Task<List<DesignationLookupDto>> DesignationsAsync(long? branchNo, CancellationToken cancellationToken = default);

    Task<List<GradeLookupDto>> GradesAsync(long? branchNo, CancellationToken cancellationToken = default);

    Task<List<GradeStepLookupDto>> GradeStepsAsync(long? gradeNo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Active employees, optionally narrowed to one branch, with department and designation names
    /// and the composed full name already filled in. <c>UserNo</c> is left unset — that mapping
    /// lives in SYS and is applied by the caller.
    /// </summary>
    Task<List<EmployeeLookupDto>> EmployeesAsync(long? branchNo, CancellationToken cancellationToken = default);
}
