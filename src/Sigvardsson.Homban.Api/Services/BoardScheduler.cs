using System;
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

                m_logger.LogInformation("Sleep duration: {SleepDuration}", sleepDuration.ToString("c"));
               
                boardChangedEventTask ??= m_boardChangedEvent.WaitAsync(stoppingToken);

                var expiredTask = await ThreadTask.WhenAny(
                    m_threadControl.Delay(sleepDuration.TotalMilliseconds > 4294967294 ? TimeSpan.FromMilliseconds(4294967294) : sleepDuration, stoppingToken),
                    boardChangedEventTask
                );

                if (expiredTask == boardChangedEventTask)
                {
                    m_logger.LogInformation("Board changed: rescheduling.");
                    // The board has changed, so let's reschedule everything
                    boardChangedEventTask = null;
                }
                else
                {
                    m_logger.LogInformation("Slept: now updating board.");
                    // It was the delay task that expired, so make sure to update the board!
                    await UpdateBoard(board, m_clock.Now, stoppingToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected
            m_logger.LogInformation("Board scheduler has been requested to terminate.");
        }
        catch (Exception ex)
        {
            m_logger.LogCritical(ex, "Board scheduler crashed: {Error}", ex.Message);
        }
    }

    private System.Threading.Tasks.Task OnBoardChange(Board board)
    {
        m_logger.LogInformation("BoardScheduler.OnBoardChange called - setting event.");
        m_boardChangedEvent.Set();
        return ThreadTask.CompletedTask;
    }

    private async ThreadTask UpdateBoard(Board board, DateTimeOffset now, CancellationToken cancellationToken)
    {
        // Tasks that have been in the state "done" long enough are moved to inactive state
        foreach (var taskId in board.DoneLaneTasks)
        {
            if (NextDayOf(board.Tasks[taskId].LastChange) <= now)
            {
                m_logger.LogInformation(
                    "Done task: it's a new day, so we're moving task {Task} from Done to Inactive. Last Change = {TasksLastChange}, now = {Now}.",
                    board.Tasks[taskId].Title,
                    board.Tasks[taskId].LastChange.ToString("u"),
                    now.ToString("u")
                );
                await m_boardService.MoveTask(taskId, Lane.Inactive, 0, cancellationToken);
            }
        }
        
        foreach (var taskId in board.InactiveLaneTasks)
        {
            var taskNextTime = m_inactiveTaskScheduler.ScheduleReady(board.Tasks[taskId], now);
            if (taskNextTime != null && taskNextTime.Value <= now)
            {
                m_logger.LogInformation(
                    "Inactive task: it's time to move {Task} from Inactive to Ready. Scheduled time = {TaskNextTime}, now = {now}",
                    board.Tasks[taskId].Title,
                    taskNextTime.Value.ToString("u"),
                    now.ToString("u")
                );
                await m_boardService.MoveTask(taskId, Lane.Ready, 0, cancellationToken);
            }
        }
    }

    private DateTimeOffset NextDayOf(DateTimeOffset time)
    {
        return new DateTimeOffset(year: time.Year, month: time.Month, day: time.Day, hour: 0, minute: 0, second: 0, time.Offset).AddDays(1);
    }

    private TimeSpan CalculateSleep(Board board, DateTimeOffset now)
    {
        DateTimeOffset? nextTime = null;
        
        foreach (var taskId in board.DoneLaneTasks)
        {
            var task = board.Tasks[taskId];
            if (nextTime == null || NextDayOf(task.LastChange) < nextTime)
            {
                nextTime = NextDayOf(task.LastChange);
            }
        }
        
        foreach (var taskId in board.InactiveLaneTasks)
        {
            var task = board.Tasks[taskId];
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