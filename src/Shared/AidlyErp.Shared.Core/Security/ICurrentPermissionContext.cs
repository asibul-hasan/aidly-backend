namespace AidlyErp.Shared.Core.Security;

/// <summary>
/// Request-scoped holder for the resolved permission of the active form (SYS re-plan §3.5),
/// set by the RBAC authorization middleware after authorization passes.
///
/// <para>Carries the two row-level narrowing dimensions:</para>
/// <list type="bullet">
///   <item><c>record_filter</c> — ALL vs OWN (<c>created_by = OwnerUserNo</c>).</item>
///   <item><c>data_scope</c> — BRANCH / DEPARTMENT / EMPLOYEE, applied by
///   <see cref="DataScopeExtensions.ApplyDataScope{T}"/>.</item>
/// </list>
///
/// <para>Both default to the widest setting when unresolved, so a token issued before the
/// feature shipped behaves exactly as it did before.</para>
/// </summary>
public interface ICurrentPermissionContext
{
    string? FormId { get; }

    int RecordFilter { get; }

    /// <summary>Resolved <c>sys_role_permission.data_scope</c> for the active form.</summary>
    short DataScope { get; }

    /// <summary><c>sys_user.user_no</c> of the caller — the owner key for OWN-record filtering.</summary>
    long? OwnerUserNo { get; }

    /// <summary><c>sys_user.employee_no</c> of the caller — the owner key for EMPLOYEE scope.</summary>
    long? OwnerEmployeeNo { get; }

    /// <summary>The caller's <c>hrm_employee.department_no</c> — the key for DEPARTMENT scope.</summary>
    long? OwnerDepartmentNo { get; }

    /// <summary>True when the active role grants only OWN-record visibility for the current form.</summary>
    bool IsOwnOnly { get; }

    void Set(string? formId, int? recordFilter, short? dataScope, long? ownerUserNo);

    /// <summary>
    /// Attaches the caller's employee identity, resolved lazily and only when a query actually
    /// needs DEPARTMENT or EMPLOYEE narrowing (see <c>ICurrentUserScopeResolver</c>).
    /// </summary>
    void SetOwnerIdentity(long? employeeNo, long? departmentNo);

    void Clear();
}

public sealed class CurrentPermissionContext : ICurrentPermissionContext
{
    public const int RecordFilterAll = 1;
    public const int RecordFilterOwn = 2;

    private int? _recordFilter;
    private short? _dataScope;

    public string? FormId { get; private set; }

    public int RecordFilter => _recordFilter ?? RecordFilterAll;

    public short DataScope => DataScopeConstants.Normalize(_dataScope);

    public long? OwnerUserNo { get; private set; }

    public long? OwnerEmployeeNo { get; private set; }

    public long? OwnerDepartmentNo { get; private set; }

    /// <summary>
    /// OWN-record visibility comes from <c>record_filter</c> only. <c>data_scope</c> is a separate,
    /// broader dimension handled by <see cref="DataScopeExtensions"/> — folding EMPLOYEE scope in
    /// here would make every <c>IsOwnOnly</c> caller silently apply a <c>created_by</c> filter on
    /// top of the employee filter, which is not the same set of rows.
    /// </summary>
    public bool IsOwnOnly => RecordFilter == RecordFilterOwn;

    public void Set(string? formId, int? recordFilter, short? dataScope, long? ownerUserNo)
    {
        FormId = formId;
        _recordFilter = recordFilter ?? RecordFilterAll;
        _dataScope = DataScopeConstants.Normalize(dataScope);
        OwnerUserNo = ownerUserNo;
    }

    public void SetOwnerIdentity(long? employeeNo, long? departmentNo)
    {
        OwnerEmployeeNo = employeeNo;
        OwnerDepartmentNo = departmentNo;
    }

    public void Clear()
    {
        FormId = null;
        _recordFilter = null;
        _dataScope = null;
        OwnerUserNo = null;
        OwnerEmployeeNo = null;
        OwnerDepartmentNo = null;
    }
}
