using Microsoft.Data.Sqlite;
using TelegramGeminiBot.Storage;

namespace TelegramGeminiBot.Tests;

public sealed class ChatHistoryStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "tgb-tests-" + Guid.NewGuid().ToString("N"));
    private readonly ChatHistoryStore _store;

    public ChatHistoryStoreTests()
    {
        _store = new ChatHistoryStore(Path.Combine(_directory, "history.db"));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // Best effort cleanup of the temp database.
        }
    }

    [Fact]
    public async Task MessagesSharingATimestamp_KeepInsertionOrder()
    {
        await _store.InitializeAsync(CancellationToken.None);
        var now = DateTimeOffset.UtcNow;

        for (var i = 0; i < 3; i++)
        {
            await _store.AddMessageAsync(1, "User", $"q{i}", now, CancellationToken.None);
            await _store.AddMessageAsync(1, "Assistant", $"a{i}", now, CancellationToken.None);
        }

        var history = await _store.GetRecentMessagesAsync(1, 10, CancellationToken.None);

        Assert.Equal(["q0", "a0", "q1", "a1", "q2", "a2"], history.Select(m => m.Content));
    }

    [Fact]
    public async Task Trim_KeepsOnlyMostRecentMessagesOfThatChat()
    {
        await _store.InitializeAsync(CancellationToken.None);
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < 5; i++)
        {
            await _store.AddMessageAsync(1, "User", $"m{i}", now, CancellationToken.None);
        }

        await _store.AddMessageAsync(2, "User", "other chat", now, CancellationToken.None);

        await _store.TrimHistoryAsync(1, 2, CancellationToken.None);

        Assert.Equal(["m3", "m4"], (await _store.GetRecentMessagesAsync(1, 10, CancellationToken.None)).Select(m => m.Content));
        Assert.Single(await _store.GetRecentMessagesAsync(2, 10, CancellationToken.None));
    }
}
