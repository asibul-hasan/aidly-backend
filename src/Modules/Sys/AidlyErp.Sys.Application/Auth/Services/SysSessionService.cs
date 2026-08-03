using AidlyErp.Shared.Core.Abstractions;
using AidlyErp.Shared.Contracts;
using AidlyErp.Sys.Contracts;
using AidlyErp.Sys.Application.Interfaces;
using AidlyErp.Hrm.Contracts;
using AidlyErp.Sys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AidlyErp.Sys.Application.Auth.Services;

public interface ISysSessionService
{
    Task<SysSession> CreateSessionAsync(long userNo, long? activeBranchNo, long? activeCompanyNo,
                                        short? accessScopeCode, int? tokenVersion,
                                        string? ipAddress, string? userAgent,
                                        CancellationToken cancellationToken = default);

    Task StoreRefreshTokenHashAsync(long sessionNo, string tokenHash, DateTime expiredAt,
                                    CancellationToken cancellationToken = default);

    Task<SysSession> ValidateRefreshTokenAsync(long sessionNo, long userNo, string tokenHash,
                                               CancellationToken cancellationToken = default);

    Task RotateRefreshTokenHashAsync(long sessionNo, string oldTokenHash, string newTokenHash,
                                     DateTime expiredAt, CancellationToken cancellationToken = default);

    Task<int> UpdateActiveBranchAsync(long sessionNo, long branchNo, CancellationToken cancellationToken = default);

    Task CloseSessionsForUserAsync(long userNo, CancellationToken cancellationToken = default);
}

/// <summary>
/// Session lifecycle service operating on the <c>sys_session</c> schema. Port of
/// <c>core.auth.service.SysSessionService</c>.
///
/// <para><c>session_uuid</c> is the externally-visible identifier (it lives in the JWT <c>sid</c>
/// claim); <c>session_token_hash</c> is a SHA-256 of the refresh token and is what is looked up
/// server-side for refresh / revocation.</para>
/// </summary>
public class SysSessionService : ISysSessionService
{
    private readonly ISysDbContext _db;
    private readonly ILogger<SysSessionService> _logger;

    public SysSessionService(ISysDbContext db, ILogger<SysSessionService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<SysSession> CreateSessionAsync(long userNo, long? activeBranchNo, long? activeCompanyNo,
                                                     short? accessScopeCode, int? tokenVersion,
                                                     string? ipAddress, string? userAgent,
                                                     CancellationToken cancellationToken = default)
    {
        var session = new SysSession
        {
            UserNo = userNo,
            ActiveBranchNo = activeBranchNo,
            ActiveCompanyNo = activeCompanyNo,
            AccessScope = accessScopeCode,
            TokenVersion = tokenVersion,
            SessionUuid = Guid.NewGuid(),
            // Placeholder token-hash; replaced with the real SHA-256 of the issued refresh token.
            SessionTokenHash = "pending-" + Guid.NewGuid().ToString("N"),
            IpAddress = ipAddress,
            UserAgent = userAgent is { Length: > 1000 } ? userAgent[..1000] : userAgent
        };

        _db.SysSessions.Add(session);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Session created: sessionNo={SessionNo}, userNo={UserNo}, branchNo={BranchNo}",
            session.SessionNo, userNo, activeBranchNo);

        return session;
    }

    public async Task StoreRefreshTokenHashAsync(long sessionNo, string tokenHash, DateTime expiredAt,
                                                 CancellationToken cancellationToken = default)
    {
        var session = await _db.SysSessions.FirstOrDefaultAsync(s => s.SessionNo == sessionNo, cancellationToken);
        if (session == null)
        {
            throw new InvalidOperationException($"Could not attach refresh token to session: sessionNo={sessionNo}");
        }

        session.SessionTokenHash = tokenHash;
        session.ExpiredAt = expiredAt;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<SysSession> ValidateRefreshTokenAsync(long sessionNo, long userNo, string tokenHash,
                                                            CancellationToken cancellationToken = default)
    {
        var session = await _db.SysSessions.FirstOrDefaultAsync(s => s.SessionNo == sessionNo, cancellationToken);

        if (session == null || !session.IsLive())
        {
            throw new InvalidOperationException("Refresh session is not active");
        }

        if (session.UserNo != userNo)
        {
            throw new InvalidOperationException("Refresh token user does not match session owner");
        }

        if (!string.Equals(session.SessionTokenHash, tokenHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Refresh token has already been rotated");
        }

        if (session.ExpiredAt.HasValue && session.ExpiredAt.Value < DateTime.UtcNow)
        {
            throw new InvalidOperationException("Refresh session has expired");
        }

        return session;
    }

    public async Task RotateRefreshTokenHashAsync(long sessionNo, string oldTokenHash, string newTokenHash,
                                                  DateTime expiredAt, CancellationToken cancellationToken = default)
    {
        // Conditional on the old hash — two concurrent refreshes must not both succeed.
        var session = await _db.SysSessions.FirstOrDefaultAsync(
            s => s.SessionNo == sessionNo && s.SessionTokenHash == oldTokenHash, cancellationToken);

        if (session == null)
        {
            throw new InvalidOperationException("Refresh token rotation failed");
        }

        session.SessionTokenHash = newTokenHash;
        session.ExpiredAt = expiredAt;
        session.LastSeenAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> UpdateActiveBranchAsync(long sessionNo, long branchNo,
                                                   CancellationToken cancellationToken = default)
    {
        var session = await _db.SysSessions.FirstOrDefaultAsync(s => s.SessionNo == sessionNo, cancellationToken);
        if (session == null) return 0;

        session.ActiveBranchNo = branchNo;
        await _db.SaveChangesAsync(cancellationToken);
        return 1;
    }

    public async Task CloseSessionsForUserAsync(long userNo, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var live = await _db.SysSessions
            .Where(s => s.UserNo == userNo && s.IsRevoked == 0 && s.LogoutAt == null)
            .ToListAsync(cancellationToken);

        foreach (var session in live)
        {
            session.IsRevoked = 1;
            session.RevokedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Revoked {Count} live session(s) for userNo={UserNo}", live.Count, userNo);
    }
}
