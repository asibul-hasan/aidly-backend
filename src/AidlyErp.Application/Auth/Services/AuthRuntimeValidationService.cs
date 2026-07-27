using System.Collections.Concurrent;
using AidlyErp.Application.Common.Exceptions;
using AidlyErp.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AidlyErp.Application.Auth.Services;

public interface IAuthRuntimeValidationService
{
    Task ValidateJwtContextAsync(long? userNo, long? companyNo, long? branchNo, long? tokenVersion,
                                 CancellationToken cancellationToken = default);
}

/// <summary>
/// Runtime guard invoked from the JWT pipeline — verifies that the principal's claims still
/// reflect reality (user active, branch membership alive, optimistic-lock <c>row_version</c>
/// unchanged). Port of <c>core.auth.service.AuthRuntimeValidationService</c>.
///
/// <para>Results are cached for 60 seconds keyed on (userNo, branchNo, tokenVersion) so this does
/// not become an N+1 database hit on every HTTP request. The cache is a process-wide singleton,
/// matching the Java field-level <c>ConcurrentHashMap</c>.</para>
/// </summary>
public class AuthRuntimeValidationService : IAuthRuntimeValidationService
{
    private const short Active = 1;
    private const short Deleted = 0;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMilliseconds(60_000);

    private static readonly ConcurrentDictionary<string, DateTimeOffset> ValidationCache = new();

    private readonly IApplicationDbContext _db;

    public AuthRuntimeValidationService(IApplicationDbContext db) => _db = db;

    public async Task ValidateJwtContextAsync(long? userNo, long? companyNo, long? branchNo, long? tokenVersion,
                                              CancellationToken cancellationToken = default)
    {
        if (userNo == null || branchNo == null)
        {
            throw new ValidationException("INVALID_TOKEN_CONTEXT");
        }

        var cacheKey = $"{userNo}:{branchNo}:{tokenVersion}";
        if (ValidationCache.TryGetValue(cacheKey, out var lastValidated)
            && DateTimeOffset.UtcNow - lastValidated < CacheTtl)
        {
            return; // Validated recently, skip DB queries
        }

        // User must still be active and undeleted; project the token version in the same read.
        var currentTokenVersion = await _db.Users
            .AsNoTracking()
            .Where(u => u.UserNo == userNo && u.IsActive == Active && u.IsDeleted == Deleted)
            .Select(u => (long?)u.TokenVersion)
            .FirstOrDefaultAsync(cancellationToken);

        if (currentTokenVersion == null)
        {
            throw new ValidationException("SESSION_CONTEXT_INACTIVE");
        }

        if (tokenVersion != null && currentTokenVersion != tokenVersion)
        {
            throw new ValidationException("TOKEN_VERSION_EXPIRED");
        }

        // Either branch membership OR a role in this branch is sufficient — a power user with
        // global access may not have an explicit user_branch row.
        var hasMembership = await _db.UserBranches
            .AsNoTracking()
            .AnyAsync(ub => ub.UserNo == userNo && ub.BranchNo == branchNo
                            && ub.IsActive == Active && ub.IsDeleted == Deleted, cancellationToken);

        if (!hasMembership)
        {
            // Java userHasAnyRoleInBranch: sys_user_branch joined to an active sys_role.
            var hasRole = await _db.UserBranches
                .AsNoTracking()
                .AnyAsync(ub => ub.UserNo == userNo && ub.BranchNo == branchNo
                                && ub.IsActive == Active && ub.IsDeleted == Deleted
                                && _db.Roles.Any(r => r.RoleNo == ub.RoleNo
                                                      && r.IsActive == Active && r.IsDeleted == Deleted),
                          cancellationToken);

            if (!hasRole)
            {
                throw new ValidationException("BRANCH_ACCESS_DENIED");
            }
        }

        ValidationCache[cacheKey] = DateTimeOffset.UtcNow;
    }
}
