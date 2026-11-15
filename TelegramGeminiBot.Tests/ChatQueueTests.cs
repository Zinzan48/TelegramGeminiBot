using TelegramGeminiBot.Services;

namespace TelegramGeminiBot.Tests;

public class ChatQueueTests
{
    [Fact]
    public async Task SameChat_IsSerialized()
    {
        var queue = new ChatQueue();
        var first = await queue.AcquireAsync(1, CancellationToken.None);

        var secondTask = queue.AcquireAsync(1, CancellationToken.None);
        await Task.Delay(50);
        Assert.False(secondTask.IsCompleted);

        first.Dispose();
        using var second = await secondTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task DifferentChats_DoNotBlockEachOther()
    {
        var queue = new ChatQueue();
        using var a = await queue.AcquireAsync(1, CancellationToken.None);

        using var b = await queue.AcquireAsync(2, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Entries_AreRemovedWhenNobodyHoldsOrWaits()
    {
        var queue = new ChatQueue();
        var first = await queue.AcquireAsync(1, CancellationToken.None);
        var secondTask = queue.AcquireAsync(1, CancellationToken.None);

        first.Dispose();
        Assert.Equal(1, queue.ActiveChats); // the waiter still holds a reference

        (await secondTask).Dispose();
        Assert.Equal(0, queue.ActiveChats);
    }

    [Fact]
    public async Task CancelledWaiter_ReleasesItsReference()
    {
        var queue = new ChatQueue();
        var first = await queue.AcquireAsync(1, CancellationToken.None);
        using var cts = new CancellationTokenSource();

        var waiter = queue.AcquireAsync(1, cts.Token);
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiter);

        first.Dispose();
        Assert.Equal(0, queue.ActiveChats);
    }

    [Fact]
    public async Task ConcurrentHandlers_ForOneChat_NeverOverlap()
    {
        var queue = new ChatQueue();
        var active = 0;
        var maxActive = 0;

        await Task.WhenAll(Enumerable.Range(0, 50).Select(async _ =>
        {
            using var gate = await queue.AcquireAsync(7, CancellationToken.None);
            var now = Interlocked.Increment(ref active);
            InterlockedMax(ref maxActive, now);
            await Task.Yield();
            Interlocked.Decrement(ref active);
        }));

        Assert.Equal(1, maxActive);
        Assert.Equal(0, queue.ActiveChats);
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int current;
        while ((current = Volatile.Read(ref target)) < value &&
               Interlocked.CompareExchange(ref target, value, current) != current)
        {
        }
    }
}
