using System.Collections.Concurrent;

namespace TelegramGeminiBot.Services;

public sealed class ChatQueue
{
    private readonly ConcurrentDictionary<long, SemaphoreSlim> _locks = new();

    public async Task<IDisposable> AcquireAsync(long chatId, CancellationToken cancellationToken)
    {
        var semaphore = _locks.GetOrAdd(chatId, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);
        return new Releaser(chatId, semaphore, _locks);
    }

    private sealed class Releaser : IDisposable
    {
        private readonly long _chatId;
        private readonly SemaphoreSlim _semaphore;
        private readonly ConcurrentDictionary<long, SemaphoreSlim> _locks;
        private bool _disposed;

        public Releaser(long chatId, SemaphoreSlim semaphore, ConcurrentDictionary<long, SemaphoreSlim> locks)
        {
            _chatId = chatId;
            _semaphore = semaphore;
            _locks = locks;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _semaphore.Release();

            if (_semaphore.CurrentCount == 1)
            {
                _locks.TryRemove(_chatId, out _);
            }
        }
    }
}
