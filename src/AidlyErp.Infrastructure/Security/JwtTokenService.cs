using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AidlyErp.Application.Auth.Services;
using AidlyErp.Domain.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace AidlyErp.Infrastructure.Security;

/// <summary>
/// HMAC-SHA256 JWT issuer/reader. Mirrors the Java <c>core.auth.service.JwtTokenService</c>
/// claim-for-claim: <c>userNo</c>, <c>companyNo</c>, <c>branchNo</c>, <c>userName</c>,
/// <c>accessScope</c>, plus optional <c>roleNo</c>/<c>roles</c>/<c>roleNos</c>, <c>sessionNo</c>,
/// <c>tokenVersion</c> and the <c>perms</c> bitmask map.
/// </summary>
public class JwtTokenService : IJwtTokenService
{
    private const string PermsClaim = "perms";

    private readonly string _secret;
    private readonly string? _issuer;
    private readonly string? _audience;
    private readonly long _accessTokenExpiration;
    private readonly long _refreshTokenExpiration;
    private readonly SymmetricSecurityKey _signingKey;
    private readonly ILogger<JwtTokenService> _logger;

    public JwtTokenService(IConfiguration configuration, ILogger<JwtTokenService> logger)
    {
        _logger = logger;

        _secret = configuration["Aidly:Jwt:Secret"]
                  ?? Environment.GetEnvironmentVariable("JWT_SECRET") ?? string.Empty;
        _issuer = configuration["Aidly:Jwt:Issuer"];
        _audience = configuration["Aidly:Jwt:Audience"];

        // Accept both key spellings: "…Expiration" mirrors the Java property name
        // (aidly.jwt.access-token-expiration); "…ExpirationMs" is what appsettings.json ships.
        _accessTokenExpiration = ReadLong(configuration,
            "Aidly:Jwt:AccessTokenExpiration", "Aidly:Jwt:AccessTokenExpirationMs", 300_000L);
        _refreshTokenExpiration = ReadLong(configuration,
            "Aidly:Jwt:RefreshTokenExpiration", "Aidly:Jwt:RefreshTokenExpirationMs", 604_800_000L);

        ValidateSecret();

        _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
    }

    private static long ReadLong(IConfiguration configuration, string primaryKey, string fallbackKey, long defaultValue)
    {
        if (long.TryParse(configuration[primaryKey], out var primary)) return primary;
        if (long.TryParse(configuration[fallbackKey], out var fallback)) return fallback;
        return defaultValue;
    }

    /// <summary>
    /// Fail-fast secret validation, matching Java's <c>@PostConstruct validateSecret()</c>.
    /// A short secret is rejected rather than silently padded — padding would let a weak key
    /// through and diverge from the Java service's security guarantee.
    /// </summary>
    private void ValidateSecret()
    {
        if (string.IsNullOrWhiteSpace(_secret))
        {
            throw new InvalidOperationException(
                "JWT secret must be configured via 'Aidly:Jwt:Secret' setting or JWT_SECRET environment variable.");
        }

        if (_secret.Length < 32)
        {
            throw new InvalidOperationException(
                "JWT secret must be at least 32 characters long for HMAC-SHA256 security.");
        }

        _logger.LogInformation(
            "JWT token service initialized with access token TTL={AccessTtl}ms, refresh token TTL={RefreshTtl}ms",
            _accessTokenExpiration, _refreshTokenExpiration);
    }

    public long AccessTokenExpiration => _accessTokenExpiration;

    public long RefreshTokenExpiration => _refreshTokenExpiration;

    // ------------------------------------------------------------------ issue --

    public string GenerateAccessToken(AidlyUserDetails userDetails) =>
        GenerateAccessToken(userDetails, null, null, null, null);

    public string GenerateAccessToken(AidlyUserDetails userDetails, long? sessionNo) =>
        GenerateAccessToken(userDetails, sessionNo, null, null, null);

    public string GenerateAccessToken(AidlyUserDetails userDetails, long? sessionNo, IReadOnlyList<long>? roleNos) =>
        GenerateAccessToken(userDetails, sessionNo, roleNos, null, null);

