using AidlyErp.Application.Auth.Dto;
using Microsoft.Extensions.Configuration;

namespace AidlyErp.Application.Auth.Services;

public interface IAuthConfigService
{
    /// <summary>Token/session timings the client needs in order to schedule its own refreshes.</summary>
    AuthConfigResponse GetClientConfig();
}

/// <summary>Port of <c>core.auth.service.AuthConfigService</c>.</summary>
public class AuthConfigService : IAuthConfigService
{
    private readonly long _accessTokenExpirationMs;
    private readonly long _refreshTokenExpirationMs;
    private readonly long _accessTokenRefreshBeforeMs;
    private readonly long _inactivityTimeoutMs;

    public AuthConfigService(IConfiguration configuration)
    {
        _accessTokenExpirationMs = Read(configuration,
            "Aidly:Jwt:AccessTokenExpiration", "Aidly:Jwt:AccessTokenExpirationMs", 300_000L);
        _refreshTokenExpirationMs = Read(configuration,
            "Aidly:Jwt:RefreshTokenExpiration", "Aidly:Jwt:RefreshTokenExpirationMs", 604_800_000L);
        _accessTokenRefreshBeforeMs = Read(configuration,
            "Aidly:Jwt:AccessTokenRefreshBefore", "Aidly:Jwt:AccessTokenRefreshBeforeMs", 30_000L);
        _inactivityTimeoutMs = Read(configuration,
            "Aidly:Session:InactivityTimeout", "Aidly:Session:InactivityTimeoutMs", 900_000L);
    }

    public AuthConfigResponse GetClientConfig() => new()
    {
        AccessTokenExpirationMs = _accessTokenExpirationMs,
        RefreshTokenExpirationMs = _refreshTokenExpirationMs,
        AccessTokenRefreshBeforeMs = _accessTokenRefreshBeforeMs,
        InactivityTimeoutMs = _inactivityTimeoutMs
    };

    private static long Read(IConfiguration configuration, string primaryKey, string fallbackKey, long defaultValue)
    {
        if (long.TryParse(configuration[primaryKey], out var primary)) return primary;
        if (long.TryParse(configuration[fallbackKey], out var fallback)) return fallback;
        return defaultValue;
    }
}
