using AidlyErp.Application.Common.Exceptions;
using AidlyErp.Application.Common.Interfaces;
using AidlyErp.Application.Common.Security;
using AidlyErp.Application.Sys.Dto;
using AidlyErp.Domain.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AidlyErp.Application.Sys.Services;

public interface ISys1109Service
{
    Task<List<Sys1109SessionDto>> GetSessionsAsync(int? limit, CancellationToken cancellationToken = default);

    Task<List<Sys1109LoginAttemptDto>> GetLoginAttemptsAsync(int? limit, CancellationToken cancellationToken = default);

    Task RevokeSessionAsync(long sessionNo, CancellationToken cancellationToken = default);
}

/// <summary>
/// SYS1109 Session/Login Monitor — read-only views over <c>sys_session</c> and
/// <c>sys_login_attempt</c>, scoped to the active company, plus an admin force-logout
/// (single-session revoke).
/// </summary>
public class Sys1109Service : ISys1109Service
{
    private const short Deleted = 0;
    private const int MaxRows = 500;
    private const int DefaultRows = 200;

    private readonly IApplicationDbContext _db;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICompanyBranchContext _ctx;
    private readonly ILogger<Sys1109Service> _logger;

    public Sys1109Service(IApplicationDbContext db, IUnitOfWork unitOfWork, ICompanyBranchContext ctx,
                          ILogger<Sys1109Service> logger)
    {
        _db = db;
        _unitOfWork = unitOfWork;
        _ctx = ctx;
        _logger = logger;
    }

    public async Task<List<Sys1109SessionDto>> GetSessionsAsync(int? limit,
                                                                CancellationToken cancellationToken = default)
    {
        var rows = await FindSessionsForCompanyAsync(Company(), Cap(limit), cancellationToken);
        return rows.Select(ToSessionDto).ToList();
    }

    public async Task<List<Sys1109LoginAttemptDto>> GetLoginAttemptsAsync(int? limit,
                                                                          CancellationToken cancellationToken = default)
    {
        var rows = await FindRecentAttemptsForCompanyAsync(Company(), Cap(limit), cancellationToken);
        return rows.Select(ToAttemptDto).ToList();
    }

