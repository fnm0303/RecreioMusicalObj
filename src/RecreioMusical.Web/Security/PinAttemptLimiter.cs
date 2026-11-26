using System.Collections.Concurrent;

namespace RecreioMusical.Web.Security;

/// <summary>
/// Limita tentativas erradas de PIN por origem. Sem isso, um PIN curto poderia ser
/// descoberto por tentativa e erro.
/// </summary>
public sealed class PinAttemptLimiter
{
    public const int MaxFailures = 5;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<string, Attempt> _attempts = new(StringComparer.Ordinal);
    private readonly TimeProvider _timeProvider;

    public PinAttemptLimiter(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
    }

    public bool IsBlocked(string key, out TimeSpan retryAfter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        retryAfter = TimeSpan.Zero;

        if (!_attempts.TryGetValue(key, out var attempt))
        {
            return false;
        }

        lock (attempt)
        {
            var now = _timeProvider.GetUtcNow();

            if (now - attempt.WindowStart >= Window)
            {
                attempt.Failures = 0;
                attempt.WindowStart = now;
                return false;
            }

            if (attempt.Failures < MaxFailures)
            {
                return false;
            }

            retryAfter = attempt.WindowStart + Window - now;
            return true;
        }
    }

    public void RecordFailure(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var now = _timeProvider.GetUtcNow();
        var attempt = _attempts.GetOrAdd(key, _ => new Attempt(now));

        lock (attempt)
        {
            if (now - attempt.WindowStart >= Window)
            {
                attempt.WindowStart = now;
                attempt.Failures = 0;
            }

            attempt.Failures++;
        }
    }

    public void Reset(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _attempts.TryRemove(key, out _);
    }

    private sealed class Attempt
    {
        public Attempt(DateTimeOffset windowStart) => WindowStart = windowStart;

        public DateTimeOffset WindowStart { get; set; }

        public int Failures { get; set; }
    }
}
