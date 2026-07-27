using AidlyErp.Application.Common.Exceptions;
using AidlyErp.Application.Common.Interfaces;
using AidlyErp.Application.Common.Security;
using AidlyErp.Application.Sys.Dto;
using AidlyErp.Domain.Sys;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AidlyErp.Application.Sys.Services;

public interface IRoleService
{
    Task<RoleDto> InsertAsync(RoleDto dto, CancellationToken cancellationToken = default);
    Task<RoleDto> UpdateAsync(long roleNo, RoleDto dto, CancellationToken cancellationToken = default);
    Task DeleteAsync(long roleNo, CancellationToken cancellationToken = default);
    Task<List<RoleDto>> GetListAsync(CancellationToken cancellationToken = default);
    Task<List<RoleLookupDto>> GetRoleLookupListAsync(CancellationToken cancellationToken = default);
    Task<RoleDto> GetDtlAsync(long roleNo, CancellationToken cancellationToken = default);
}

/// <summary>
/// Generic role CRUD behind <c>/api/v1/sys/roles</c>. Roles are company-scoped templates; the
/// branches they are published to live in <c>sys_role_branch</c>, where a single row with a
/// <c>null</c> branch means "all branches".
/// </summary>
public class RoleService : IRoleService
{
    private const short Deleted = 0;

    private readonly IApplicationDbContext _db;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICompanyBranchContext _ctx;
    private readonly ILogger<RoleService> _logger;

    public RoleService(IApplicationDbContext db, IUnitOfWork unitOfWork, ICompanyBranchContext ctx,
                       ILogger<RoleService> logger)
    {
        _db = db;
        _unitOfWork = unitOfWork;
        _ctx = ctx;
        _logger = logger;
    }