    public string GenerateAccessToken(AidlyUserDetails userDetails, long? sessionNo, IReadOnlyList<long>? roleNos,
                                      long? tokenVersion) =>
        GenerateAccessToken(userDetails, sessionNo, roleNos, tokenVersion, null);

    public string GenerateAccessToken(AidlyUserDetails userDetails, long? sessionNo, IReadOnlyList<long>? roleNos,
                                      long? tokenVersion, IReadOnlyDictionary<string, int>? permBits)
    {
        var claims = new Dictionary<string, object>
        {
            ["userNo"] = userDetails.UserNo!,
            ["companyNo"] = userDetails.CompanyNo!,
            ["branchNo"] = userDetails.BranchNo!,
            ["userName"] = userDetails.UserName,
            ["accessScope"] = userDetails.AccessScope.GetCode()
        };

        if (roleNos is { Count: > 0 })
        {
            claims["roleNo"] = roleNos[0];
            claims["roles"] = roleNos;
            claims["roleNos"] = roleNos;
        }

        if (sessionNo != null) claims["sessionNo"] = sessionNo;
        if (tokenVersion != null) claims["tokenVersion"] = tokenVersion;
        if (permBits != null) claims[PermsClaim] = permBits;

        return CreateToken(claims, userDetails.UserName, _accessTokenExpiration);
    }

    public string GenerateRefreshToken(AidlyUserDetails userDetails) =>
        GenerateRefreshToken(userDetails, null, null);

    public string GenerateRefreshToken(AidlyUserDetails userDetails, long? sessionNo, long? tokenVersion)
    {
        var claims = new Dictionary<string, object>
        {
            ["userNo"] = userDetails.UserNo!,
            ["companyNo"] = userDetails.CompanyNo!,
            ["branchNo"] = userDetails.BranchNo!,
            ["userName"] = userDetails.UserName,
            ["accessScope"] = userDetails.AccessScope.GetCode()
        };

        if (sessionNo != null) claims["sessionNo"] = sessionNo;
        if (tokenVersion != null) claims["tokenVersion"] = tokenVersion;

        return CreateToken(claims, userDetails.UserName, _refreshTokenExpiration);
    }

