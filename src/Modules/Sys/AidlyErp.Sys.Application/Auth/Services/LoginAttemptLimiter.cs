using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;

namespace AidlyErp.Sys.Application.Auth.Services;

/// <summary>
/// In-memory sliding-window rate limiter for login attempts, keyed on
/// (username, ip-address). Port of <c>core.auth.service.LoginAttemptLimiter</c>.
///
/// <para>Registered as a singleton — the Java <c>@Component</c> is likewise application-scoped,
/// and the counter map must outlive individual requests to be meaningful.</para>
/// </summary>
public class LoginAttemptLimiter
{
    private readonly int _maxAttempts;
    private readonly TimeSpan _window;
    private readonly TimeProvider _clock;
    private readonly ConcurrentDictionary<string, AttemptWindow> _attempts = new();

    public LoginAttemptLimiter(IConfiguration configuration)
        : this(
            int.TryParse(configuration["Aidly:Security:LoginRateLimit:MaxAttempts"], out var max) ? max : 10,
            TimeSpan.FromSeconds(
                long.TryParse(configuration["Aidly:Security:LoginRateLimit:WindowSeconds"], out var secs) ? secs : 900),
            TimeProvider.System)
    {
    }

    internal LoginAttemptLimiter(int maxAttempts, TimeSpan window, TimeProvider clock)
    {
        _maxAttempts = maxAttempts;
        _window = window;
        _clock = clock;
    }

    public bool IsBlocked(string? username, string? ipAddress)
    {
        var key = Key(username, ipAddress);
        if (!_attempts.TryGetValue(key, out var current))
        {
            return false;
        }

        if (current.ExpiresAt < _clock.GetUtcNow())
        {
            // Window elapsed — drop it, but only if it has not been replaced in the meantime.
            ((ICollection<KeyValuePair<string, AttemptWindow>>)_attempts)
                .Remove(new KeyValuePair<string, AttemptWindow>(key, current));
            return false;
        }

        return current.Count >= _maxAttempts;
    }

    public void RecordFailure(string? username, string? ipAddress)
    {
        var now = _clock.GetUtcNow();
        _attempts.AddOrUpdate(
            Key(username, ipAddress),
            _ => new AttemptWindow(1, now.Add(_window)),
            (_, current) => current.ExpiresAt < now
                ? new AttemptWindow(1, now.Add(_window))
                : new AttemptWindow(current.Count + 1, current.ExpiresAt));
    }

    public void Reset(string? username, string? ipAddress) => _attempts.TryRemove(Key(username, ipAddress), out _);

    private static string Key(string? username, string? ipAddress) =>
        $"{Normalize(username)}|{Normalize(ipAddress)}";

    private static string Normalize(string? value) => value?.Trim().ToLowerInvariant() ?? string.Empty;

    private sealed record AttemptWindow(int Count, DateTimeOffset ExpiresAt);
}
