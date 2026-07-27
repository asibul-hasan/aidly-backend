namespace AidlyErp.Application.Common.Security;

/// <summary>
/// Request-scoped holder for the resolved permission of the active form (SYS re-plan §3.5),
/// set by the RBAC authorization filter after authorization passes.
///
/// <para>Exposes the <c>record_filter</c> dimension so service/repository code can narrow a
/// list to the caller's own records (<c>created_by = OwnerUserNo</c>) when the role grants
/// only OWN visibility:</para>
/// <code>
/// if (permissionContext.IsOwnOnly)
///     query = query.Where(x =&gt; x.CreatedBy == permissionContext.OwnerUserNo);
/// </code>
///
/// <para>Until <c>sys_role_permission.record_filter</c> is wired through the RBAC resolver, the
/// filter defaults to <see cref="CurrentPermissionContext.RecordFilterAll"/> (no narrowing).</para>
/// </summary>
public interface ICurrentPermissionContext
{
    string? FormId { get; }

    int RecordFilter { get; }

    long? OwnerUserNo { get; }

    /// <summary>True when the active role grants only OWN-record visibility for the current form.</summary>
    bool IsOwnOnly { get; }

    void Set(string? formId, int? recordFilter, long? ownerUserNo);

    void Clear();
}

public sealed class CurrentPermissionContext : ICurrentPermissionContext
{
    public const int RecordFilterAll = 1;
    public const int RecordFilterOwn = 2;

    private int? _recordFilter;

    public string? FormId { get; private set; }

    public int RecordFilter => _recordFilter ?? RecordFilterAll;

    public long? OwnerUserNo { get; private set; }

    public bool IsOwnOnly => RecordFilter == RecordFilterOwn;

    public void Set(string? formId, int? recordFilter, long? ownerUserNo)
    {
        FormId = formId;
        _recordFilter = recordFilter ?? RecordFilterAll;
        OwnerUserNo = ownerUserNo;
    }

    public void Clear()
    {
        FormId = null;
        _recordFilter = null;
        OwnerUserNo = null;
    }
}
