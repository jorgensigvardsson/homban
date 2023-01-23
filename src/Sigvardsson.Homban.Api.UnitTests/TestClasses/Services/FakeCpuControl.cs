using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sigvardsson.Homban.Api.Services;
using Task = System.Threading.Tasks.Task;

namespace Sigvardsson.Homban.Api.UnitTests.TestClasses.Services;

public class FakeCpuControl : IThreadControl, IClock
{
    private readonly Queue<TaskCompletionSource> m_delayQueue = new();
    
    public Task Delay(TimeSpan duration, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource();
        tcs.Task.ContinueWith(t => Now += duration, cancellationToken);
        m_delayQueue.Enqueue(tcs);
        return tcs.Task;
    }

    public DateTimeOffset Now { get; set; }

    public bool Tick()
    {
        if (m_delayQueue.TryDequeue(out var tcs))
        {
            tcs.SetResult();
            return true;
        }

        return false;
    }
}