using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Abstractions;
using AidlyErp.Shared.Contracts;
using AidlyErp.Sys.Contracts;
using AidlyErp.Sys.Application.Interfaces;
using AidlyErp.Hrm.Contracts;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Sys.Application.Dto;
using AidlyErp.Sys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AidlyErp.Sys.Application.Services;

public interface IUserService
{
    Task<UserDto> InsertAsync(UserDto dto, CancellationToken cancellationToken = default);
    Task<UserDto> UpdateAsync(long userNo, UserDto dto, CancellationToken cancellationToken = default);
    Task DeleteAsync(long userNo, CancellationToken cancellationToken = default);
    Task<List<UserDto>> GetListAsync(string? search, short? isActive, short? isLocked,
                                     CancellationToken cancellationToken = default);
    Task<List<UserSummaryDto>> GetSummaryListAsync(string? search, short? isActive,
                                                   CancellationToken cancellationToken = default);
    Task<List<UserRoleDto>> GetUserRolesAsync(long userNo, CancellationToken cancellationToken = default);
    Task<UserDto> GetDtlAsync(long userNo, CancellationToken cancellationToken = default);
    Task<UserDto> GetByEmployeeNoAsync(long employeeNo, CancellationToken cancellationToken = default);
    Task<UserDto> SetLockedAsync(long userNo, short locked, CancellationToken cancellationToken = default);
    Task ResetPasswordAsync(long userNo, string newPassword, CancellationToken cancellationToken = default);
}

/// <summary>
/// Generic user CRUD behind <c>/api/v1/sys/users</c>. Every user must be an employee — the
/// login row is keyed to <c>employee_no</c> and enriched from the HRM employee record on read.
/// </summary>
public class UserService : IUserService
{
    private const short Deleted = 0;

    private readonly ISysDbContext _db;
    private readonly IUnitOfWork<ISysDbContext> _unitOfWork;
    private readonly ICompanyBranchContext _ctx;
    private readonly ILogger<UserService> _logger;

    public UserService(ISysDbContext db, IUnitOfWork<ISysDbContext> unitOfWork, ICompanyBranchContext ctx,
                       ILogger<UserService> logger)
    {
        _db = db;
        _unitOfWork = unitOfWork;
        _ctx = ctx;
        _logger = logger;
    }

