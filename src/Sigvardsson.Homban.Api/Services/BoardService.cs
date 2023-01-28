using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Sigvardsson.Homban.Api.Controllers;
using Sigvardsson.Homban.Api.Hubs;

namespace Sigvardsson.Homban.Api.Services;

public enum Lane
{
    Inactive,
    Ready,
    InProgress,
    Done
} 

public interface IBoardService
{
    Task<Board> ReadBoard(CancellationToken cancellationToken);
    Task<BoardAndTask> MoveTask(Guid taskId, Lane lane, int index, CancellationToken cancellationToken);
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
    private readonly IBoardHubService m_boardHubService;
    private readonly Mutex m_mutex = new ();
    private Board? m_board;
    private readonly ConcurrentBag<BoardServiceObserverRegistration> m_observerRegistrations = new();

    public BoardService(ILogger<BoardService> logger,
                        IBackingStoreService backingStoreService,
                        IGuidGenerator guidGenerator,
                        IClock clock,
                        IBoardHubService boardHubService)
    {
        m_logger = logger ?? throw new ArgumentNullException(nameof(logger));
        m_backingStoreService = backingStoreService ?? throw new ArgumentNullException(nameof(backingStoreService));
        m_guidGenerator = guidGenerator ?? throw new ArgumentNullException(nameof(guidGenerator));
        m_clock = clock ?? throw new ArgumentNullException(nameof(clock));
        m_boardHubService = boardHubService ?? throw new ArgumentNullException(nameof(boardHubService));
    }

    public void Dispose()
    {
        m_mutex.Dispose();
    }

    // Assumes the mutex is locked!
    private async Task<Board> GetBoard(CancellationToken cancellationToken)
    {
        return m_board ??= await m_backingStoreService.Get(cancellationToken);
    }

    private void SetBoard(Board board) => m_board = board;

    public Task<Board> ReadBoard(CancellationToken cancellationToken)
    {
        return m_mutex.Locked(async () => await GetBoard(CancellationToken.None), cancellationToken);
    }

    private Task<BoardAndTask> EditBoard(Func<Board, BoardAndTask> editor, CancellationToken cancellationToken)
    {
        return m_mutex.Locked(async () =>
        {
            using var logScope = m_logger.BeginScope("EditBoard (Board -> BoardAndTask)");
            
            m_logger.LogInformation("Board read");
            var board = await GetBoard(cancellationToken);
            var boardAndTask = editor(board);
            if (ReferenceEquals(boardAndTask.Board, board))
            {
                m_logger.LogInformation("No change to board, returning same board and task.");
                return boardAndTask;
            }

            m_logger.LogInformation("Change detected, updating backing store with new board.");
            await m_backingStoreService.Set(boardAndTask.Board, cancellationToken);
            SetBoard(boardAndTask.Board);
            m_logger.LogInformation("Firing observers.");
            await m_boardHubService.SendBoardUpdates(boardAndTask.Board, cancellationToken);
            return boardAndTask;
        }, cancellationToken);
    }
    
    private Task<Board> EditBoard(Func<Board, Board> editor, CancellationToken cancellationToken)
    {
        return m_mutex.Locked(async () =>
        {
            using var logScope = m_logger.BeginScope("EditBoard (Board -> Board)");
            
            var board = await GetBoard(cancellationToken);
            m_logger.LogInformation("Board read");
            var newBoard = editor(board);
            if (ReferenceEquals(newBoard, board))
            {
                m_logger.LogInformation("No change to board, returning same board.");
                return board;
            }

            m_logger.LogInformation("Change detected, updating backing store with new board.");
            await m_backingStoreService.Set(newBoard, cancellationToken);
            SetBoard(newBoard);
            
            m_logger.LogInformation("Firing observers.");
            await m_boardHubService.SendBoardUpdates(newBoard, cancellationToken);
            return newBoard;
        }, cancellationToken);
    }

    private IdentifiedTask NewTask(Guid id, TaskData taskData)
    {
        return new IdentifiedTask(
            Id: id,
            Title: taskData.Title,
            Description: taskData.Description,
            Schedule: taskData.Schedule,
            Created: m_clock.Now,
            LastChange: m_clock.Now,
            LastMovedOnToBoardTime: m_clock.Now,
            LastMovedOffTheBoardTime: null
        );
    }
    
    private IdentifiedTask UpdateTask(Guid id, Task oldTask, TaskData taskData)
    {
        return new IdentifiedTask(
            Id: id,
            Title: taskData.Title,
            Description: taskData.Description,
            Schedule: taskData.Schedule,
            Created: oldTask.Created,
            LastChange: m_clock.Now,
            LastMovedOnToBoardTime: oldTask.LastMovedOnToBoardTime,
            LastMovedOffTheBoardTime: oldTask.LastMovedOffTheBoardTime
        );
    }

    public Task<BoardAndTask> CreateTask(TaskData taskData, CancellationToken cancellationToken)
    {
        return EditBoard(board =>
        {
            using var logScope = m_logger.BeginScope("CreateTask(TaskData, CancellationToken)");
            
            var taskId = m_guidGenerator.NewGuid();
            var newTask = NewTask(taskId, taskData);
            
            m_logger.LogInformation("Task {TaskId} was created", taskId.ToString("D"));

            return new BoardAndTask(board with
            {
                Tasks = board.Tasks.Add(newTask.Id, newTask),
                ReadyLaneTasks = board.ReadyLaneTasks.Add(newTask.Id),
                InactiveLaneTasks = board.InactiveLaneTasks
            }, newTask);
        }, cancellationToken);
    }

