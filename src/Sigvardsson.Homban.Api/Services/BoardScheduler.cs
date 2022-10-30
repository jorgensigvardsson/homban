using System;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;

using ThreadTask = System.Threading.Tasks.Task;

namespace Sigvardsson.Homban.Api.Services;

public class BoardScheduler : BackgroundService
{
    private readonly IBoardService m_boardService;
    private readonly ILogger<BoardScheduler> m_logger;
    private readonly IInactiveTaskScheduler m_inactiveTaskScheduler;
    private readonly IThreadControl m_threadControl;
    private readonly IClock m_clock;
    private readonly AsyncAutoResetEvent m_boardChangedEvent = new();
    private static readonly TimeSpan s_durationInDoneColumn = TimeSpan.FromHours(2);

    public BoardScheduler(IBoardService boardService,
                          ILogger<BoardScheduler> logger,
                          IInactiveTaskScheduler inactiveTaskScheduler,
                          IThreadControl threadControl,
                          IClock clock)
    {
        m_boardService = boardService ?? throw new ArgumentNullException(nameof(boardService));
        m_logger = logger ?? throw new ArgumentNullException(nameof(logger));
        m_inactiveTaskScheduler = inactiveTaskScheduler ?? throw new ArgumentNullException(nameof(inactiveTaskScheduler));
        m_threadControl = threadControl ?? throw new ArgumentNullException(nameof(threadControl));
        m_clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    protected override async System.Threading.Tasks.Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var observerRegistration = m_boardService.RegisterObserver(OnBoardChange);
        try
        {
            ThreadTask? boardChangedEventTask = null;
            while (!stoppingToken.IsCancellationRequested)
            {
                var board = await m_boardService.ReadBoard(stoppingToken);
                var sleepDuration = CalculateSleep(board, m_clock.Now);
               
                boardChangedEventTask ??= m_boardChangedEvent.WaitAsync(stoppingToken);
                
                var expiredTask = await ThreadTask.WhenAny(
                    m_threadControl.Delay(sleepDuration, stoppingToken),
                    boardChangedEventTask
                );

                if (expiredTask == boardChangedEventTask)
                {
                    // The board has changed, so let's reschedule everything
                    boardChangedEventTask = null;
                }
                else
                {
                    // It was the delay task that expired, so make sure to update the board!
                    await UpdateBoard(board, m_clock.Now, stoppingToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
        catch (Exception ex)
        {
            m_logger.LogCritical(ex, "Board scheduler crashed: {Error}", ex.Message);
        }
    }

    private System.Threading.Tasks.Task OnBoardChange(Board board)
    {
        m_boardChangedEvent.Set();
        return ThreadTask.CompletedTask;
    }

    private async ThreadTask UpdateBoard(Board board, DateTimeOffset now, CancellationToken cancellationToken)
    {
        // Tasks that have been in the state "done" long enough are moved to inactive state
        foreach (var task in board.Tasks.Where(t => t.State is State.Done && t.LastChange + s_durationInDoneColumn <= now))
        {
            await m_boardService.SetTaskState(task.Id, State.Inactive, cancellationToken);
        }
        
        foreach (var task in board.Tasks.Where(t => t.State is State.Inactive))
        {
            var taskNextTime = m_inactiveTaskScheduler.ScheduleReady(task, now);
            if (taskNextTime <= now)
                await m_boardService.SetTaskState(task.Id, State.Ready, cancellationToken);
        }
    }

    private TimeSpan CalculateSleep(Board board, DateTimeOffset now)
    {
        DateTimeOffset? nextTime = null;

        foreach (var task in board.Tasks.Where(t => t.State is State.Done))
        {
            if (nextTime == null || task.LastChange + s_durationInDoneColumn < nextTime)
                nextTime = task.LastChange + s_durationInDoneColumn;
        }

        foreach (var task in board.Tasks.Where(t => t.State is State.Inactive))
        {
            var taskNextTime = m_inactiveTaskScheduler.ScheduleReady(task, now);
            if (nextTime == null || taskNextTime < nextTime)
                nextTime = taskNextTime;
        }

        if (nextTime == null)
            return Timeout.InfiniteTimeSpan;

        if (nextTime.Value > now)
            return nextTime.Value - now;

        return TimeSpan.Zero;
    }
}