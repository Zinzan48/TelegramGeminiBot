namespace TelegramGeminiBot.Services;

/// <summary>
/// Serializes work per chat so one chat's history is never read and written concurrently.
/// Entries are reference-counted and removed only when nobody holds or waits for them.
/// </summary>
public sealed class ChatQueue
{
    private readonly Dictionary<long, Entry> _entries = new();
    private readonly object _sync = new();

    public async Task<IDisposable> AcquireAsync(long chatId, CancellationToken cancellationToken)
    {
        Entry entry;
        lock (_sync)
        {
            if (!_entries.TryGetValue(chatId, out entry!))
            {
                entry = new Entry();
                _entries[chatId] = entry;
            }

            entry.References++;
        }

        try
        {
            await entry.Semaphore.WaitAsync(cancellationToken);
        }
        catch
        {
            Release(chatId, entry, semaphoreHeld: false);
            throw;
        }

        return new Releaser(this, chatId, entry);
    }

    /// <summary>Number of chats currently holding or waiting for a lock (for tests and diagnostics).</summary>
    public int ActiveChats
    {
        get
        {
            lock (_sync)
            {
                return _entries.Count;
            }
        }
    }

    private void Release(long chatId, Entry entry, bool semaphoreHeld)
    {
        if (semaphoreHeld)
        {
            entry.Semaphore.Release();
        }

        lock (_sync)
        {
            entry.References--;
            if (entry.References == 0)
            {
                _entries.Remove(chatId);
                entry.Semaphore.Dispose();
            }
        }
    }

    private sealed class Entry
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);
        public int References { get; set; }
    }

    private sealed class Releaser(ChatQueue owner, long chatId, Entry entry) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                owner.Release(chatId, entry, semaphoreHeld: true);
            }
        }
    }
}
