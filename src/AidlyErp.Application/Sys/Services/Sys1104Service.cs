using AidlyErp.Application.Auth.Services;
using AidlyErp.Application.Common.Exceptions;
using AidlyErp.Application.Common.Interfaces;
using AidlyErp.Application.Common.Security;
using AidlyErp.Application.Sys.Dto;
using AidlyErp.Domain.Sys;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AidlyErp.Application.Sys.Services;

public interface ISys1104Service
{
    Task<List<Sys1104UserOptionDto>> GetUsersAsync(CancellationToken cancellationToken = default);

    Task<List<Sys1104OptionDto>> GetBranchesAsync(CancellationToken cancellationToken = default);

    Task<List<Sys1104RoleOptionDto>> GetRolesAsync(CancellationToken cancellationToken = default);

    Task<List<Sys1104MappingDto>> GetMappingsAsync(long userNo, CancellationToken cancellationToken = default);

    Task<List<Sys1104MappingDto>> SaveMappingsAsync(long userNo, List<Sys1104MappingDto>? rows,
                                                    CancellationToken cancellationToken = default);

    Task<int> BulkSaveMappingsAsync(List<long>? userNos, List<Sys1104MappingDto>? rows,
                                    CancellationToken cancellationToken = default);
}

/// <summary>
/// SYS1104 User-Branch Mapping — assigns a user to branches with a role per branch
/// (<c>sys_user_branch</c>, which carries <c>role_no</c>). All scoped to the active company.
/// </summary>
public class Sys1104Service : ISys1104Service
{
    private const short Deleted = 0;

    private readonly IApplicationDbContext _db;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICompanyBranchContext _ctx;
    private readonly IRbacAuthorizationService _rbac;
    private readonly ILogger<Sys1104Service> _logger;

    public Sys1104Service(IApplicationDbContext db, IUnitOfWork unitOfWork, ICompanyBranchContext ctx,
                          IRbacAuthorizationService rbac, ILogger<Sys1104Service> logger)
    {
        _db = db;
        _unitOfWork = unitOfWork;
        _ctx = ctx;
        _rbac = rbac;
        _logger = logger;
    }

    // ─── Options ─────────────────────────────────────────────────────────────

    /// <summary>Scoped to the active company in SQL — never load every user and filter in memory.</summary>
    public async Task<List<Sys1104UserOptionDto>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        var companyNo = ActiveCompany();

