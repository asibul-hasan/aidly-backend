using AidlyErp.Domain.Common;

namespace AidlyErp.Application.Auth.Services;

/// <summary>
/// Principal carried in the JWT. The .NET counterpart of Java's <c>AidlyUserDetails</c>
/// (a Spring Security <c>UserDetails</c>); ASP.NET Core models the authenticated principal as
/// claims, so this type exists to pass token-generation inputs around and to expose the same
/// account-status flags.
/// </summary>
public sealed class AidlyUserDetails
{
    public long? UserNo { get; }
    public long? CompanyNo { get; }
    public long? BranchNo { get; }
    public AccessScope AccessScope { get; }
    public string UserName { get; }
    public string Password { get; }
    public bool Locked { get; }
    public bool Active { get; }
    public IReadOnlyCollection<string> Authorities { get; }

    /// <summary>Convenience constructor for the token-refresh path (BRANCH scope, status not re-checked).</summary>
    public AidlyUserDetails(long? userNo, long? companyNo, long? branchNo, string userName, string password)
        : this(userNo, companyNo, branchNo, AccessScope.Branch, userName, password, false, true, Array.Empty<string>()) { }

    /// <summary>Convenience constructor carrying the access scope (refresh/switch paths).</summary>
    public AidlyUserDetails(long? userNo, long? companyNo, long? branchNo, AccessScope accessScope,
                            string userName, string password)
        : this(userNo, companyNo, branchNo, accessScope, userName, password, false, true, Array.Empty<string>()) { }

    /// <summary>Backward-compatible full constructor (defaults scope to BRANCH).</summary>
    public AidlyUserDetails(long? userNo, long? companyNo, long? branchNo,
                            string userName, string password,
                            bool locked, bool active, IReadOnlyCollection<string> authorities)
        : this(userNo, companyNo, branchNo, AccessScope.Branch, userName, password, locked, active, authorities) { }

    public AidlyUserDetails(long? userNo, long? companyNo, long? branchNo, AccessScope accessScope,
                            string userName, string password,
                            bool locked, bool active, IReadOnlyCollection<string> authorities)
    {
        UserNo = userNo;
        CompanyNo = companyNo;
        BranchNo = branchNo;
        AccessScope = accessScope;
        UserName = userName;
        Password = password;
        Locked = locked;
        Active = active;
        Authorities = authorities;
    }

    /// <summary>Reflects <c>sys_user.is_locked</c>. Login is rejected when this is false.</summary>
    public bool IsAccountNonLocked => !Locked;

    /// <summary>Reflects <c>sys_user.is_active</c>. Login is rejected when this is false.</summary>
    public bool IsEnabled => Active;

    public bool IsAccountNonExpired => true;

    public bool IsCredentialsNonExpired => true;
}

public interface IJwtTokenService
{
    // ---------------------------------------------------------------- issue --

    string GenerateAccessToken(AidlyUserDetails userDetails);

    string GenerateAccessToken(AidlyUserDetails userDetails, long? sessionNo);

    string GenerateAccessToken(AidlyUserDetails userDetails, long? sessionNo, IReadOnlyList<long>? roleNos);

    string GenerateAccessToken(AidlyUserDetails userDetails, long? sessionNo, IReadOnlyList<long>? roleNos,
                               long? tokenVersion);

    /// <summary>
    /// Issues an access token, optionally embedding the user's per-form permission bitmasks
    /// (<c>perms</c> claim). With this claim present the per-request RBAC check is a pure
    /// in-memory bitmask lookup — no database round trip. Omitting it (legacy callers) leaves the
    /// authorization filter to fall back to the cached DB resolver.
    /// </summary>
    string GenerateAccessToken(AidlyUserDetails userDetails, long? sessionNo, IReadOnlyList<long>? roleNos,
                               long? tokenVersion, IReadOnlyDictionary<string, int>? permBits);

    string GenerateRefreshToken(AidlyUserDetails userDetails);

    string GenerateRefreshToken(AidlyUserDetails userDetails, long? sessionNo, long? tokenVersion);

    /// <summary>
    /// Generates an opaque 32-byte (64 hex-char) cryptographically random value suitable for use
    /// as a refresh token stored in an HttpOnly cookie. This value is NOT a JWT — it is stored
    /// hashed in the database.
    /// </summary>
    string GenerateRefreshTokenValue();

    /// <summary>
    /// Returns a SHA-256 hex digest of the raw token. Only the hash is persisted; the raw value
    /// lives only in the HttpOnly cookie and is never logged or stored server-side.
    /// </summary>
    string HashToken(string rawToken);

    // --------------------------------------------------------------- verify --

    bool ValidateToken(string token);

    bool ValidateToken(string token, AidlyUserDetails userDetails);

    // ----------------------------------------------------------------- read --

    string? GetUsernameFromToken(string token);

    long? GetCompanyNoFromToken(string token);

    long? GetBranchNoFromToken(string token);

    long? GetUserNoFromToken(string token);

    long? GetSessionNoFromToken(string token);

    long? GetTokenVersionFromToken(string token);

    /// <summary>Reads <c>accessScope</c> (1=BRANCH, 2=COMPANY, 3=GLOBAL); defaults to BRANCH when absent.</summary>
    AccessScope GetAccessScopeFromToken(string token);

    /// <summary>
    /// Reads the per-form permission bitmask map (<c>perms</c> claim). Returns <c>null</c> when the
    /// claim is absent (legacy token) so the caller can fall back to the DB resolver; an empty map
    /// means "authenticated but no form permissions".
    /// </summary>
    IReadOnlyDictionary<string, int>? GetPermsFromToken(string token);

    // ------------------------------------------------------------- lifetimes --

    /// <summary>Configured access-token TTL in milliseconds.</summary>
    long AccessTokenExpiration { get; }

    /// <summary>Configured refresh-token TTL in milliseconds (for cookie Max-Age calculation).</summary>
    long RefreshTokenExpiration { get; }

    // ------------------------------------------------------- legacy overloads --

    /// <summary>Retained so existing callers keep compiling; delegates to the richer overloads.</summary>
    string GenerateAccessToken(long userNo, string userId, string userName, long companyNo, long branchNo, short accessScope);

    /// <summary>Retained so existing callers keep compiling.</summary>
    string GenerateRefreshToken(long userNo);

    /// <summary>Retained so existing callers keep compiling.</summary>
    bool ValidateToken(string token, out long userNo, out long companyNo, out long branchNo);
}