    public Task<RoleDto> InsertAsync(RoleDto dto, CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(async ct =>
        {
            if (dto.CompanyNo == null)
            {
                throw new ValidationException("company_no is required to create a role");
            }

            if (await _db.Roles.AnyAsync(r => r.CompanyNo == dto.CompanyNo && r.RoleId == dto.RoleId
                                              && r.IsDeleted == Deleted, ct))
            {
                throw new ValidationException("Role ID already exists in this company: " + dto.RoleId);
            }

            var entity = new Role { CompanyNo = dto.CompanyNo };
            Apply(dto, entity);

            _db.Roles.Add(entity);
            await _db.SaveChangesAsync(ct);

            await SaveBranchNosAsync(entity.RoleNo, dto.BranchNos, ct);

            _logger.LogInformation("Role inserted: roleNo={RoleNo}, roleId={RoleId}, companyNo={CompanyNo}, branches={Branches}",
                entity.RoleNo, entity.RoleId, entity.CompanyNo,
                dto.BranchNos == null || dto.BranchNos.Count == 0 ? "ALL" : string.Join(",", dto.BranchNos));

            var result = ToDto(entity);
            result.BranchNos = await LoadBranchNosAsync(entity.RoleNo, ct);
            return result;
        }, cancellationToken);

    public Task<RoleDto> UpdateAsync(long roleNo, RoleDto dto, CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(async ct =>
        {
            var entity = await LoadAsync(roleNo, ct);

            if (IsSystem(entity))
            {
                throw new ValidationException("System roles cannot be edited.");
            }

            Apply(dto, entity);
            await _db.SaveChangesAsync(ct);

            await SaveBranchNosAsync(roleNo, dto.BranchNos, ct);

            _logger.LogInformation("Role updated: roleNo={RoleNo}, branches={Branches}", roleNo,
                dto.BranchNos == null || dto.BranchNos.Count == 0 ? "ALL" : string.Join(",", dto.BranchNos));

            var result = ToDto(entity);
            result.BranchNos = await LoadBranchNosAsync(roleNo, ct);
            return result;
        }, cancellationToken);

    public Task DeleteAsync(long roleNo, CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(async ct =>
        {
            var entity = await LoadAsync(roleNo, ct);

            if (IsSystem(entity))
            {
                throw new ValidationException("System roles cannot be deleted.");
            }

            entity.PerformSoftDelete(_ctx.CurrentUserNo());
            await SoftDeleteBranchNosAsync(roleNo, ct);

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Role soft-deleted: roleNo={RoleNo}", roleNo);
        }, cancellationToken);

    public async Task<List<RoleDto>> GetListAsync(CancellationToken cancellationToken = default)
    {
        var companyNo = Company();

        var roles = await _db.Roles
            .AsNoTracking()
            .Where(r => r.CompanyNo == companyNo && r.IsDeleted == Deleted)
            .ToListAsync(cancellationToken);

        var roleNos = roles.Select(r => r.RoleNo).ToList();

        // One query for every role's branch publication, then group — not a query per role.
        var branchesByRole = roleNos.Count == 0
            ? new Dictionary<long, List<long>>()
            : (await _db.SysRoleBranches
                    .AsNoTracking()
                    .Where(rb => roleNos.Contains(rb.RoleNo) && rb.IsDeleted == Deleted && rb.BranchNo != null)
                    .ToListAsync(cancellationToken))
                .GroupBy(rb => rb.RoleNo)
                .ToDictionary(g => g.Key, g => g.Select(rb => rb.BranchNo!.Value).ToList());

        return roles.Select(r =>
        {
            var dto = ToDto(r);
            dto.BranchNos = branchesByRole.TryGetValue(r.RoleNo, out var list) ? list : new List<long>();
            return dto;
        }).ToList();
    }

    public async Task<List<RoleLookupDto>> GetRoleLookupListAsync(CancellationToken cancellationToken = default)
    {
        var companyNo = Company();

        return await _db.Roles
            .AsNoTracking()
            .Where(r => r.CompanyNo == companyNo && r.IsDeleted == Deleted)
            .Select(r => new RoleLookupDto
            {
                RoleNo = r.RoleNo,
                RoleId = r.RoleId,
                RoleName = r.RoleName,
                IsSystemRole = r.IsSystemRole,
                IsActive = r.IsActive
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<RoleDto> GetDtlAsync(long roleNo, CancellationToken cancellationToken = default)
    {
        var dto = ToDto(await LoadAsync(roleNo, cancellationToken));
        dto.BranchNos = await LoadBranchNosAsync(roleNo, cancellationToken);
        return dto;
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task<Role> LoadAsync(long roleNo, CancellationToken ct) =>
        await _db.Roles.FirstOrDefaultAsync(r => r.RoleNo == roleNo && r.IsDeleted == Deleted, ct)
        ?? throw new NotFoundException("Role not found: roleNo=" + roleNo);

    private static bool IsSystem(Role role) => role.IsSystemRole == 1;

    private long Company() =>
        _ctx.CompanyNo ?? throw new ValidationException("No active company in context");

    /// <summary>
    /// Replaces the role's branch publication wholesale. An empty/absent list is stored as a
    /// single row with a <c>null</c> branch, which means "all branches".
    /// </summary>
    private async Task SaveBranchNosAsync(long roleNo, List<long>? branchNos, CancellationToken ct)
    {
        var userNo = _ctx.CurrentUserNo();

        var existing = await _db.SysRoleBranches
            .Where(rb => rb.RoleNo == roleNo && rb.IsDeleted == Deleted)
            .ToListAsync(ct);

        foreach (var rb in existing) rb.PerformSoftDelete(userNo);

        var cleaned = branchNos?.Distinct().ToList() ?? new List<long>();

        if (cleaned.Count == 0)
        {
            _db.SysRoleBranches.Add(new SysRoleBranch { RoleNo = roleNo, BranchNo = null });
        }
        else
        {
            foreach (var branchNo in cleaned)
            {
                _db.SysRoleBranches.Add(new SysRoleBranch { RoleNo = roleNo, BranchNo = branchNo });
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Explicit branch grants only — the "all branches" null row is not reported.</summary>
    private async Task<List<long>> LoadBranchNosAsync(long roleNo, CancellationToken ct) =>
        await _db.SysRoleBranches
            .AsNoTracking()
            .Where(rb => rb.RoleNo == roleNo && rb.IsDeleted == Deleted && rb.BranchNo != null)
            .Select(rb => rb.BranchNo!.Value)
            .ToListAsync(ct);

    private async Task SoftDeleteBranchNosAsync(long roleNo, CancellationToken ct)
    {
        var userNo = _ctx.CurrentUserNo();

        var rows = await _db.SysRoleBranches
            .Where(rb => rb.RoleNo == roleNo && rb.IsDeleted == Deleted)
            .ToListAsync(ct);

        foreach (var rb in rows) rb.PerformSoftDelete(userNo);
    }

    private static void Apply(RoleDto dto, Role e)
    {
        if (dto.CompanyNo != null) e.CompanyNo = dto.CompanyNo;
        if (dto.RoleId != null) e.RoleId = dto.RoleId;
        if (dto.RoleName != null) e.RoleName = dto.RoleName;
        if (dto.RoleDesc != null) e.RoleDesc = dto.RoleDesc;
        if (dto.IsSystemRole != null) e.IsSystemRole = dto.IsSystemRole.Value;
        if (dto.IsActive != null) e.IsActive = dto.IsActive.Value;
    }

    private static RoleDto ToDto(Role e) => new()
    {
        RoleNo = e.RoleNo,
        CompanyNo = e.CompanyNo,
        RoleId = e.RoleId,
        RoleName = e.RoleName,
        RoleDesc = e.RoleDesc,
        IsSystemRole = e.IsSystemRole,
        IsActive = e.IsActive,
        RowVersion = e.RowVersion
    };
}
