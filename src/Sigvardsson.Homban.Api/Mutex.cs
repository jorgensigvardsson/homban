using System;
using System.Threading;
using System.Threading.Tasks;

namespace Sigvardsson.Homban.Api;

public class Mutex : IDisposable
{
    private readonly SemaphoreSlim m_lock = new(1, 1);

    public async Task<T> Locked<T>(Func<Task<T>> a, CancellationToken cancellationToken)
    {
        await m_lock.WaitAsync(cancellationToken);

        try
        {
            return await a();
        }
        finally
        {
            m_lock.Release();
        }
    }
    
    public async Task Locked(Func<Task> a, CancellationToken cancellationToken)
    {
        await m_lock.WaitAsync(cancellationToken);

        try
        {
            await a();
        }
        finally
        {
            m_lock.Release();
        }
    }

    public void Dispose()
    {
        m_lock.Dispose();
    }
}