    public Task<BoardAndTask> UpdateTask(Guid taskId, TaskData taskData, CancellationToken cancellationToken)
    {
        return EditBoard(board =>
        {
            using var logScope = m_logger.BeginScope("UpdateTask(Guid, TaskData, CancellationToken)");

            if (!board.Tasks.TryGetValue(taskId, out var task))
            {
                m_logger.LogInformation("Unknown task ID {TaskId}", taskId.ToString("D"));
                throw new ArgumentException("Unknown task ID", nameof(taskId));
            }

            var newTask = UpdateTask(taskId, task, taskData);
            m_logger.LogInformation("Task {TaskId} was updated", taskId.ToString("D"));
            return new BoardAndTask(board with
            {
                Tasks = board.Tasks.SetItem(taskId, newTask)
            }, newTask);
        }, cancellationToken);
    }

    public Task<Board> DeleteTask(Guid taskId, CancellationToken cancellationToken)
    {
        return EditBoard(board =>
        {
            m_logger.LogInformation("Deleting task {TaskId}", taskId.ToString("D"));
            
            return board with
            {
                Tasks = board.Tasks.Remove(taskId),
                ReadyLaneTasks = board.ReadyLaneTasks.Remove(taskId),
                InProgressLaneTasks = board.InProgressLaneTasks.Remove(taskId),
                DoneLaneTasks = board.DoneLaneTasks.Remove(taskId),
                InactiveLaneTasks = board.InactiveLaneTasks.Remove(taskId)
            };
        }, cancellationToken);
    }

    private (Lane lane, int index) FindLaneAndIndex(Board board, Guid taskId)
    {
        var index = board.ReadyLaneTasks.IndexOf(taskId);
        if (index >= 0)
            return (Lane.Ready, index);

        index = board.InProgressLaneTasks.IndexOf(taskId);
        if (index >= 0)
            return (Lane.InProgress, index);
        
        index = board.DoneLaneTasks.IndexOf(taskId);
        if (index >= 0)
            return (Lane.Done, index);
        
        index = board.InactiveLaneTasks.IndexOf(taskId);
        if (index >= 0)
            return (Lane.Inactive, index);

        throw new ArgumentException($"Unknown task ID {taskId}", nameof(taskId));
    }

    private ImmutableArray<Guid> MoveTaskInLane(ImmutableArray<Guid> laneTasks, Guid taskId, int? prevIndex, int? index)
    {
        if (index == null && prevIndex == null)
            return laneTasks;
        
        if (prevIndex == null && index != null)
            return laneTasks.Insert(index.Value, taskId);
        
        if (prevIndex != null && index == null)
            return laneTasks.RemoveAt(prevIndex.Value);
        
        if (prevIndex!.Value != index!.Value)
            return laneTasks.RemoveAt(prevIndex.Value).Insert(index.Value, taskId);

        return laneTasks;
    }

    public Task<BoardAndTask> MoveTask(Guid taskId, Lane lane, int index, CancellationToken cancellationToken)
    {
        return EditBoard(board =>
        {
            using var logScope = m_logger.BeginScope("Moving Task");

            if (!board.Tasks.TryGetValue(taskId, out var task))
            {
                m_logger.LogInformation("Unknown task ID {TaskId}", taskId.ToString("D"));
                throw new ArgumentException("Unknown task ID", nameof(taskId));
            }

            var identifiedTask = new IdentifiedTask(
                Id: taskId,
                Title: task.Title,
                Description: task.Description,
                Schedule: task.Schedule,
                Created: task.Created,
                LastChange: task.LastChange,
                LastMovedOnToBoardTime: task.LastMovedOnToBoardTime,
                LastMovedOffTheBoardTime: task.LastMovedOffTheBoardTime
            );

            var (prevLane, prevIndex) = FindLaneAndIndex(board, taskId);
            if (prevLane == lane && prevIndex == index)
            {
                m_logger.LogInformation("No op: prevLane = {PrevLane}, lane = {Lane}, prevIndex = {PrevIndex}, index = {Index}", 
                                        prevLane.ToString("G"), lane.ToString("G"), prevIndex, index);
                return new BoardAndTask(board, identifiedTask);
            }

            var newTask = identifiedTask with
            {
                LastChange = m_clock.Now,
                LastMovedOnToBoardTime = prevLane != Lane.Ready && lane == Lane.Ready ? m_clock.Now : task.LastMovedOnToBoardTime,
                LastMovedOffTheBoardTime = prevLane != Lane.Inactive && lane == Lane.Inactive ? m_clock.Now : task.LastMovedOnToBoardTime,
            };

            var readyLaneTasks = MoveTaskInLane(board.ReadyLaneTasks, taskId, prevLane == Lane.Ready ? prevIndex : null, lane == Lane.Ready ? index : null);
            var inProgressLaneTasks = MoveTaskInLane(board.InProgressLaneTasks, taskId, prevLane == Lane.InProgress ? prevIndex : null, lane == Lane.InProgress ? index : null);
            var doneLaneTasks = MoveTaskInLane(board.DoneLaneTasks, taskId, prevLane == Lane.Done ? prevIndex : null, lane == Lane.Done ? index : null);
            var inactiveLaneTasks = MoveTaskInLane(board.InactiveLaneTasks, taskId, prevLane == Lane.Inactive ? prevIndex : null, lane == Lane.Inactive ? index : null);
            
            m_logger.LogInformation("Moving task from {PrevLane}:{PrevIndex} to {Lane}:{Index}", 
                                    prevLane.ToString("G"), prevIndex, lane.ToString("G"), index);

            return new BoardAndTask(board with
            {
                Tasks = board.Tasks.SetItem(newTask.Id, newTask),
                ReadyLaneTasks = readyLaneTasks,
                InProgressLaneTasks = inProgressLaneTasks,
                DoneLaneTasks = doneLaneTasks,
                InactiveLaneTasks = inactiveLaneTasks
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