    public Task RevokeSessionAsync(long sessionNo, CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(async ct =>
        {
            var session = await _db.SysSessions.FirstOrDefaultAsync(s => s.SessionNo == sessionNo, ct)
                          ?? throw new NotFoundException("Session not found");

            if (!await BelongsToCompanyAsync(session, Company(), ct))
            {
                throw new ValidationException("Session does not belong to your company");
            }

            // Guarded update: a session already revoked matches nothing, so a double-revoke is
            // reported rather than silently succeeding.
            if (session.IsRevoked != 0)
            {
                throw new ValidationException("Session is already closed");
            }

            var now = DateTime.UtcNow;
            session.IsRevoked = 1;
            session.RevokedAt = now;
            session.LogoutAt = now;

            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("SYS1109: session {SessionNo} revoked by userNo={UserNo}",
                sessionNo, _ctx.UserNo);
        }, cancellationToken);

    // ── helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// A session belongs to the company when its <c>active_company_no</c> matches; when that
    /// column is null the owning user's home company decides.
    /// </summary>
    private async Task<bool> BelongsToCompanyAsync(SysSession s, long companyNo, CancellationToken ct)
    {
        if (s.ActiveCompanyNo == companyNo) return true;
        if (s.ActiveCompanyNo != null) return false;

        return await _db.Users
            .AsNoTracking()
            .Where(u => u.UserNo == s.UserNo && u.IsDeleted == Deleted)
            .Select(u => u.CompanyNo)
            .FirstOrDefaultAsync(ct) == companyNo;
    }

    private long Company() =>
        _ctx.CompanyNo ?? throw new ValidationException("No active company in context");

    private static int Cap(int? limit) => limit is > 0 and <= MaxRows ? limit.Value : DefaultRows;

    /// <summary>Not revoked, not logged out, and not past its expiry.</summary>
    private static bool IsLive(SessionRow r)
    {
        var notRevoked = r.IsRevoked is null or 0;
        var notLoggedOut = r.LogoutAt == null;
        var notExpired = r.ExpiredAt == null || r.ExpiredAt > DateTime.UtcNow;
        return notRevoked && notLoggedOut && notExpired;
    }

    /// <summary>
    /// Live sessions first, then most recent. Scoped to a company by the session's active company
    /// or, when that is null, the owning user's home company. Ported verbatim from
    /// <c>SysSessionRepository.findSessionsForCompany</c> — the CASE-based ordering is what puts
    /// live sessions at the top.
    /// </summary>
    private Task<List<SessionRow>> FindSessionsForCompanyAsync(long companyNo, int limit, CancellationToken ct) =>
        _db.Database.SqlQueryRaw<SessionRow>(
            """
            SELECT s.session_no        AS "SessionNo",
                   s.user_no           AS "UserNo",
                   u.user_id           AS "UserId",
                   u.user_name         AS "UserName",
                   s.ip_address        AS "IpAddress",
                   s.user_agent        AS "UserAgent",
                   s.active_branch_no  AS "ActiveBranchNo",
                   s.active_company_no AS "ActiveCompanyNo",
                   s.login_at          AS "LoginAt",
                   s.last_seen_at      AS "LastSeenAt",
                   s.expired_at        AS "ExpiredAt",
                   s.logout_at         AS "LogoutAt",
                   s.is_revoked        AS "IsRevoked"
            FROM   sys_session s
            JOIN   sys_user u ON u.user_no = s.user_no
            WHERE  (s.active_company_no = {0}
                    OR (s.active_company_no IS NULL AND u.company_no = {0}))
            ORDER  BY (CASE WHEN s.is_revoked = 0 AND s.logout_at IS NULL
                              AND (s.expired_at IS NULL OR s.expired_at > CURRENT_TIMESTAMP)
                            THEN 0 ELSE 1 END),
                     s.login_at DESC
            LIMIT {1}
            """, companyNo, limit).ToListAsync(ct);

    /// <summary>
    /// Recent login attempts, joined to the user so only attempts against known users of this
    /// company are shown. Ported verbatim from <c>LoginAttemptRepository.findRecentForCompany</c>.
    /// </summary>
    private Task<List<AttemptRow>> FindRecentAttemptsForCompanyAsync(long companyNo, int limit,
                                                                     CancellationToken ct) =>
        _db.Database.SqlQueryRaw<AttemptRow>(
            """
            SELECT la.login_attempt_no AS "LoginAttemptNo",
                   la.user_id          AS "UserId",
                   u.user_name         AS "UserName",
                   la.ip_address       AS "IpAddress",
                   la.is_success       AS "IsSuccess",
                   la.fail_reason      AS "FailReason",
                   la.attempted_at     AS "AttemptedAt"
            FROM   sys_login_attempt la
            JOIN   sys_user u ON u.user_id = la.user_id
                             AND u.company_no = {0}
                             AND u.is_deleted = 0
            ORDER  BY la.attempted_at DESC
            LIMIT {1}
            """, companyNo, limit).ToListAsync(ct);

    private static Sys1109SessionDto ToSessionDto(SessionRow r) => new()
    {
        SessionNo = r.SessionNo,
        UserNo = r.UserNo,
        UserId = r.UserId,
        UserName = r.UserName,
        IpAddress = r.IpAddress,
        UserAgent = r.UserAgent,
        ActiveBranchNo = r.ActiveBranchNo,
        ActiveCompanyNo = r.ActiveCompanyNo,
        LoginAt = r.LoginAt,
        LastSeenAt = r.LastSeenAt,
        ExpiredAt = r.ExpiredAt,
        LogoutAt = r.LogoutAt,
        IsRevoked = r.IsRevoked,
        IsLive = IsLive(r)
    };

    private static Sys1109LoginAttemptDto ToAttemptDto(AttemptRow r) => new()
    {
        LoginAttemptNo = r.LoginAttemptNo,
        UserId = r.UserId,
        UserName = r.UserName,
        IpAddress = r.IpAddress,
        IsSuccess = r.IsSuccess,
        FailReason = r.FailReason,
        AttemptedAt = r.AttemptedAt
    };

    /// <summary>Projection for <see cref="FindSessionsForCompanyAsync"/>; names match the SQL aliases.</summary>
    private sealed class SessionRow
    {
        public long SessionNo { get; set; }
        public long? UserNo { get; set; }
        public string? UserId { get; set; }
        public string? UserName { get; set; }
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
        public long? ActiveBranchNo { get; set; }
        public long? ActiveCompanyNo { get; set; }
        public DateTime? LoginAt { get; set; }
        public DateTime? LastSeenAt { get; set; }
        public DateTime? ExpiredAt { get; set; }
        public DateTime? LogoutAt { get; set; }
        public short? IsRevoked { get; set; }
    }

    /// <summary>Projection for <see cref="FindRecentAttemptsForCompanyAsync"/>.</summary>
    private sealed class AttemptRow
    {
        public long LoginAttemptNo { get; set; }
        public string? UserId { get; set; }
        public string? UserName { get; set; }
        public string? IpAddress { get; set; }
        public short? IsSuccess { get; set; }
        public string? FailReason { get; set; }
        public DateTime? AttemptedAt { get; set; }
    }
}
