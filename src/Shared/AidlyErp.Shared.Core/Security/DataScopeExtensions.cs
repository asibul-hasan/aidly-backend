using System.Linq.Expressions;

namespace AidlyErp.Shared.Core.Security;

/// <summary>
/// Row-level security applied on top of the company/branch global query filter.
///
/// <para>Where the tenant filter answers <i>"which company's data is this?"</i>, the data scope
/// answers <i>"how much of that branch may this role see?"</i>:</para>
/// <list type="bullet">
///   <item><b>BRANCH</b> — everything the tenant filter already allows. No extra predicate.</item>
///   <item><b>DEPARTMENT</b> — rows whose <c>department_no</c> is the caller's department.</item>
///   <item><b>EMPLOYEE</b> — rows whose <c>employee_no</c> is the caller's employee record.</item>
/// </list>
///
/// <para><b>Opt-in by interface.</b> A scope only narrows an entity that declares the matching
/// column via <see cref="IDepartmentScopedEntity"/> / <see cref="IEmployeeOwnedEntity"/>. An
/// entity with no such column (a currency, a menu) is returned untouched — narrowing it would
/// be meaningless, and silently returning zero rows is worse than not filtering.</para>
///
/// <para><b>Fails closed.</b> When a scope applies but the caller's employee identity could not
/// be resolved, the query is narrowed to nothing rather than left open.</para>
/// </summary>
public static class DataScopeExtensions
{
    /// <summary>
    /// Narrows <paramref name="query"/> to the rows the caller's resolved data scope permits.
    ///
    /// <para>Call this on list/read queries in a service, after the standard tenant-scoped
    /// predicate:</para>
    /// <code>
    /// var rows = await _db.LeaveApplications
    ///     .AsNoTracking()
    ///     .Where(x =&gt; x.IsDeleted == 0)
    ///     .ApplyDataScope(_permissionContext)
    ///     .ToListAsync(ct);
    /// </code>
    /// </summary>
    public static IQueryable<T> ApplyDataScope<T>(this IQueryable<T> query, ICurrentPermissionContext permissionContext)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(permissionContext);

        return permissionContext.DataScope switch
        {
            DataScopeConstants.Department => ApplyDepartmentScope(query, permissionContext.OwnerDepartmentNo),
            DataScopeConstants.Employee => ApplyEmployeeScope(query, permissionContext.OwnerEmployeeNo),

            // BRANCH (and anything unrecognised, via Normalize) adds no predicate: the company +
            // branch global query filter is already the branch boundary.
            _ => query
        };
    }

    private static IQueryable<T> ApplyDepartmentScope<T>(IQueryable<T> query, long? departmentNo) where T : class
    {
        if (!typeof(IDepartmentScopedEntity).IsAssignableFrom(typeof(T))) return query;
        if (departmentNo == null) return query.Where(_ => false);

        return query.Where(BuildEquals<T>(nameof(IDepartmentScopedEntity.DepartmentNo), departmentNo.Value));
    }

    private static IQueryable<T> ApplyEmployeeScope<T>(IQueryable<T> query, long? employeeNo) where T : class
    {
        if (!typeof(IEmployeeOwnedEntity).IsAssignableFrom(typeof(T))) return query;
        if (employeeNo == null) return query.Where(_ => false);

        return query.Where(BuildEquals<T>(nameof(IEmployeeOwnedEntity.EmployeeNo), employeeNo.Value));
    }

    /// <summary>
    /// Builds <c>x =&gt; x.{property} == value</c> against the CONCRETE entity type.
    ///
    /// <para>This is why the predicate is composed by hand instead of written as a lambda: an
    /// interface cast inside an expression tree — <c>x =&gt; ((IEmployeeOwnedEntity)x).EmployeeNo
    /// == value</c> — is not translatable by EF Core and throws at query time. Binding the member
    /// on <typeparamref name="T"/> itself produces an ordinary column comparison.</para>
    /// </summary>
    private static Expression<Func<T, bool>> BuildEquals<T>(string propertyName, long value)
    {
        var parameter = Expression.Parameter(typeof(T), "x");

        // Bind the CONCRETE property, not the interface member. Entities implement these
        // interfaces explicitly (their own columns are non-nullable long), so the interface
        // member itself is a computed property EF cannot translate — the backing column can.
        var declared = typeof(T).GetProperty(propertyName)
            ?? throw new InvalidOperationException(
                $"{typeof(T).Name} is marked for '{propertyName}' data scoping but exposes no public " +
                $"'{propertyName}' property to filter on.");

        var property = Expression.Property(parameter, declared);

        // Match the constant to the column's own type so long and long? both compare cleanly.
        var constant = property.Type == typeof(long?)
            ? Expression.Constant((long?)value, typeof(long?))
            : Expression.Constant(value, typeof(long));

        return Expression.Lambda<Func<T, bool>>(Expression.Equal(property, constant), parameter);
    }
}

/// <summary>
/// Marks an entity that carries a <c>department_no</c> the DEPARTMENT data scope can filter on.
/// </summary>
public interface IDepartmentScopedEntity
{
    long? DepartmentNo { get; }
}

/// <summary>
/// Marks an entity that belongs to an employee (<c>employee_no</c>) — the column the EMPLOYEE
/// data scope filters on.
///
/// <para>Deliberately NOT <c>created_by</c>: an HR officer filing leave on an employee's behalf
/// would otherwise make that leave invisible to the employee it belongs to, while showing it to
/// the officer. Ownership here means "whose record is this", not "who typed it in".</para>
/// </summary>
public interface IEmployeeOwnedEntity
{
    long? EmployeeNo { get; }
}
