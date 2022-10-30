using System;
using System.Threading;
using ThreadTask = System.Threading.Tasks.Task;

namespace Sigvardsson.Homban.Api.Services;

public interface IThreadControl
{
    ThreadTask Delay(TimeSpan duration, CancellationToken cancellationToken);
}

public class ThreadControl : IThreadControl
{
    public ThreadTask Delay(TimeSpan duration, CancellationToken cancellationToken) => ThreadTask.Delay(duration, cancellationToken);
}