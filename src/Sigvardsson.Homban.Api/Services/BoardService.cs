using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Sigvardsson.Homban.Api.Services;

public interface IBoardService
{
    Task<Board> ReadBoard(CancellationToken cancellationToken);
    Task<BoardAndTask> SetTaskState(Guid taskId, State newState, CancellationToken cancellationToken);
    Task<BoardAndTask> CreateTask(TaskData taskData, CancellationToken cancellationToken);
    Task<BoardAndTask> UpdateTask(Guid taskId, TaskData taskData, CancellationToken cancellationToken);
    Task<Board> DeleteTask(Guid taskId, CancellationToken cancellationToken);
    IDisposable RegisterObserver(Func<Board, System.Threading.Tasks.Task> observer);
}

public class BoardService : IBoardService, IDisposable
{
    private readonly ILogger<BoardService> m_logger;
    private readonly IBackingStoreService m_backingStoreService;
    private readonly IGuidGenerator m_guidGenerator;
    private readonly IClock m_clock;
    private readonly Mutex m_mutex = new ();
    private Board? m_board;
    private readonly ConcurrentBag<BoardServiceObserverRegistration> m_observerRegistrations = new();

    public BoardService(ILogger<BoardService> logger,
                        IBackingStoreService backingStoreService,
                        IGuidGenerator guidGenerator,
                        IClock clock)
    {
        m_logger = logger ?? throw new ArgumentNullException(nameof(logger));
        m_backingStoreService = backingStoreService ?? throw new ArgumentNullException(nameof(backingStoreService));
        m_guidGenerator = guidGenerator ?? throw new ArgumentNullException(nameof(guidGenerator));
        m_clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public void Dispose()
    {
        m_mutex.Dispose();
    }

    public Task<Board> ReadBoard(CancellationToken cancellationToken)
    {
        return m_mutex.Locked(async () =>
        {
            m_board ??= await m_backingStoreService.Get(cancellationToken);
            return m_board;
        }, cancellationToken);
    }

    private Task<BoardAndTask> EditBoard(Func<Board, BoardAndTask> editor, CancellationToken cancellationToken)
    {
        return m_mutex.Locked(async () =>
        {
            var boardAndTask = editor(m_board ?? await m_backingStoreService.Get(cancellationToken));
            await m_backingStoreService.Set(m_board = boardAndTask.Board, cancellationToken);
            await FireObservers(m_board);
            return boardAndTask;
        }, cancellationToken);
    }
    
    private Task<Board> EditBoard(Func<Board, Board> editor, CancellationToken cancellationToken)
    {
        return m_mutex.Locked(async () =>
        {
            await m_backingStoreService.Set(m_board = editor(m_board ?? await m_backingStoreService.Get(cancellationToken)), cancellationToken);
            await FireObservers(m_board);
            return m_board;
        }, cancellationToken);
    }

    private Task NewTask(Guid taskId, TaskData taskData)
    {
        return new Task(
            Id: taskId,
            Title: taskData.Title,
            Description: taskData.Description,
            State: taskData.State,
            Schedule: taskData.Schedule,
            Created: m_clock.Now,
            LastChange: m_clock.Now,
            LastMovedOnToBoardTime: m_clock.Now,
            LastMovedOffTheBoardTime: taskData.State == State.Inactive ? m_clock.Now : null
        );
    }
    
    private Task UpdateTask(Task oldTask, TaskData taskData)
    {
        return new Task(
            Id: oldTask.Id,
            Title: taskData.Title,
            Description: taskData.Description,
            State: taskData.State,
            Schedule: taskData.Schedule,
            Created: oldTask.Created,
            LastChange: m_clock.Now,
            LastMovedOnToBoardTime: taskData.State == State.Ready ? m_clock.Now : oldTask.LastMovedOnToBoardTime,
            LastMovedOffTheBoardTime: taskData.State == State.Inactive ? m_clock.Now : oldTask.LastMovedOffTheBoardTime
        );
    }

    public Task<BoardAndTask> CreateTask(TaskData taskData, CancellationToken cancellationToken)
    {
        return EditBoard(board =>
        {
            var newTask = NewTask(m_guidGenerator.NewGuid(), taskData);
            
            return new BoardAndTask(board with
            {
                Tasks = board.Tasks
                             .Append(newTask)
                             .ToImmutableArray()
            }, newTask);
        }, cancellationToken);
    }

    public Task<BoardAndTask> UpdateTask(Guid taskId, TaskData taskData, CancellationToken cancellationToken)
    {
        return EditBoard(board =>
        {
            var task = board.Tasks.SingleOrDefault(t => t.Id == taskId);
            if (task == null)
                throw new ArgumentException("Unknown task ID", nameof(taskId));

            var newTask = UpdateTask(task, taskData);
            return new BoardAndTask(board with
            {
                Tasks = board.Tasks
                             .Remove(task)
                             .Append(newTask)
                             .ToImmutableArray()
            }, newTask);
        }, cancellationToken);
    }

    public Task<Board> DeleteTask(Guid taskId, CancellationToken cancellationToken)
    {
        return EditBoard(board =>
        {
            var task = board.Tasks.SingleOrDefault(t => t.Id == taskId);
            if (task == null)
                return board;

            return board with
            {
                Tasks = board.Tasks
                             .Remove(task)
                             .ToImmutableArray()
            };
        }, cancellationToken);
    }

    public Task<BoardAndTask> SetTaskState(Guid taskId, State newState, CancellationToken cancellationToken)
    {
        return EditBoard(board =>
        {
            var task = board.Tasks.SingleOrDefault(t => t.Id == taskId);
            if (task == null)
                throw new ArgumentException("Unknown task ID", nameof(taskId));

            var newTask = task with
            {
                State = newState,
                LastChange = m_clock.Now,
                LastMovedOnToBoardTime = newState == State.Ready ? m_clock.Now : task.LastMovedOnToBoardTime,
                LastMovedOffTheBoardTime = newState == State.Inactive ? m_clock.Now : task.LastMovedOnToBoardTime,
            };

            return new BoardAndTask(board with
            {
                Tasks = board.Tasks
                             .Remove(task)
                             .Append(newTask)
                             .ToImmutableArray()
            }, newTask);
        }, cancellationToken);
    }

    public IDisposable RegisterObserver(Func<Board, System.Threading.Tasks.Task> observer)
    {
        var observerRegistration = new BoardServiceObserverRegistration(observer, UnregisterObserver); 
        m_observerRegistrations.Add(observerRegistration);
        return observerRegistration;
    }

    private void UnregisterObserver(BoardServiceObserverRegistration observer)
    {
        m_observerRegistrations.TryTake(out _);
    }

    private async System.Threading.Tasks.Task FireObservers(Board board)
    {
        var registrationsSnapshot = m_observerRegistrations.ToArray();
        foreach (var observerRegistration in registrationsSnapshot)
        {
            try
            {
                await observerRegistration.Observer(board);
            }
            catch (Exception ex)
            {
                m_logger.LogError("An observer threw an exception: {error}", ex);
            }
        }
    }
}

public class BoardServiceObserverRegistration : IDisposable
{
    private readonly Action<BoardServiceObserverRegistration> m_unregister;

    public BoardServiceObserverRegistration(Func<Board, System.Threading.Tasks.Task> observer, Action<BoardServiceObserverRegistration> unregister)
    {
        Observer = observer ?? throw new ArgumentNullException(nameof(observer));
        m_unregister = unregister ?? throw new ArgumentNullException(nameof(unregister));
    }

    public void Dispose()
    {
        m_unregister(this);
        GC.SuppressFinalize(this);
    }
    
    public Func<Board, System.Threading.Tasks.Task> Observer { get; }
}
