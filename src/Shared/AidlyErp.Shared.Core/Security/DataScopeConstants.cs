namespace AidlyErp.Shared.Core.Security;

/// <summary>
/// Defines the available Row-Level Security Data Scopes for the ERP.
/// Mapped to the sys_role_permission.data_scope column.
/// </summary>
public static class DataScopeConstants
{
    /// <summary>
    /// User sees every record in their branch — i.e. NO narrowing beyond the tenant
    /// (company + branch) query filter that already applies to every entity.
    /// This is the default so that adding row-level security to an existing install
    /// does not silently strip access from roles configured before the feature existed.
    /// </summary>
    public const short Branch = 1;

    /// <summary>User sees records belonging to their own department.</summary>
    public const short Department = 2;

    /// <summary>User sees only records that belong to them (their employee record).</summary>
    public const short Employee = 3;

    /// <summary>The scope applied when none is configured or the token predates the feature.</summary>
    public const short Default = Branch;

    /// <summary>Coerces any stored/claimed value to a known scope, falling back to <see cref="Default"/>.</summary>
    public static short Normalize(short? value) => value switch
    {
        Department => Department,
        Employee => Employee,
        _ => Default
    };
}
