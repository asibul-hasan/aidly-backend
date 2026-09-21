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

public interface ISys1101Service
{
    Task<List<Sys1101EmployeeDto>> GetEmployeeListAsync(short? isActive, CancellationToken cancellationToken = default);

    Task<Sys1101EmployeeDto> GetEmployeeDetailAsync(long employeeNo, CancellationToken cancellationToken = default);

    Task<Sys1101UserDto?> GetUserByEmployeeNoAsync(long employeeNo, CancellationToken cancellationToken = default);

    Task<Sys1101UserDto> GetUserDetailAsync(long userNo, CancellationToken cancellationToken = default);

    Task<Sys1101UserDto> CreateUserAsync(Sys1101UserDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Provisions a login user straight from an employee's data: <c>user_id</c> and the initial
    /// password are both the employee's <c>employee_id</c>; first login is forced to change it.
    /// Idempotent — returns the existing user (and ensures the employee flag) if one already
    /// exists. Used by the "Create user from employee" action AND by the HRM employee form's
    /// <c>is_create_user</c> checkbox.
    /// </summary>
    Task<Sys1101UserDto> CreateUserFromEmployeeAsync(long employeeNo, CancellationToken cancellationToken = default);

    Task<Sys1101UserDto> UpdateUserAsync(long userNo, Sys1101UserDto dto, CancellationToken cancellationToken = default);

    Task DeleteUserAsync(long userNo, CancellationToken cancellationToken = default);

    Task<Sys1101UserDto> LockUserAsync(long userNo, short locked, CancellationToken cancellationToken = default);

    Task ResetPasswordAsync(long userNo, string newPassword, CancellationToken cancellationToken = default);

    Task<List<Sys1101UserRoleDto>> GetUserRolesAsync(long userNo, CancellationToken cancellationToken = default);
}

/// <summary>
/// Dedicated service for the SYS1101 User Management form.
/// Handles all business logic for this specific form.
/// </summary>
public class Sys1101Service : ISys1101Service
{
    private const short Deleted = 0;
    private const short Active = 1;

    private readonly ISysDbContext _db;
    private readonly IUnitOfWork<ISysDbContext> _unitOfWork;
    private readonly ICompanyBranchContext _ctx;
    private readonly IEmployeeDirectory _employees;
    private readonly ILogger<Sys1101Service> _logger;

    public Sys1101Service(ISysDbContext db, IUnitOfWork<ISysDbContext> unitOfWork, ICompanyBranchContext ctx,
                          IEmployeeDirectory employees,
                          ILogger<Sys1101Service> logger)
    {
        _db = db;
        _unitOfWork = unitOfWork;
        _ctx = ctx;
        _employees = employees;
        _logger = logger;
    }

    // ─── Employee Operations ─────────────────────────────────────────────────

    public async Task<List<Sys1101EmployeeDto>> GetEmployeeListAsync(short? isActive,
                                                                     CancellationToken cancellationToken = default)
    {
        // Employees belong to HRM, so they are read through the module contract. The contract
        // resolves department and designation names by join, exactly as the Java
        // findAllWithFilters query did.
        var employees = await _employees.ListAsync(isActive, cancellationToken);

        return employees.Select(ToEmployeeDto).ToList();
    }

    public async Task<Sys1101EmployeeDto> GetEmployeeDetailAsync(long employeeNo,
                                                                 CancellationToken cancellationToken = default)
    {
        var emp = await _employees.FindAsync(employeeNo, cancellationToken)
                  ?? throw new NotFoundException("Employee not found: employeeNo=" + employeeNo);

        return ToEmployeeDto(emp);
    }

    /// <summary>
    /// Maps the HRM read model onto this form's DTO. <c>joining_date</c> is a DATE in the Java
    /// contract but a <c>DateTime</c> on the entity, so it is narrowed here to keep the JSON shape
    /// identical.
    /// </summary>
    private static Sys1101EmployeeDto ToEmployeeDto(EmployeeInfo e) => new()
    {
        EmployeeNo = e.EmployeeNo,
        EmployeeId = e.EmployeeId,
        FullName = BuildFullName(e.FirstName, e.MiddleName, e.LastName),
        DepartmentNo = e.DepartmentNo,
        DepartmentName = e.DepartmentName,
        DesignationNo = e.DesignationNo,
        DesignationName = e.DesignationName,
        IsActive = e.IsActive,
        JoiningDate = DateOnly.FromDateTime(e.JoiningDate),
        OfficialEmail = e.OfficialEmail,
        MobileNumber = e.MobileNumber
    };

