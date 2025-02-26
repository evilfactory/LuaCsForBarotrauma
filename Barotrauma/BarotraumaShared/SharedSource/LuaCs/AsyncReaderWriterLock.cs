using System;
using System.Threading;
using System.Threading.Tasks;

namespace Barotrauma.LuaCs;


// taken from: <https://gist.github.com/cajuncoding/a88f0d00847dcfc241ae80d1c7bafb1e?permalink_comment_id=4498792>
public sealed class AsyncReaderWriterLock : IDisposable
{
    readonly SemaphoreSlim _readSemaphore = new SemaphoreSlim(1, 1);
    readonly SemaphoreSlim _writeSemaphore = new SemaphoreSlim(1, 1);
    int _readerCount;

    public async Task<IDisposable> AcquireWriterLock(CancellationToken token = default)
    {
        await _writeSemaphore.WaitAsync(token).ConfigureAwait(false);
        try
        {
            await _readSemaphore.WaitAsync(token).ConfigureAwait(false);
        }
        catch
        {
            _writeSemaphore.Release();
            throw;
        }

        return new LockToken(ReleaseWriterLock);
    }

    private void ReleaseWriterLock()
    {
        _readSemaphore.Release();
        _writeSemaphore.Release();
    }

    public async Task<IDisposable> AcquireReaderLock(CancellationToken token = default)
    {
        await _writeSemaphore.WaitAsync(token).ConfigureAwait(false);
        if (Interlocked.Increment(ref _readerCount) == 1)
        {
            try
            {
                await _readSemaphore.WaitAsync(token).ConfigureAwait(false);
            }
            catch
            {
                Interlocked.Decrement(ref _readerCount);
                _writeSemaphore.Release();
                throw;
            }
        }

        _writeSemaphore.Release();
        return new LockToken(ReleaseReaderLock);
    }

    private void ReleaseReaderLock()
    {
        if (Interlocked.Decrement(ref _readerCount) == 0)
            _readSemaphore.Release();
    }

    public void Dispose()
    {
        _writeSemaphore.Dispose();
        _readSemaphore.Dispose();
    }

    private sealed class LockToken : IDisposable
    {
        private readonly Action _action;
        public LockToken(Action action) => _action = action;
        public void Dispose() => _action?.Invoke();
    }
}
