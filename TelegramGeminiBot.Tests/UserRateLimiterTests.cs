using TelegramGeminiBot.Services;

namespace TelegramGeminiBot.Tests;

public class UserRateLimiterTests
{
    private sealed class ManualTimeProvider : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public void BlocksAfterLimit_UntilWindowElapses()
    {
        var clock = new ManualTimeProvider();
        var limiter = new UserRateLimiter(2, TimeSpan.FromMinutes(1), clock);

        Assert.True(limiter.TryAcquire(1));
        Assert.True(limiter.TryAcquire(1));
        Assert.False(limiter.TryAcquire(1));

        clock.Now += TimeSpan.FromMinutes(1);

        Assert.True(limiter.TryAcquire(1));
    }

    [Fact]
    public void LimitsAreTrackedPerUser()
    {
        var limiter = new UserRateLimiter(1, TimeSpan.FromMinutes(1), new ManualTimeProvider());

        Assert.True(limiter.TryAcquire(1));
        Assert.False(limiter.TryAcquire(1));
        Assert.True(limiter.TryAcquire(2));
    }

    [Fact]
    public void ZeroLimit_DisablesLimiting()
    {
        var limiter = new UserRateLimiter(0, TimeSpan.FromMinutes(1), new ManualTimeProvider());

        Assert.All(Enumerable.Range(0, 100), _ => Assert.True(limiter.TryAcquire(1)));
    }
}