        return await _db.Users
            .AsNoTracking()
            .Where(u => u.CompanyNo == companyNo && u.IsDeleted == Deleted)
            .OrderBy(u => u.UserId)
            .Select(u => new Sys1104UserOptionDto(u.UserNo, u.UserId, u.UserName, u.EmployeeNo, u.AccessScope))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Sys1104OptionDto>> GetBranchesAsync(CancellationToken cancellationToken = default)
    {
        var companyNo = ActiveCompany();

        return await _db.Branches
            .AsNoTracking()
            .Where(b => b.CompanyNo == companyNo && b.IsDeleted == Deleted)
            .OrderBy(b => b.BranchNo)
            .Select(b => new Sys1104OptionDto(b.BranchNo, b.BranchName))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Sys1104RoleOptionDto>> GetRolesAsync(CancellationToken cancellationToken = default)
    {
        var companyNo = ActiveCompany();

        var roles = await _db.Roles
            .AsNoTracking()
            .Where(r => r.CompanyNo == companyNo && r.IsDeleted == Deleted)
            .OrderBy(r => r.RoleNo)
            .ToListAsync(cancellationToken);

        var roleNos = roles.Select(r => r.RoleNo).ToList();

        // One query for every role's branch publication, then group in memory.
        var branchesByRole = roleNos.Count == 0
            ? new Dictionary<long, List<long>>()
            : (await _db.SysRoleBranches
                    .AsNoTracking()
                    .Where(rb => roleNos.Contains(rb.RoleNo) && rb.IsDeleted == Deleted)
                    .ToListAsync(cancellationToken))
                .GroupBy(rb => rb.RoleNo)
                .ToDictionary(g => g.Key, g => g.Select(rb => rb.BranchNo ?? 0L).ToList());

        return roles
            .Select(r => new Sys1104RoleOptionDto(
                r.RoleNo,
                r.RoleName,
                branchesByRole.TryGetValue(r.RoleNo, out var branches) ? branches : new List<long>()))
            .ToList();
    }

    // ─── Mappings ────────────────────────────────────────────────────────────

    public async Task<List<Sys1104MappingDto>> GetMappingsAsync(long userNo,
                                                                CancellationToken cancellationToken = default)
    {
        await LoadUserAsync(userNo, cancellationToken);

        var branchNames = (await GetBranchesAsync(cancellationToken))
            .ToDictionary(o => o.Value, o => o.Label);

        var roleNames = (await GetRolesAsync(cancellationToken))
            .ToDictionary(o => o.Value, o => o.Label);

        var rows = await _db.UserBranches
            .AsNoTracking()
            .Where(ub => ub.UserNo == userNo && ub.IsDeleted == Deleted)
            .ToListAsync(cancellationToken);

        return rows.Select(ub => ToDto(ub, branchNames, roleNames)).ToList();
    }

    public Task<List<Sys1104MappingDto>> SaveMappingsAsync(long userNo, List<Sys1104MappingDto>? rows,
                                                           CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(async ct =>
        {
            await LoadUserAsync(userNo, ct);

            if (rows == null)
            {
                return await GetMappingsAsync(userNo, ct);
            }

            // ALL rows, including soft-deleted ones. A previously-removed (user, branch) slot must
            // be REVIVED rather than re-inserted, otherwise the insert violates uq_user_branch.
            var existing = await _db.UserBranches
                .IgnoreQueryFilters()
                .Where(ub => ub.UserNo == userNo)
                .ToListAsync(ct);

            var byBranch = new Dictionary<long, UserBranch>();
            foreach (var ub in existing) byBranch.TryAdd(ub.BranchNo, ub);

            var currentUser = _ctx.CurrentUserNo();
            var kept = new HashSet<long>();
            var defaultTaken = false;

            // Batch-validate every referenced role in ONE query rather than per row.
            var requestedRoleNos = rows.Where(r => r.RoleNo != null).Select(r => r.RoleNo!.Value).Distinct().ToList();

            var validRoleNos = requestedRoleNos.Count == 0
                ? new HashSet<long>()
                : (await _db.Roles
                        .AsNoTracking()
                        .Where(r => requestedRoleNos.Contains(r.RoleNo) && r.IsDeleted == Deleted)
                        .Select(r => r.RoleNo)
                        .ToListAsync(ct))
                    .ToHashSet();

            foreach (var row in rows)
            {
                if (row.BranchNo == null || row.RoleNo == null)
                {
                    throw new ValidationException("Each mapping needs a branch and a role");
                }

                if (!validRoleNos.Contains(row.RoleNo.Value))
                {
                    throw new NotFoundException("Role not found: roleNo=" + row.RoleNo);
                }

                if (!byBranch.TryGetValue(row.BranchNo.Value, out var ub))
                {
                    ub = new UserBranch
                    {
                        UserNo = userNo,
                        BranchNo = row.BranchNo.Value
                    };
                    _db.UserBranches.Add(ub);
                    byBranch[row.BranchNo.Value] = ub;
                }

                ub.RoleNo = row.RoleNo.Value;

                // Only the first row asking to be default wins.
                var wantsDefault = row.IsDefault == 1 && !defaultTaken;
                ub.IsDefault = (short)(wantsDefault ? 1 : 0);
                if (wantsDefault) defaultTaken = true;

                ub.IsActive = row.IsActive ?? 1;
                ub.IsDeleted = Deleted;   // revives a previously soft-deleted slot

                if (ub.RowVersion <= 0) ub.RowVersion = 1L;

                kept.Add(row.BranchNo.Value);
            }

            // Soft-delete mappings the user no longer has.
            foreach (var ub in existing)
            {
                if (!kept.Contains(ub.BranchNo) && ub.IsDeleted == Deleted)
                {
                    ub.PerformSoftDelete(currentUser);
                }
            }

            await _db.SaveChangesAsync(ct);

            // Guarantee one default when at least one mapping exists.
            if (!defaultTaken && kept.Count > 0)
            {
                var live = await _db.UserBranches
                    .Where(ub => ub.UserNo == userNo && ub.IsDeleted == Deleted)
                    .ToListAsync(ct);

                if (live.Count > 0)
                {
                    live[0].IsDefault = 1;
                    await _db.SaveChangesAsync(ct);
                }
            }

            // Branch-role assignments changed → drop the RBAC cache so access updates immediately.
            _rbac.EvictPermissionCache();

            _logger.LogInformation("Saved {Count} branch-role mapping(s) for userNo={UserNo}", rows.Count, userNo);

            return await GetMappingsAsync(userNo, ct);
        }, cancellationToken);

    public Task<int> BulkSaveMappingsAsync(List<long>? userNos, List<Sys1104MappingDto>? rows,
                                           CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(async ct =>
        {
            if (userNos == null || userNos.Count == 0)
            {
                throw new ValidationException("At least one user is required");
            }

            var count = 0;

            foreach (var userNo in userNos.Distinct())
            {
                await SaveMappingsAsync(userNo, rows, ct);
                count++;
            }

            return count;
        }, cancellationToken);

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task<User> LoadUserAsync(long userNo, CancellationToken ct) =>
        await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserNo == userNo && u.IsDeleted == Deleted, ct)
        ?? throw new NotFoundException("User not found: userNo=" + userNo);

    private long ActiveCompany() =>
        _ctx.CompanyNo ?? throw new ValidationException("No active company in context");

    private static Sys1104MappingDto ToDto(UserBranch ub, IReadOnlyDictionary<long, string?> branchNames,
                                           IReadOnlyDictionary<long, string?> roleNames) => new()
    {
        UserBranchNo = ub.UserBranchNo,
        BranchNo = ub.BranchNo,
        BranchName = branchNames.TryGetValue(ub.BranchNo, out var branchName) ? branchName : null,
        RoleNo = ub.RoleNo,
        RoleName = roleNames.TryGetValue(ub.RoleNo, out var roleName) ? roleName : null,
        IsDefault = ub.IsDefault,
        IsActive = ub.IsActive
    };
}