    // ─── User Operations ─────────────────────────────────────────────────────

    public async Task<Sys1101UserDto?> GetUserByEmployeeNoAsync(long employeeNo,
                                                                CancellationToken cancellationToken = default)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.EmployeeNo == employeeNo && u.IsDeleted == Deleted, cancellationToken);

        return user == null ? null : ToUserDto(user);
    }

    public async Task<Sys1101UserDto> GetUserDetailAsync(long userNo, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users
                       .AsNoTracking()
                       .FirstOrDefaultAsync(u => u.UserNo == userNo && u.IsDeleted == Deleted, cancellationToken)
                   ?? throw new NotFoundException("User not found: userNo=" + userNo);

        return ToUserDto(user);
    }

    public Task<Sys1101UserDto> CreateUserAsync(Sys1101UserDto dto, CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(async ct =>
        {
            if (dto.EmployeeNo == null)
            {
                throw new ValidationException("Employee number is required");
            }

            // Check if a user already exists for this employee
            var exists = await _db.Users
                .AnyAsync(u => u.EmployeeNo == dto.EmployeeNo && u.IsDeleted == Deleted, ct);

            if (exists)
            {
                throw new ValidationException("User already exists for this employee");
            }

            var user = new User
            {
                UserNo = dto.EmployeeNo.Value,
                EmployeeNo = dto.EmployeeNo.Value,
                UserId = dto.UserId ?? string.Empty,
                UserName = dto.UserName,
                CompanyNo = _ctx.CompanyNo,
                AccessScope = dto.AccessScope ?? 1,
                IsActive = 1,
                IsLocked = 0,
                FailedLoginCount = 0
            };

            // Password optional — an admin can set/reset later; login is blocked until set.
            if (!string.IsNullOrWhiteSpace(dto.Password))
            {
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
                user.PasswordChangedAt = DateTime.UtcNow;
                user.MustChangePassword = 1;
            }

            _db.Users.Add(user);
            await _db.SaveChangesAsync(ct);

            // The employee now has a login account → reflect it on the HRM employee record.
            await _employees.MarkHasUserAsync(user.EmployeeNo ?? 0, ct);

            _logger.LogInformation("User created: userNo={UserNo}, employeeNo={EmployeeNo}",
                user.UserNo, user.EmployeeNo);

            return ToUserDto(user);
        }, cancellationToken);

    public Task<Sys1101UserDto> CreateUserFromEmployeeAsync(long employeeNo,
                                                            CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(async ct =>
        {
            var emp = await _employees.FindAsync(employeeNo, ct)
                      ?? throw new NotFoundException("Employee not found: employeeNo=" + employeeNo);

            var existing = await _db.Users
                .FirstOrDefaultAsync(u => u.EmployeeNo == employeeNo && u.IsDeleted == Deleted, ct);

            if (existing != null)
            {
                await _employees.MarkHasUserAsync(employeeNo, ct);
                return ToUserDto(existing);
            }

            var empId = emp.EmployeeId;

            if (string.IsNullOrWhiteSpace(empId))
            {
                throw new ValidationException("Employee has no employee ID to use as the login id");
            }

            var loginTaken = await _db.Users.AnyAsync(u => u.UserId == empId && u.IsDeleted == Deleted, ct);

            if (loginTaken)
            {
                throw new ValidationException($"A user with login id '{empId}' already exists");
            }

            var user = new User
            {
                UserNo = employeeNo,                                       // user no = employee no
                EmployeeNo = employeeNo,
                UserId = empId,                                            // username = employee id
                UserName = emp.FullName,
                CompanyNo = _ctx.CompanyNo,
                DefaultBranchNo = emp.BranchNo,
                AccessScope = 1,                                           // BRANCH
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(empId),      // initial password = employee id
                PasswordChangedAt = DateTime.UtcNow,
                MustChangePassword = 1,                                    // force change on first login
                IsActive = 1,
                IsLocked = 0,
                FailedLoginCount = 0
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync(ct);

            await _employees.MarkHasUserAsync(employeeNo, ct);

            _logger.LogInformation(
                "User provisioned from employee: userNo={UserNo}, employeeNo={EmployeeNo}, userId={UserId}",
                user.UserNo, employeeNo, empId);

            return ToUserDto(user);
        }, cancellationToken);

    public Task<Sys1101UserDto> UpdateUserAsync(long userNo, Sys1101UserDto dto,
                                                CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(async ct =>
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.UserNo == userNo && u.IsDeleted == Deleted, ct)
                       ?? throw new NotFoundException("User not found: userNo=" + userNo);

            // Only these fields can be updated from this form — user_id is immutable here.
            if (dto.UserName != null) user.UserName = dto.UserName;
            if (dto.IsActive != null) user.IsActive = dto.IsActive.Value;
            if (dto.IsLocked != null) user.IsLocked = dto.IsLocked.Value;
            if (dto.AccessScope != null) user.AccessScope = dto.AccessScope.Value;

            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("User updated: userNo={UserNo}", user.UserNo);

            return ToUserDto(user);
        }, cancellationToken);

    public Task DeleteUserAsync(long userNo, CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(async ct =>
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.UserNo == userNo && u.IsDeleted == Deleted, ct)
                       ?? throw new NotFoundException("User not found: userNo=" + userNo);

            user.PerformSoftDelete(_ctx.CurrentUserNo());
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("User deleted: userNo={UserNo}", userNo);
        }, cancellationToken);

    public Task<Sys1101UserDto> LockUserAsync(long userNo, short locked,
                                              CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(async ct =>
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.UserNo == userNo && u.IsDeleted == Deleted, ct)
                       ?? throw new NotFoundException("User not found: userNo=" + userNo);

            user.IsLocked = locked;
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("User {Action}: userNo={UserNo}", locked == 1 ? "locked" : "unlocked", userNo);

            return ToUserDto(user);
        }, cancellationToken);

    public Task ResetPasswordAsync(long userNo, string newPassword, CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(async ct =>
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.UserNo == userNo && u.IsDeleted == Deleted, ct)
                       ?? throw new NotFoundException("User not found: userNo=" + userNo);

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            user.PasswordChangedAt = DateTime.UtcNow;
            user.MustChangePassword = 1;
            user.FailedLoginCount = 0;
            user.IsLocked = 0;

            // Bumping the token version invalidates every access token already issued to this
            // user, so a reset immediately terminates their existing sessions.
            user.TokenVersion = user.TokenVersion + 1;

            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Password reset: userNo={UserNo}", userNo);
        }, cancellationToken);

    // ─── User Role Operations ────────────────────────────────────────────────

    public async Task<List<Sys1101UserRoleDto>> GetUserRolesAsync(long userNo,
                                                                  CancellationToken cancellationToken = default)
    {
        var rows = await _db.UserBranches
            .AsNoTracking()
            .Where(ub => ub.UserNo == userNo && ub.IsDeleted == Deleted)
            .ToListAsync(cancellationToken);

        if (rows.Count == 0) return new List<Sys1101UserRoleDto>();

        // Batch-load all referenced roles in ONE query, then resolve names from a map —
        // never a role query per user-branch row.
        var roleNos = rows.Select(r => r.RoleNo).Distinct().ToList();

        var roleNames = roleNos.Count == 0
            ? new Dictionary<long, string>()
            : await _db.Roles
                .AsNoTracking()
                .Where(r => roleNos.Contains(r.RoleNo) && r.IsDeleted == Deleted)
                .ToDictionaryAsync(r => r.RoleNo, r => r.RoleName, cancellationToken);

        return rows.Select(ub => new Sys1101UserRoleDto(
            ub.UserBranchNo,
            ub.RoleNo,
            ub.RoleNo.HasValue && roleNames.TryGetValue(ub.RoleNo.Value, out var name) ? name : "Unknown",
            ub.IsDefault,
            ub.IsActive)).ToList();
    }

    // ─── Helper Methods ──────────────────────────────────────────────────────

    private static Sys1101UserDto ToUserDto(User user) => new()
    {
        UserNo = user.UserNo,
        UserId = user.UserId,
        EmployeeNo = user.EmployeeNo,
        UserName = user.UserName,
        IsActive = user.IsActive,
        IsLocked = user.IsLocked,
        AccessScope = user.AccessScope,
        FailedLoginCount = user.FailedLoginCount,
        LastLoginAt = user.LastLoginAt,
        PasswordChangedAt = user.PasswordChangedAt,
        RowVersion = user.RowVersion
        // Password is deliberately never echoed back.
    };

    private static string BuildFullName(string? first, string? middle, string? last) =>
        string.Join(' ', new[] { first, middle, last }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(part => part!.Trim()));
}
