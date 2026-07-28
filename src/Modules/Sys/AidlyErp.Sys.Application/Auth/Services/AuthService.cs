using Microsoft.EntityFrameworkCore;
using AidlyErp.Sys.Application.Auth.Dto;
using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Abstractions;
using AidlyErp.Shared.Contracts;
using AidlyErp.Sys.Contracts;
using AidlyErp.Sys.Application.Interfaces;
using AidlyErp.Hrm.Contracts;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Shared.Core;
using AidlyErp.Sys.Domain;

namespace AidlyErp.Sys.Application.Auth.Services;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request);
    Task<RefreshTokenResponse> RefreshAsync(RefreshTokenRequest request);
    Task<CurrentUserResponse> GetCurrentUserAsync();
    Task<LoginResponse> SwitchBranchAsync(SwitchBranchRequest request);
    Task<LoginResponse> SwitchCompanyAsync(SwitchCompanyRequest request);
    Task ChangePasswordAsync(ChangePasswordRequest request);
    Task ResetPasswordAsync(ResetPasswordRequest request);

    /// <summary>
    /// Pre-login context discovery: the companies this user can sign into and the branches
    /// available inside each. Called before authentication, so the login screen can offer a
    /// company/branch picker.
    /// </summary>
    Task<List<UserContextResponse>> GetUserContextsAsync(string? userId);

    Task<List<AidlyErp.Sys.Application.Dto.CompanyDto>> GetCompaniesByUserIdAsync(string? userId);

    Task<List<AidlyErp.Sys.Application.Dto.BranchDto>> GetBranchesByUserIdAsync(string? userId);
    FormPermissionResponse GetFormPermission(string formId);
}

public class AuthService : IAuthService
{
    private readonly ISysDbContext _db;
    private readonly IEmployeeDirectory _employees;
    private readonly ICompanyBranchContext _tenantContext;
    private readonly IJwtTokenService _jwtService;
    private readonly IRbacAuthorizationService _rbacService;
    private readonly ISysSessionService _sessionService;
    private readonly ILoginAttemptService _loginAttemptService;
    private readonly LoginAttemptLimiter _loginAttemptLimiter;
    private readonly IAuthConfigService? _authConfigService;

    public AuthService(
        ISysDbContext db,
        ICompanyBranchContext tenantContext,
        IJwtTokenService jwtService,
        IRbacAuthorizationService rbacService,
        ISysSessionService sessionService,
        ILoginAttemptService loginAttemptService,
        LoginAttemptLimiter loginAttemptLimiter,
        IEmployeeDirectory employees,
        IAuthConfigService? authConfigService = null)
    {
        _db = db;
        _employees = employees;
        _tenantContext = tenantContext;
        _jwtService = jwtService;
        _rbacService = rbacService;
        _sessionService = sessionService;
        _loginAttemptService = loginAttemptService;
        _loginAttemptLimiter = loginAttemptLimiter;
        _authConfigService = authConfigService;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        var identifier = request.GetIdentifier();
        if (string.IsNullOrWhiteSpace(identifier))
            throw new ArgumentException("Username or UserId is required");

        // Check brute-force rate limiter before hitting the DB.
        if (_loginAttemptLimiter.IsBlocked(identifier, null))
            throw new UnauthorizedAccessException("Too many failed attempts. Please try again later.");

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.UserId == identifier || u.UserName == identifier);

        if (user == null)
        {
            await _loginAttemptService.RecordAsync(identifier, null, false, "Invalid username");
            throw new KeyNotFoundException("Invalid username or password");
        }

        if (user.IsLocked == 1)
        {
            await _loginAttemptService.RecordAsync(identifier, null, false, "Account locked");
            throw new UnauthorizedAccessException("Account is locked. Contact your administrator.");
        }