    public Task<UserDto> InsertAsync(UserDto dto, CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(async ct =>
        {
            if (string.IsNullOrWhiteSpace(dto.UserId)) throw new ValidationException("userId is required");

            if (await _db.Users.AnyAsync(u => u.UserId == dto.UserId && u.IsDeleted == Deleted, ct))
            {
                throw new ValidationException("User ID already exists: " + dto.UserId);
            }

            if (dto.EmployeeNo != null
                && await _db.Users.AnyAsync(u => u.EmployeeNo == dto.EmployeeNo && u.IsDeleted == Deleted, ct))
            {
                throw new ValidationException("Another user already linked to employeeNo=" + dto.EmployeeNo);
            }

            if (dto.EmployeeNo == null)
            {
                throw new ValidationException("employee_no is required (a user must be an employee)");
            }

            var entity = new User
            {
                // user_no mirrors employee_no — the two are the same identity.
                UserNo = dto.UserNo ?? dto.EmployeeNo.Value,
                EmployeeNo = dto.EmployeeNo.Value,
                UserId = dto.UserId
            };

            Apply(dto, entity);

            if (!string.IsNullOrWhiteSpace(dto.Password))
            {
                entity.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
                entity.PasswordChangedAt = DateTime.UtcNow;
                entity.MustChangePassword = 1;
            }

            entity.CompanyNo = dto.CompanyNo ?? _ctx.CompanyNo;
            entity.AccessScope = dto.AccessScope ?? 1;
            entity.DefaultBranchNo = dto.DefaultBranchNo;
            entity.IsActive = dto.IsActive ?? 1;
            entity.IsLocked = dto.IsLocked ?? 0;
            entity.FailedLoginCount = dto.FailedLoginCount ?? 0;

            _db.Users.Add(entity);
            await _db.SaveChangesAsync(ct);

            await ReplaceRoleMappingsAsync(entity.UserNo, dto.RoleMappings, ct);

            _logger.LogInformation("User inserted: userNo={UserNo}, userId={UserId}", entity.UserNo, entity.UserId);
            return await EnrichAsync(entity, ct);
        }, cancellationToken);

    public Task<UserDto> UpdateAsync(long userNo, UserDto dto, CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(async ct =>
        {
            var entity = await LoadAsync(userNo, ct);

            if (!string.IsNullOrWhiteSpace(dto.UserId) && dto.UserId != entity.UserId)
            {
                if (await _db.Users.AnyAsync(u => u.UserId == dto.UserId && u.IsDeleted == Deleted, ct))
                {
                    throw new ValidationException("User ID already exists: " + dto.UserId);
                }

                entity.UserId = dto.UserId;
            }

            Apply(dto, entity);
            await _db.SaveChangesAsync(ct);

            await ReplaceRoleMappingsAsync(entity.UserNo, dto.RoleMappings, ct);

            return await EnrichAsync(entity, ct);
        }, cancellationToken);

    public Task DeleteAsync(long userNo, CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(async ct =>
        {
            var entity = await LoadAsync(userNo, ct);
            entity.PerformSoftDelete(_ctx.CurrentUserNo());

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("User soft-deleted: userNo={UserNo}", userNo);
        }, cancellationToken);

    public async Task<List<UserDto>> GetListAsync(string? search, short? isActive, short? isLocked,
                                                  CancellationToken cancellationToken = default)
    {
        var query = _db.Users.AsNoTracking().Where(u => u.IsDeleted == Deleted);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(u => u.UserId.ToLower().Contains(term)
                                     || (u.UserName != null && u.UserName.ToLower().Contains(term)));
        }

        if (isActive != null) query = query.Where(u => u.IsActive == isActive);
        if (isLocked != null) query = query.Where(u => u.IsLocked == isLocked);

        var users = await query.OrderBy(u => u.UserId).ToListAsync(cancellationToken);

        var result = new List<UserDto>(users.Count);
        foreach (var user in users) result.Add(await EnrichAsync(user, cancellationToken));

        return result;
    }

    public async Task<List<UserSummaryDto>> GetSummaryListAsync(string? search, short? isActive,
                                                                CancellationToken cancellationToken = default)
    {
        var query = _db.Users.AsNoTracking().Where(u => u.IsDeleted == Deleted);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(u => u.UserId.ToLower().Contains(term)
                                     || (u.UserName != null && u.UserName.ToLower().Contains(term)));
        }

        if (isActive != null) query = query.Where(u => u.IsActive == isActive);

        return await query
            .OrderBy(u => u.UserId)
            .Select(u => new UserSummaryDto
            {
                UserNo = u.UserNo,
                UserId = u.UserId,
                UserName = u.UserName,
                EmployeeNo = u.EmployeeNo,
                IsActive = u.IsActive,
                IsLocked = u.IsLocked
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<List<UserRoleDto>> GetUserRolesAsync(long userNo, CancellationToken cancellationToken = default)
    {
        var rows = await _db.UserBranches
            .AsNoTracking()
            .Where(ub => ub.UserNo == userNo && ub.IsDeleted == Deleted)
            .ToListAsync(cancellationToken);

        return await ToRoleDtosAsync(rows, cancellationToken);
    }

    public async Task<UserDto> GetDtlAsync(long userNo, CancellationToken cancellationToken = default) =>
        await EnrichAsync(await LoadAsync(userNo, cancellationToken), cancellationToken);

    public async Task<UserDto> GetByEmployeeNoAsync(long employeeNo, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users
                       .FirstOrDefaultAsync(u => u.EmployeeNo == employeeNo && u.IsDeleted == Deleted, cancellationToken)
                   ?? throw new NotFoundException("No user linked to employeeNo=" + employeeNo);

        return await EnrichAsync(user, cancellationToken);
    }

    public Task<UserDto> SetLockedAsync(long userNo, short locked, CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(async ct =>
        {
            var entity = await LoadAsync(userNo, ct);

            entity.IsLocked = locked;

            // Unlocking also clears the failed-attempt counter, so the user isn't immediately
            // re-locked by the lockout threshold.
            if (locked == 0) entity.FailedLoginCount = 0;

            await _db.SaveChangesAsync(ct);
            return await EnrichAsync(entity, ct);
        }, cancellationToken);

    public Task ResetPasswordAsync(long userNo, string newPassword, CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(async ct =>
        {
            var user = await LoadAsync(userNo, ct);

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            user.PasswordChangedAt = DateTime.UtcNow;
            user.MustChangePassword = 1;
            user.FailedLoginCount = 0;
            user.IsLocked = 0;

            // Invalidates every access token already issued to this user.
            user.TokenVersion = user.TokenVersion == null ? 2 : user.TokenVersion + 1;

            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Password reset for userNo={UserNo} by userNo={ActorNo}",
                userNo, _ctx.CurrentUserNo());
        }, cancellationToken);

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task<User> LoadAsync(long userNo, CancellationToken ct) =>
        await _db.Users.FirstOrDefaultAsync(u => u.UserNo == userNo && u.IsDeleted == Deleted, ct)
        ?? throw new NotFoundException("User not found: userNo=" + userNo);

    /// <summary>Fills the read-only employee fields and the role mappings.</summary>
    private async Task<UserDto> EnrichAsync(User user, CancellationToken ct)
    {
        var dto = ToDto(user);

        var mappings = await _db.UserBranches
            .AsNoTracking()
            .Where(ub => ub.UserNo == user.UserNo && ub.IsDeleted == Deleted)
            .ToListAsync(ct);

        dto.RoleMappings = await ToRoleDtosAsync(mappings, ct);

        if (user.EmployeeNo == null) return dto;

        var emp = await _db.HrmEmployees
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.EmployeeNo == user.EmployeeNo && e.IsDeleted == Deleted, ct);

        if (emp == null) return dto;

        dto.EmployeeId = emp.EmployeeId;

        // Java joins first + last only here (no middle name).
        var empName = emp.FirstName;
        if (!string.IsNullOrWhiteSpace(emp.LastName)) empName = empName + " " + emp.LastName;
        dto.EmployeeName = empName;

        dto.DepartmentNo = emp.DepartmentNo;
        dto.DesignationNo = emp.DesignationNo;
        dto.BranchNo = emp.BranchNo;
        dto.OfficialEmail = emp.OfficialEmail;
        dto.MobileNumber = emp.MobileNumber;
        dto.JoiningDate = DateOnly.FromDateTime(emp.JoiningDate);

        dto.DepartmentName = await _db.HrmDepartments.AsNoTracking()
            .Where(d => d.DepartmentNo == emp.DepartmentNo && d.IsDeleted == Deleted)
            .Select(d => d.DepartmentName)
            .FirstOrDefaultAsync(ct);

        dto.DesignationName = await _db.HrmDesignations.AsNoTracking()
            .Where(d => d.DesignationNo == emp.DesignationNo && d.IsDeleted == Deleted)
            .Select(d => d.DesignationName)
            .FirstOrDefaultAsync(ct);

        return dto;
    }

    /// <summary>Resolves role names in one batched query rather than per row.</summary>
    private async Task<List<UserRoleDto>> ToRoleDtosAsync(List<UserBranch> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return new List<UserRoleDto>();

        var roleNos = rows.Select(r => r.RoleNo).Distinct().ToList();

        var roleNames = await _db.Roles
            .AsNoTracking()
            .Where(r => roleNos.Contains(r.RoleNo) && r.IsDeleted == Deleted)
            .ToDictionaryAsync(r => r.RoleNo, r => r.RoleName, ct);

        return rows.Select(ub => new UserRoleDto
        {
            UserRoleNo = ub.UserBranchNo,
            UserNo = ub.UserNo,
            BranchNo = ub.BranchNo,
            RoleNo = ub.RoleNo,
            RoleName = roleNames.TryGetValue(ub.RoleNo, out var name) ? name : null,
            IsPrimary = ub.IsDefault,
            IsActive = ub.IsActive,
            RowVersion = ub.RowVersion
        }).ToList();
    }

    /// <summary>
    /// Replaces the user's branch/role mappings wholesale. A null list means "leave alone" —
    /// only an explicit list clears and rewrites them.
    /// </summary>
    private async Task ReplaceRoleMappingsAsync(long userNo, List<UserRoleDto>? roleDtos, CancellationToken ct)
    {
        if (roleDtos == null) return;

        var userNoActor = _ctx.CurrentUserNo();

        var existing = await _db.UserBranches
            .Where(ub => ub.UserNo == userNo && ub.IsDeleted == Deleted)
            .ToListAsync(ct);

        foreach (var ub in existing) ub.PerformSoftDelete(userNoActor);

        foreach (var dto in roleDtos)
        {
            if (dto.RoleNo == null || dto.BranchNo == null) continue;

            var roleExists = await _db.Roles
                .AnyAsync(r => r.RoleNo == dto.RoleNo && r.IsDeleted == Deleted, ct);

            if (!roleExists)
            {
                throw new NotFoundException("Role not found: roleNo=" + dto.RoleNo);
            }

            _db.UserBranches.Add(new UserBranch
            {
                UserNo = userNo,
                BranchNo = dto.BranchNo.Value,
                RoleNo = dto.RoleNo.Value,
                IsDefault = dto.IsPrimary ?? 0,
                IsActive = dto.IsActive ?? 1
            });
        }

        await _db.SaveChangesAsync(ct);
    }

    private static void Apply(UserDto dto, User e)
    {
        if (dto.UserName != null) e.UserName = dto.UserName;
        if (dto.Email != null) e.Email = dto.Email;
        if (dto.CompanyNo != null) e.CompanyNo = dto.CompanyNo;
        if (dto.AccessScope != null) e.AccessScope = dto.AccessScope.Value;
        if (dto.DefaultBranchNo != null) e.DefaultBranchNo = dto.DefaultBranchNo;
        if (dto.IsActive != null) e.IsActive = dto.IsActive.Value;
        if (dto.IsLocked != null) e.IsLocked = dto.IsLocked.Value;
        if (dto.FailedLoginCount != null) e.FailedLoginCount = dto.FailedLoginCount.Value;
    }

    private static UserDto ToDto(User e) => new()
    {
        UserNo = e.UserNo,
        UserId = e.UserId,
        UserName = e.UserName,
        Email = e.Email,
        EmployeeNo = e.EmployeeNo,
        IsActive = e.IsActive,
        IsLocked = e.IsLocked,
        FailedLoginCount = e.FailedLoginCount,
        CompanyNo = e.CompanyNo,
        AccessScope = e.AccessScope,
        DefaultBranchNo = e.DefaultBranchNo,
        LastLoginAt = e.LastLoginAt,
        PasswordChangedAt = e.PasswordChangedAt,
        RowVersion = e.RowVersion
        // Password is deliberately never echoed back.
    };
}
