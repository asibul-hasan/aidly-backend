using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Abstractions;
using AidlyErp.Shared.Contracts;
using AidlyErp.Sys.Contracts;
using AidlyErp.Sys.Application.Interfaces;
using AidlyErp.Hrm.Contracts;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Sys.Application.Dto;
using AidlyErp.Hrm.Domain;
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
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICompanyBranchContext _ctx;
    private readonly ILogger<Sys1101Service> _logger;

    public Sys1101Service(ISysDbContext db, IUnitOfWork unitOfWork, ICompanyBranchContext ctx,
                          ILogger<Sys1101Service> logger)
    {
        _db = db;
        _unitOfWork = unitOfWork;
        _ctx = ctx;
        _logger = logger;
    }

    // ─── Employee Operations ─────────────────────────────────────────────────

    public async Task<List<Sys1101EmployeeDto>> GetEmployeeListAsync(short? isActive,
                                                                     CancellationToken cancellationToken = default)
    {
        // Department and designation names are resolved by join, matching the Java
        // findAllWithFilters query.
        return await (from e in _db.HrmEmployees.AsNoTracking()
                      where e.IsDeleted == Deleted && (isActive == null || e.IsActive == isActive)
                      join d in _db.HrmDepartments.AsNoTracking() on e.DepartmentNo equals d.DepartmentNo into depts
                      from d in depts.DefaultIfEmpty()
                      join g in _db.HrmDesignations.AsNoTracking() on e.DesignationNo equals g.DesignationNo into desigs
                      from g in desigs.DefaultIfEmpty()
                      orderby e.EmployeeId
                      select new Sys1101EmployeeDto
                      {
                          EmployeeNo = e.EmployeeNo,
                          EmployeeId = e.EmployeeId,
                          FullName = BuildFullName(e.FirstName, e.MiddleName, e.LastName),
                          DepartmentNo = e.DepartmentNo,
                          DepartmentName = d == null ? null : d.DepartmentName,
                          DesignationNo = e.DesignationNo,
                          DesignationName = g == null ? null : g.DesignationName,
                          IsActive = e.IsActive,
                          // joining_date is a DATE in the Java contract; the entity stores it as
                          // DateTime, so narrow it here to keep the JSON shape identical.
                          JoiningDate = DateOnly.FromDateTime(e.JoiningDate),
                          OfficialEmail = e.OfficialEmail,
                          MobileNumber = e.MobileNumber
                      })
            .ToListAsync(cancellationToken);
    }

    public async Task<Sys1101EmployeeDto> GetEmployeeDetailAsync(long employeeNo,
                                                                 CancellationToken cancellationToken = default)
    {
        var emp = await _db.HrmEmployees
                      .AsNoTracking()
                      .FirstOrDefaultAsync(e => e.EmployeeNo == employeeNo && e.IsDeleted == Deleted, cancellationToken)
                  ?? throw new NotFoundException("Employee not found: employeeNo=" + employeeNo);

        // Resolve department/designation names via direct indexed PK lookups — never by scanning
        // the whole employee table just to read two names.
        var deptName = emp.DepartmentNo == null
            ? null
            : await _db.HrmDepartments.AsNoTracking()
                .Where(d => d.DepartmentNo == emp.DepartmentNo && d.IsDeleted == Deleted)
                .Select(d => d.DepartmentName)
                .FirstOrDefaultAsync(cancellationToken);

        var desigName = emp.DesignationNo == null
            ? null
            : await _db.HrmDesignations.AsNoTracking()
                .Where(g => g.DesignationNo == emp.DesignationNo && g.IsDeleted == Deleted)
                .Select(g => g.DesignationName)
                .FirstOrDefaultAsync(cancellationToken);

        return new Sys1101EmployeeDto
        {
            EmployeeNo = emp.EmployeeNo,
            EmployeeId = emp.EmployeeId,
            FullName = BuildFullName(emp.FirstName, emp.MiddleName, emp.LastName),
            DepartmentNo = emp.DepartmentNo,
            DepartmentName = deptName,
            DesignationNo = emp.DesignationNo,
            DesignationName = desigName,
            IsActive = emp.IsActive,
            JoiningDate = DateOnly.FromDateTime(emp.JoiningDate),
            OfficialEmail = emp.OfficialEmail,
            MobileNumber = emp.MobileNumber
        };
    }

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
            var emp = await _db.HrmEmployees
                .FirstOrDefaultAsync(e => e.EmployeeNo == user.EmployeeNo && e.IsDeleted == Deleted, ct);

            if (emp != null) await MarkEmployeeHasUserAsync(emp, ct);

            _logger.LogInformation("User created: userNo={UserNo}, employeeNo={EmployeeNo}",
                user.UserNo, user.EmployeeNo);

            return ToUserDto(user);
        }, cancellationToken);

    public Task<Sys1101UserDto> CreateUserFromEmployeeAsync(long employeeNo,
                                                            CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(async ct =>
        {
            var emp = await _db.HrmEmployees
                          .FirstOrDefaultAsync(e => e.EmployeeNo == employeeNo && e.IsDeleted == Deleted, ct)
                      ?? throw new NotFoundException("Employee not found: employeeNo=" + employeeNo);

            var existing = await _db.Users
                .FirstOrDefaultAsync(u => u.EmployeeNo == employeeNo && u.IsDeleted == Deleted, ct);

            if (existing != null)
            {
                await MarkEmployeeHasUserAsync(emp, ct);
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
                UserName = BuildFullName(emp),
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

            await MarkEmployeeHasUserAsync(emp, ct);

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
            roleNames.TryGetValue(ub.RoleNo, out var name) ? name : "Unknown",
            ub.IsDefault,
            ub.IsActive)).ToList();
    }

    // ─── Helper Methods ──────────────────────────────────────────────────────

    /// <summary>Sets <c>hrm_employee.is_create_user = 1</c> (the employee has a login account).</summary>
    private async Task MarkEmployeeHasUserAsync(HrmEmployee emp, CancellationToken ct)
    {
        if (emp.IsCreateUser != 1)
        {
            emp.IsCreateUser = 1;
            await _db.SaveChangesAsync(ct);
        }
    }

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

    /// <summary>Falls back to the employee id when every name part is blank.</summary>
    private static string BuildFullName(HrmEmployee e)
    {
        var name = BuildFullName(e.FirstName, e.MiddleName, e.LastName);
        return string.IsNullOrWhiteSpace(name) ? e.EmployeeId : name;
    }

    private static string BuildFullName(string? first, string? middle, string? last) =>
        string.Join(' ', new[] { first, middle, last }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(part => part!.Trim()));
}