        if (string.IsNullOrEmpty(user.PasswordHash) || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= 5)
            {
                user.IsLocked = 1;
                user.LockedUntil = DateTime.UtcNow.AddMinutes(15);
            }
            await _db.SaveChangesAsync();
            await _loginAttemptService.RecordAsync(identifier, null, false, "Invalid password");
            throw new UnauthorizedAccessException("Invalid username or password");
        }

        user.FailedLoginCount = 0;
        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var companyNo = request.CompanyNo ?? user.CompanyNo ?? 1;
        var branchNo = request.BranchNo ?? user.DefaultBranchNo ?? 1;

        var accessToken = _jwtService.GenerateAccessToken(
            user.UserNo,
            user.UserId ?? user.UserName,
            user.UserName,
            companyNo,
            branchNo,
            user.AccessScope
        );

        var refreshToken = _jwtService.GenerateRefreshToken(user.UserNo);

        // Create a session record for the login.
        try
        {
            await _sessionService.CreateSessionAsync(
                user.UserNo, branchNo, companyNo,
                user.AccessScope, user.TokenVersion,
                null, null);
        }
        catch
        {
            // Session creation failure should not block login.
        }

        // Record successful login attempt.
        await _loginAttemptService.RecordAsync(identifier, null, true, null);

        // Resolve the RBAC session — menu, roles, accessible branches and the per-form
        // permission bitmask. Without this the client receives a token but no navigation.
        var rbac = await _rbacService.ResolveSessionAsync(user.UserNo, branchNo);

        // Re-issue the access token carrying the perms claim, so per-request authorization is an
        // in-memory bitmask lookup with zero DB round trips (see PermissionBits). The first token
        // was minted before the session was resolved and has no perms.
        accessToken = _jwtService.GenerateAccessToken(
            new AidlyUserDetails(
                user.UserNo,
                rbac.CompanyNo ?? companyNo,
                rbac.BranchNo ?? branchNo,
                AccessScopeExtensions.FromCode(user.AccessScope),
                user.UserId ?? user.UserName,
                string.Empty),
            sessionNo: null,
            roleNos: rbac.RoleNos,
            tokenVersion: user.TokenVersion,
            permBits: rbac.PermBits);

        // Employee-derived display fields, resolved by indexed lookup.
        // Employee display fields come from HRM through its contract, which already resolves
        // department and designation names.
        var emp = user.EmployeeNo <= 0 ? null : await _employees.FindAsync(user.EmployeeNo);

        string? departmentName = emp?.DepartmentName;
        string? designationName = emp?.DesignationName;

        return new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            TokenType = "Bearer",
            ExpiresIn = _jwtService.AccessTokenExpiration / 1000,
            UserNo = user.UserNo,
            UserId = user.UserId ?? user.UserName,
            UserName = user.UserName,
            CompanyNo = rbac.CompanyNo ?? companyNo,
            BranchNo = rbac.BranchNo ?? branchNo,
            AccessScope = user.AccessScope,

            AuthConfig = _authConfigService?.GetClientConfig(),
            EmployeeNo = user.EmployeeNo,
            EmployeeName = emp?.EmployeeName,
            DepartmentName = departmentName,
            DesignationName = designationName,
            CompanyName = rbac.CompanyName,
            BranchName = rbac.BranchName,
            AvailableBranches = rbac.Branches,
            RoleNos = rbac.RoleNos,
            Roles = rbac.Roles,
            MustChangePassword = user.MustChangePassword,
            Menu = rbac.Menu
        };
    }

    public async Task<RefreshTokenResponse> RefreshAsync(RefreshTokenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            throw new ArgumentException("Refresh token is required");

        if (!_jwtService.ValidateToken(request.RefreshToken, out var userNo, out var companyNo, out var branchNo))
            throw new UnauthorizedAccessException("Invalid or expired refresh token");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserNo == userNo);
        if (user == null || user.IsLocked == 1)
            throw new UnauthorizedAccessException("User not active or locked");

        var newAccessToken = _jwtService.GenerateAccessToken(
            user.UserNo,
            user.UserId ?? user.UserName,
            user.UserName,
            companyNo > 0 ? companyNo : (user.CompanyNo ?? 1),
            branchNo > 0 ? branchNo : (user.DefaultBranchNo ?? 1),
            user.AccessScope
        );

        var newRefreshToken = _jwtService.GenerateRefreshToken(user.UserNo);

        return new RefreshTokenResponse
        {
            AccessToken = newAccessToken,
            RefreshToken = newRefreshToken,
            TokenType = "Bearer",
            ExpiresIn = 300
        };
    }

    public async Task<CurrentUserResponse> GetCurrentUserAsync()
    {
        if (!_tenantContext.UserNo.HasValue)
            throw new UnauthorizedAccessException("User context not established");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserNo == _tenantContext.UserNo.Value);
        if (user == null)
            throw new KeyNotFoundException("User not found");

        var companyNo = _tenantContext.CompanyNo ?? user.CompanyNo ?? 0;
        var branchNo = _tenantContext.BranchNo ?? user.DefaultBranchNo ?? 0;

        // Employee-derived display fields.
        string? employeeName = null, designation = null, companyName = null, branchName = null;
        if (user.EmployeeNo > 0)
        {
            var emp = await _employees.FindAsync(user.EmployeeNo);
            if (emp != null)
            {
                // Java joins first + last only here — no middle name.
                employeeName = $"{emp.FirstName}{(string.IsNullOrWhiteSpace(emp.LastName) ? "" : " " + emp.LastName)}";
                if (emp.DesignationNo > 0) designation = emp.DesignationName;
            }
        }

        if (companyNo > 0)
            companyName = await _db.Companies.AsNoTracking()
                .Where(c => c.CompanyNo == companyNo && c.IsDeleted == 0)
                .Select(c => c.CompanyName).FirstOrDefaultAsync();

        if (branchNo > 0)
            branchName = await _db.Branches.AsNoTracking()
                .Where(b => b.BranchNo == branchNo && b.IsDeleted == 0)
                .Select(b => b.BranchName).FirstOrDefaultAsync();

        return new CurrentUserResponse
        {
            UserNo = user.UserNo,
            UserId = user.UserId ?? user.UserName,
            UserName = user.UserName,
            Name = employeeName ?? user.UserName,
            Email = user.Email,
            EmployeeNo = user.EmployeeNo,
            Designation = designation,
            CompanyNo = companyNo,
            CompanyName = companyName,
            BranchNo = branchNo,
            BranchName = branchName,
            AccessScope = user.AccessScope
        };
    }

    public async Task<LoginResponse> SwitchBranchAsync(SwitchBranchRequest request)
    {
        if (!_tenantContext.UserNo.HasValue)
            throw new UnauthorizedAccessException("User context not established");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserNo == _tenantContext.UserNo.Value);
        if (user == null)
            throw new KeyNotFoundException("User not found");

        var companyNo = _tenantContext.CompanyNo ?? user.CompanyNo ?? 1;

        var accessToken = _jwtService.GenerateAccessToken(
            user.UserNo,
            user.UserId ?? user.UserName,
            user.UserName,
            companyNo,
            request.BranchNo,
            user.AccessScope
        );

        var refreshToken = _jwtService.GenerateRefreshToken(user.UserNo);

        return new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            TokenType = "Bearer",
            ExpiresIn = 300,
            UserNo = user.UserNo,
            UserId = user.UserId ?? user.UserName,
            UserName = user.UserName,
            CompanyNo = companyNo,
            BranchNo = request.BranchNo,
            AccessScope = user.AccessScope
        };
    }

    public async Task<LoginResponse> SwitchCompanyAsync(SwitchCompanyRequest request)
    {
        if (!_tenantContext.UserNo.HasValue)
            throw new UnauthorizedAccessException("User context not established");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserNo == _tenantContext.UserNo.Value);
        if (user == null)
            throw new KeyNotFoundException("User not found");

        var branchNo = request.BranchNo ?? 1;

        var accessToken = _jwtService.GenerateAccessToken(
            user.UserNo,
            user.UserId ?? user.UserName,
            user.UserName,
            request.CompanyNo,
            branchNo,
            user.AccessScope
        );

        var refreshToken = _jwtService.GenerateRefreshToken(user.UserNo);

        return new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            TokenType = "Bearer",
            ExpiresIn = 300,
            UserNo = user.UserNo,
            UserId = user.UserId ?? user.UserName,
            UserName = user.UserName,
            CompanyNo = request.CompanyNo,
            BranchNo = branchNo,
            AccessScope = user.AccessScope
        };
    }

    public async Task ChangePasswordAsync(ChangePasswordRequest request)
    {
        if (!_tenantContext.UserNo.HasValue)
            throw new UnauthorizedAccessException("User context not established");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserNo == _tenantContext.UserNo.Value);
        if (user == null)
            throw new KeyNotFoundException("User not found");

        if (string.IsNullOrEmpty(user.PasswordHash) || !BCrypt.Net.BCrypt.Verify(request.OldPassword, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid current password");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.PasswordChangedAt = DateTime.UtcNow;
        user.MustChangePassword = 0;
        await _db.SaveChangesAsync();
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserName))
            throw new ArgumentException("Username is required");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == request.UserName || u.UserName == request.UserName);
        if (user == null)
            throw new KeyNotFoundException("User not found");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.PasswordChangedAt = DateTime.UtcNow;
        user.MustChangePassword = 1;
        await _db.SaveChangesAsync();
    }

    public FormPermissionResponse GetFormPermission(string formId)
    {
        // Delegate to the RBAC authorization service which resolves permissions from the
        // JWT bitmask or the cached DB resolver. Falls back to all-false if resolution fails.
        var userNo = _tenantContext.UserNo ?? 0;
        var companyNo = _tenantContext.CompanyNo;
        var branchNo = _tenantContext.BranchNo;
        try
        {
            return _rbacService.GetFormPermissionAsync(userNo, companyNo, branchNo, formId).GetAwaiter().GetResult()
                ?? new FormPermissionResponse { FormId = formId };
        }
        catch
        {
            return new FormPermissionResponse { FormId = formId };
        }
    }

    // ─── Pre-login context discovery ─────────────────────────────────────────

    public async Task<List<UserContextResponse>> GetUserContextsAsync(string? userId)
    {
        var rbac = await ResolveContextsAsync(userId);

        // Group the user's branches by company, preserving the order RBAC returned them in
        // (default branch first) so the picker's first option is the sensible default.
        var byCompany = new Dictionary<long, List<Dto.BranchDto>>();
        var order = new List<long>();

        foreach (var b in rbac.Branches)
        {
            if (b.CompanyNo == null) continue;

            if (!byCompany.TryGetValue(b.CompanyNo.Value, out var list))
            {
                list = new List<Dto.BranchDto>();
                byCompany[b.CompanyNo.Value] = list;
                order.Add(b.CompanyNo.Value);
            }

            list.Add(b);
        }

        return order.Select(companyNo =>
        {
            var branches = byCompany[companyNo];

            return new UserContextResponse
            {
                CompanyNo = companyNo,
                CompanyName = branches.Select(b => b.CompanyName).FirstOrDefault(n => n != null)
                              ?? rbac.CompanyName,
                Branches = branches
                    .Select(b => new UserContextBranchDto(b.BranchNo, b.BranchName))
                    .ToList()
            };
        }).ToList();
    }

    public async Task<List<AidlyErp.Sys.Application.Dto.CompanyDto>> GetCompaniesByUserIdAsync(string? userId)
    {
        var rbac = await ResolveContextsAsync(userId);

        var companyNos = rbac.Branches
            .Where(b => b.CompanyNo != null)
            .Select(b => b.CompanyNo!.Value)
            .Distinct()
            .ToList();

        if (companyNos.Count == 0) return new List<AidlyErp.Sys.Application.Dto.CompanyDto>();

        var companies = await _db.Companies
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(c => companyNos.Contains(c.CompanyNo) && c.IsDeleted == 0)
            .ToListAsync();

        return companies.Select(c => new AidlyErp.Sys.Application.Dto.CompanyDto
        {
            CompanyNo = c.CompanyNo,
            CompanyId = c.CompanyId,
            CompanyName = c.CompanyName,
            CompanyNameNls = c.CompanyNameNls,
            CompanyType = c.CompanyType,
            City = c.City,
            CountryCode = c.CountryCode,
            MobileNo = c.MobileNo,
            Email = c.Email,
            LogoPath = c.LogoPath,
            IsActive = c.IsActive,
            RowVersion = c.RowVersion
        }).ToList();
    }

    public async Task<List<AidlyErp.Sys.Application.Dto.BranchDto>> GetBranchesByUserIdAsync(string? userId)
    {
        var rbac = await ResolveContextsAsync(userId);

        return rbac.Branches.Select(b => new AidlyErp.Sys.Application.Dto.BranchDto
        {
            BranchNo = b.BranchNo,
            BranchName = b.BranchName,
            CompanyNo = b.CompanyNo,
            CompanyName = b.CompanyName,
            IsActive = 1
        }).ToList();
    }

    /// <summary>
    /// Shared prologue for the three pre-login lookups: resolve the login id to a live account,
    /// then resolve its RBAC session. Every failure reports the same generic message so this
    /// unauthenticated endpoint cannot be used to enumerate valid user ids.
    /// </summary>
    private async Task<RbacSessionContext> ResolveContextsAsync(string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ValidationException("INVALID_CREDENTIALS");
        }

        var user = await _db.Users
                       .AsNoTracking()
                       .FirstOrDefaultAsync(u => (u.UserId == userId || u.UserName == userId)
                                                 && u.IsDeleted == 0)
                   ?? throw new ValidationException("INVALID_CREDENTIALS");

        if (user.IsActive != 1 || user.IsLocked == 1)
        {
            throw new ValidationException("INVALID_CREDENTIALS");
        }

        return await _rbacService.ResolveSessionAsync(user.UserNo, null);
    }
}