    public string GenerateRefreshTokenValue()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToHexStringLower(bytes);
    }

    public string HashToken(string rawToken)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexStringLower(hash);
    }

    private string CreateToken(IDictionary<string, object> claims, string subject, long expirationMs)
    {
        var now = DateTime.UtcNow;
        var handler = new JwtSecurityTokenHandler();

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[] { new Claim(JwtRegisteredClaimNames.Sub, subject) }),
            Claims = claims,
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMilliseconds(expirationMs),
            Issuer = _issuer,
            Audience = _audience,
            SigningCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256)
        };

        return handler.WriteToken(handler.CreateToken(descriptor));
    }

    // ----------------------------------------------------------------- verify --

    public bool ValidateToken(string token)
    {
        try
        {
            return !IsTokenExpired(token);
        }
        catch (SecurityTokenExpiredException)
        {
            _logger.LogWarning("JWT token expired");
            return false;
        }
        catch (Exception e) when (e is SecurityTokenException or ArgumentException)
        {
            _logger.LogWarning("Invalid JWT token: {Message}", e.Message);
            return false;
        }
    }

    public bool ValidateToken(string token, AidlyUserDetails userDetails)
    {
        try
        {
            var username = GetUsernameFromToken(token);
            return username == userDetails.UserName && !IsTokenExpired(token);
        }
        catch (SecurityTokenExpiredException)
        {
            _logger.LogWarning("JWT token expired for user: {User}", userDetails.UserName);
            return false;
        }
        catch (Exception e) when (e is SecurityTokenException or ArgumentException)
        {
            _logger.LogWarning("Invalid JWT token: {Message}", e.Message);
            return false;
        }
    }

    // ------------------------------------------------------------------- read --

    public string? GetUsernameFromToken(string token) =>
        GetAllClaims(token).FindFirst(JwtRegisteredClaimNames.Sub)?.Value
        ?? GetAllClaims(token).FindFirst(ClaimTypes.NameIdentifier)?.Value;

    public long? GetCompanyNoFromToken(string token) => GetLongClaim(token, "companyNo");

    public long? GetBranchNoFromToken(string token) => GetLongClaim(token, "branchNo");

    public long? GetUserNoFromToken(string token) => GetLongClaim(token, "userNo");

    public long? GetSessionNoFromToken(string token) => GetLongClaim(token, "sessionNo");

    public long? GetTokenVersionFromToken(string token) => GetLongClaim(token, "tokenVersion");

    public AccessScope GetAccessScopeFromToken(string token) =>
        AccessScopeExtensions.FromCode(GetLongClaim(token, "accessScope"));

    public IReadOnlyDictionary<string, int>? GetPermsFromToken(string token)
    {
        var jwt = ReadValidated(token);
        if (!jwt.Payload.TryGetValue(PermsClaim, out var raw) || raw is null) return null;

        var perms = new Dictionary<string, int>();

        switch (raw)
        {
            case JsonElement { ValueKind: JsonValueKind.Object } element:
                foreach (var prop in element.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetInt32(out var v))
                        perms[prop.Name] = v;
                }
                break;

            case IDictionary<string, object> map:
                foreach (var (key, value) in map)
                {
                    if (key is null || value is null) continue;
                    if (int.TryParse(value.ToString(), out var v)) perms[key] = v;
                }
                break;

            default:
                return null;
        }

        return perms;
    }

    private long? GetLongClaim(string token, string claimName)
    {
        var jwt = ReadValidated(token);
        if (!jwt.Payload.TryGetValue(claimName, out var value) || value is null) return null;

        return value switch
        {
            JsonElement { ValueKind: JsonValueKind.Number } e => e.GetInt64(),
            JsonElement { ValueKind: JsonValueKind.String } e =>
                long.TryParse(e.GetString(), out var parsed) ? parsed : null,
            long l => l,
            int i => i,
            _ => long.TryParse(value.ToString(), out var parsed) ? parsed : null
        };
    }

    private bool IsTokenExpired(string token) => ReadValidated(token).ValidTo < DateTime.UtcNow;

    private ClaimsPrincipal GetAllClaims(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        return handler.ValidateToken(token, ValidationParameters(validateLifetime: false), out _);
    }

    /// <summary>
    /// Parses and signature-verifies the token, returning the raw JWT so payload claims can be
    /// read with their original JSON types (the <c>perms</c> map in particular).
    /// Lifetime is validated separately by <see cref="IsTokenExpired"/>, matching the Java
    /// service where <c>parseSignedClaims</c> throws only on signature/format problems.
    /// </summary>
    private JwtSecurityToken ReadValidated(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        handler.ValidateToken(token, ValidationParameters(validateLifetime: false), out var validated);
        return (JwtSecurityToken)validated;
    }

    private TokenValidationParameters ValidationParameters(bool validateLifetime) => new()
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = _signingKey,
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = validateLifetime,
        ClockSkew = TimeSpan.Zero
    };

    // -------------------------------------------------------- legacy overloads --

    public string GenerateAccessToken(long userNo, string userId, string userName, long companyNo, long branchNo,
                                      short accessScope) =>
        GenerateAccessToken(new AidlyUserDetails(
            userNo, companyNo, branchNo, AccessScopeExtensions.FromCode(accessScope), userName, string.Empty));

    public string GenerateRefreshToken(long userNo) =>
        GenerateRefreshToken(new AidlyUserDetails(userNo, null, null, string.Empty, string.Empty));

    public bool ValidateToken(string token, out long userNo, out long companyNo, out long branchNo)
    {
        userNo = 0;
        companyNo = 0;
        branchNo = 0;

        try
        {
            var handler = new JwtSecurityTokenHandler();
            handler.ValidateToken(token, ValidationParameters(validateLifetime: true), out _);

            userNo = GetUserNoFromToken(token) ?? 0;
            companyNo = GetCompanyNoFromToken(token) ?? 0;
            branchNo = GetBranchNoFromToken(token) ?? 0;

            return userNo > 0;
        }
        catch
        {
            return false;
        }
    }
}
