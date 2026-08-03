using System.Collections.Concurrent;
using AidlyErp.Hrm.Contracts;
using AidlyErp.Sys.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AidlyErp.Sys.Application.Auth.Services;

/// <summary>The caller's employee identity — the keys the DEPARTMENT and EMPLOYEE data scopes filter on.</summary>
public readonly record struct UserScopeIdentity(long? EmployeeNo, long? DepartmentNo);

public interface ICurrentUserScopeResolver
{
    /// <summary>
    /// Resolves <c>sys_user.employee_no</c> and that employee's <c>department_no</c> for
    /// <paramref name="userNo"/>. Cached — see the implementation for why that is safe.
    /// </summary>
    Task<UserScopeIdentity> ResolveAsync(long userNo, CancellationToken cancellationToken = default);

    /// <summary>Drops the cache — call after an employee's department or user linkage changes.</summary>
    void Evict();
}

/// <summary>
/// Supplies the employee identity behind DEPARTMENT / EMPLOYEE data scopes.
///
/// <para><b>Why this is cached.</b> The RBAC decision itself is deliberately zero-DB (the
/// permissions ride in the JWT). Resolving an employee needs the database, so caching keeps the
/// common path free: an entry is read at most once per user per TTL, and only for users whose
/// role actually carries a narrowing scope — a BRANCH-scoped role never calls this at all.</para>
///
/// <para>The mapping is stable — a user's employee link is set at provisioning and a department
/// transfer is a rare HR event — so a short TTL trades a stale department for one request window
/// against a query on every request. <see cref="Evict"/> exists for the transfer case.</para>
/// </summary>
public class CurrentUserScopeResolver : ICurrentUserScopeResolver
{
    // 10 minutes, not one. The connection pool is small (8, shared by every module), and this
    // lookup runs in middleware BEFORE the controller takes its own connection — so a short TTL
    // turns a rare enrichment into recurring pool pressure on every form load. A user's employee
    // link is set once at provisioning and a department transfer is a rare HR event, so a stale
    // entry for a few minutes is harmless; Evict() covers the transfer case.
    private const long TtlMillis = 600_000;
    private const int CacheMax = 20_000;

    private static readonly ConcurrentDictionary<long, CacheEntry> Cache = new();

    private readonly ISysDbContext _db;
    private readonly IEmployeeDirectory _employees;

    public CurrentUserScopeResolver(ISysDbContext db, IEmployeeDirectory employees)
    {
        _db = db;
        _employees = employees;
    }

    public async Task<UserScopeIdentity> ResolveAsync(long userNo, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        if (Cache.TryGetValue(userNo, out var cached) && cached.ExpiresAt > now)
        {
            return cached.Identity;
        }

        // sys_user.employee_no is the link; 0 means "no employee behind this login".
        var employeeNo = await _db.Users
            .AsNoTracking()
            .Where(u => u.UserNo == userNo && u.IsDeleted == 0)
            .Select(u => (long?)u.EmployeeNo)
            .FirstOrDefaultAsync(cancellationToken);

        if (employeeNo is null or 0)
        {
            return Store(userNo, new UserScopeIdentity(null, null), now);
        }

        var employee = await _employees.FindAsync(employeeNo.Value, cancellationToken);

        return Store(userNo, new UserScopeIdentity(employeeNo, employee?.DepartmentNo), now);
    }

    public void Evict() => Cache.Clear();

    private static UserScopeIdentity Store(long userNo, UserScopeIdentity identity, long now)
    {
        if (Cache.Count > CacheMax) Cache.Clear();
        Cache[userNo] = new CacheEntry(identity, now + TtlMillis);
        return identity;
    }

    private readonly record struct CacheEntry(UserScopeIdentity Identity, long ExpiresAt);
}
