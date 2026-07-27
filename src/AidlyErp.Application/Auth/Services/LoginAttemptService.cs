using AidlyErp.Application.Common.Interfaces;
using AidlyErp.Domain.Sys;
using Microsoft.Extensions.Logging;

namespace AidlyErp.Application.Auth.Services;

public interface ILoginAttemptService
{
    Task RecordAsync(string? userId, string? ipAddress, bool success, string? failReason,
                     CancellationToken cancellationToken = default);
}

/// <summary>
/// Persists a <c>sys_login_attempt</c> row for every login outcome.
///
/// <para>The Java service runs this in its own transaction
/// (<c>Propagation.REQUIRES_NEW</c>) so the audit row commits even when the surrounding login
/// transaction rolls back on a failed attempt. Here the same isolation is achieved by writing
/// through a separate <see cref="IApplicationDbContext"/> scope, created per call.</para>
///
/// <para>Never throws — auditing must not break the login flow.</para>
/// </summary>
public class LoginAttemptService : ILoginAttemptService
{
    private readonly IApplicationDbContextFactory _contextFactory;
    private readonly ILogger<LoginAttemptService> _logger;

    public LoginAttemptService(IApplicationDbContextFactory contextFactory, ILogger<LoginAttemptService> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task RecordAsync(string? userId, string? ipAddress, bool success, string? failReason,
                                  CancellationToken cancellationToken = default)
    {
        try
        {
            // Separate scope == separate transaction, so this row survives a rollback of the
            // failed login it is recording.
            await using var scope = _contextFactory.CreateScope();

            scope.Context.LoginAttempts.Add(new LoginAttempt
            {
                UserId = Truncate(string.IsNullOrWhiteSpace(userId) ? "?" : userId, 50)!,
                IpAddress = Truncate(ipAddress, 45),
                IsSuccess = (short)(success ? 1 : 0),
                FailReason = success ? null : Truncate(failReason, 60),
                AttemptedAt = DateTime.UtcNow
            });

            await scope.Context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Could not persist login attempt for user_id={UserId}: {Message}", userId, ex.Message);
        }
    }

    private static string? Truncate(string? value, int max)
    {
        if (value == null) return null;
        return value.Length <= max ? value : value[..max];
    }
}
