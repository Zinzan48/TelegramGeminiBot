using System.Collections.Concurrent;

namespace TelegramGeminiBot.Services;

/// <summary>
/// Fixed-window, in-memory limiter keyed by Telegram user ID.
/// </summary>
public sealed class UserRateLimiter
{
    private readonly int _permitLimit;
    private readonly TimeSpan _window;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<long, Window> _windows = new();

    public UserRateLimiter(int permitLimit, TimeSpan window, TimeProvider timeProvider)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(permitLimit);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(window, TimeSpan.Zero);
        _permitLimit = permitLimit;
        _window = window;
        _timeProvider = timeProvider;
    }

    /// <summary>Returns true when the user may send another prompt now.</summary>
    public bool TryAcquire(long userId)
    {
        if (_permitLimit == 0)
        {
            return true;
        }

        var now = _timeProvider.GetUtcNow();
        var window = _windows.GetOrAdd(userId, _ => new Window(now));

        lock (window)
        {
            if (now - window.Start >= _window)
            {
                window.Start = now;
                window.Count = 0;
            }

            if (window.Count >= _permitLimit)
            {
                return false;
            }

            window.Count++;
        }

        if (_windows.Count > 10_000)
        {
            PruneExpired(now);
        }

        return true;
    }

    private void PruneExpired(DateTimeOffset now)
    {
        foreach (var (key, window) in _windows)
        {
            if (now - window.Start >= _window)
            {
                _windows.TryRemove(key, out _);
            }
        }
    }

    private sealed class Window(DateTimeOffset start)
    {
        public DateTimeOffset Start { get; set; } = start;
        public int Count { get; set; }
    }
}
