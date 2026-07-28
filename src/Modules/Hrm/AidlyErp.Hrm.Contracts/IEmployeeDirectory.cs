namespace AidlyErp.Hrm.Contracts;

/// <summary>
/// Public read model of an employee. Deliberately not the <c>HrmEmployee</c> entity — that type is
/// internal to HRM and must not leak across the module boundary, or every consumer becomes coupled
/// to HRM's schema.
/// </summary>
public sealed record EmployeeInfo
{
    public required long EmployeeNo { get; init; }
    public required string EmployeeId { get; init; }
    public string? FirstName { get; init; }
    public string? MiddleName { get; init; }
    public string? LastName { get; init; }
    public long? DepartmentNo { get; init; }
    public string? DepartmentName { get; init; }
    public long? DesignationNo { get; init; }
    public string? DesignationName { get; init; }
    public short? IsActive { get; init; }
    public short IsCreateUser { get; init; }
    public long? BranchNo { get; init; }
    public DateTime JoiningDate { get; init; }
    public string? OfficialEmail { get; init; }
    public string? MobileNumber { get; init; }

    /// <summary>Name parts joined, falling back to the employee id when all of them are blank.</summary>
    public string FullName
    {
        get
        {
            var name = string.Join(' ',
                new[] { FirstName, MiddleName, LastName }.Where(p => !string.IsNullOrWhiteSpace(p))).Trim();
            return string.IsNullOrWhiteSpace(name) ? EmployeeId : name;
        }
    }
}

/// <summary>
/// The only way another module may reach employee data — the synchronous contract call described
/// in the inter-module communication rules. Consumers get this interface; the implementation and
/// <c>HrmDbContext</c> stay inside HRM.
/// </summary>
public interface IEmployeeDirectory
{
    /// <summary>
    /// Active-filtered employee list ordered by employee id, with department and designation names
    /// resolved. Pass <c>null</c> for <paramref name="isActive"/> to include inactive employees.
    /// </summary>
    Task<IReadOnlyList<EmployeeInfo>> ListAsync(short? isActive, CancellationToken cancellationToken = default);

    /// <summary>Single employee with department and designation names resolved; <c>null</c> when not found.</summary>
    Task<EmployeeInfo?> FindAsync(long employeeNo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Display names for a set of employees, in one query. Used where a caller holds employee
    /// numbers (branch managers, approvers) and needs only labels.
    /// </summary>
    Task<IReadOnlyDictionary<long, string>> GetNamesAsync(IReadOnlyCollection<long> employeeNos,
                                                          CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that the employee now has a login account (<c>hrm_employee.is_create_user = 1</c>).
    /// Idempotent — a no-op when the flag is already set.
    ///
    /// <para>This commits in HRM's own transaction, not the caller's. Provisioning a user and
    /// flagging the employee are therefore no longer one atomic unit; if the flag write fails the
    /// user still exists. The flag is derived state that can be recomputed from
    /// <c>sys_user.employee_no</c>, so the exposure is a stale checkbox rather than lost data —
    /// but if that ever needs to be atomic, it should move to an integration event on the
    /// outbox.</para>
    /// </summary>
    Task MarkHasUserAsync(long employeeNo, CancellationToken cancellationToken = default);
}
