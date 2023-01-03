using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using AutoFixture;
using Moq;
using Shouldly;
using Sigvardsson.Homban.Api.Services;
using Sigvardsson.Homban.Api.UnitTests.Infrastructure;
using Xunit;
using Task = Sigvardsson.Homban.Api.Services.Task;
using ThreadTask = System.Threading.Tasks.Task;

namespace Sigvardsson.Homban.Api.UnitTests.TestClasses.Services;

[SuppressMessage("ReSharper", "AccessToDisposedClosure")]
public class BoardSchedulerTests : TestBase<BoardScheduler>
{
    private readonly Mock<IBoardService> m_boardService;
    private readonly Mock<IInactiveTaskScheduler> m_inactiveTaskScheduler;
    private readonly Mock<IThreadControl> m_threadControl;
    private readonly Mock<IClock> m_clock;

    public BoardSchedulerTests()
    {
        m_boardService = InjectMock<IBoardService>();
        m_inactiveTaskScheduler = InjectMock<IInactiveTaskScheduler>();
        m_threadControl = InjectMock<IThreadControl>();
        m_clock = InjectMock<IClock>();
        m_fixture.Customize<Schedule>(
            c => c.FromFactory(() =>
            {
                return (m_fixture.Create<int>() % 3) switch
                {
                    0 => m_fixture.Create<OneTimeSchedule>(),
                    1 => m_fixture.Create<PeriodicScheduleFollowingActivity>(),
                    _ => m_fixture.Create<PeriodicScheduleFollowingCalendar>()
                };
            })
        );
    }

    [Fact]
    public async ThreadTask ServiceWaitsForTheNextTaskToBeScheduled_NoDoneOrInactiveTasks_WaitsForever()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        var taskId = m_fixture.Create<Guid>();
        var task = m_fixture.Create<Task>();
        
        var board = new Board(
            Tasks: new Dictionary<Guid, Task>{ [taskId] = task}.ToImmutableDictionary(kvp => kvp.Key, kvp => kvp.Value),
            ReadyLaneTasks: new [] { taskId }.ToImmutableArray(),
            InProgressLaneTasks: ImmutableArray<Guid>.Empty,
            DoneLaneTasks: ImmutableArray<Guid>.Empty, 
            InactiveLaneTasks: ImmutableArray<Guid>.Empty 
        );
        
        m_boardService.Setup(m => m.ReadBoard(It.IsAny<CancellationToken>()))
                      .ReturnsAsync(board);

        m_threadControl.Setup(m => m.Delay(Timeout.InfiniteTimeSpan, It.IsAny<CancellationToken>()))
                       .Callback(() => cts.Cancel())
                       .Returns(ThreadTask.CompletedTask);
        
        // Act
        var sut = CreateSut();
        await sut.StartAsync(cts.Token);

        // Assert
        m_threadControl.Verify(m => m.Delay(Timeout.InfiniteTimeSpan, It.IsAny<CancellationToken>()));
    }
    
    [Fact]
    public async ThreadTask ServiceWaitsForTheNextTaskToBeScheduled_DoneTaskIsMovedToInactiveAfterMidnight()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        var now = m_fixture.Create<DateTimeOffset>();
        var then = new DateTimeOffset(year: now.Year, month: now.Month, day: now.Day, hour: 0, minute: 0, second: 0, now.Offset).AddDays(1);
        var lastChange = now - TimeSpan.FromMinutes(1);
        var taskId = m_fixture.Create<Guid>();
        var task = m_fixture.Create<IdentifiedTask>() with {LastChange = lastChange};
        var hasSlept = false; 
        var board = new Board(
            Tasks: new Dictionary<Guid, Task>{ [taskId] = task}.ToImmutableDictionary(kvp => kvp.Key, kvp => kvp.Value),
            ReadyLaneTasks: ImmutableArray<Guid>.Empty,
            InProgressLaneTasks: ImmutableArray<Guid>.Empty,
            DoneLaneTasks: new [] { taskId }.ToImmutableArray(), 
            InactiveLaneTasks: ImmutableArray<Guid>.Empty 
        );
        
        m_boardService.Setup(m => m.ReadBoard(It.IsAny<CancellationToken>()))
                      .ReturnsAsync(board);

        m_clock.Setup(m => m.Now)
               .Returns(() => hasSlept ? then : now);

        m_threadControl.Setup(m => m.Delay(then - now, It.IsAny<CancellationToken>()))
                       .Callback(() => hasSlept = true)
                       .Returns(ThreadTask.CompletedTask);

        m_boardService.Setup(m => m.MoveTask(taskId, Lane.Inactive, 0, It.IsAny<CancellationToken>()))
                      .Callback(() => cts.Cancel())
                      .ReturnsAsync(new BoardAndTask(board, task));
        
        // Act
        var sut = CreateSut();
        await sut.StartAsync(cts.Token);

        // Assert
        m_threadControl.Verify(m => m.Delay(then - now, It.IsAny<CancellationToken>()));
        m_boardService.Verify(m => m.MoveTask(taskId, Lane.Inactive, 0, It.IsAny<CancellationToken>()));
    }
    
    [Fact]
    public async ThreadTask ServiceWaitsForTheNextTaskToBeScheduled_InactiveTaskIsMovedToReadyAfterCalculatedTime()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        var now = m_fixture.Create<DateTimeOffset>();
        var then = now + TimeSpan.FromMinutes(1);
        var callCount = 0;
        var taskId = m_fixture.Create<Guid>();
        var task = m_fixture.Create<IdentifiedTask>();
        var board = new Board(
            Tasks: new Dictionary<Guid, Task>{ [taskId] = task}.ToImmutableDictionary(kvp => kvp.Key, kvp => kvp.Value),
            ReadyLaneTasks: ImmutableArray<Guid>.Empty,
            InProgressLaneTasks: ImmutableArray<Guid>.Empty,
            DoneLaneTasks: ImmutableArray<Guid>.Empty,  
            InactiveLaneTasks: new [] { taskId }.ToImmutableArray() 
        );
        
        m_boardService.Setup(m => m.ReadBoard(It.IsAny<CancellationToken>()))
                      .ReturnsAsync(board);

        m_clock.Setup(m => m.Now)
               .Returns(now);

        m_inactiveTaskScheduler.Setup(m => m.ScheduleReady(task, now))
                               .Returns(() => ++callCount == 1 ? then : now);

        m_threadControl.Setup(m => m.Delay(then - now, It.IsAny<CancellationToken>()))
                       .Returns(ThreadTask.CompletedTask);

        m_boardService.Setup(m => m.MoveTask(taskId, Lane.Ready, 0, It.IsAny<CancellationToken>()))
                      .Callback(() => cts.Cancel())
                      .ReturnsAsync(new BoardAndTask(board, task));
        
        // Act
        var sut = CreateSut();
        await sut.StartAsync(cts.Token);

        // Assert
        m_threadControl.Verify(m => m.Delay(then - now, It.IsAny<CancellationToken>()));
        m_boardService.Verify(m => m.MoveTask(taskId, Lane.Ready, 0, It.IsAny<CancellationToken>()));
    }
    
    [Fact]
    public async ThreadTask ServiceWakesUpWhenBoardHasChanged()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var readCount = 0;
        var now = m_fixture.Create<DateTimeOffset>();
        var then = now + TimeSpan.FromMinutes(1);
        var taskId = m_fixture.Create<Guid>();
        var task = m_fixture.Create<IdentifiedTask>();
        var emptyBoard = new Board(
            Tasks: ImmutableDictionary<Guid, Task>.Empty, 
            ReadyLaneTasks: ImmutableArray<Guid>.Empty,
            InProgressLaneTasks: ImmutableArray<Guid>.Empty,
            DoneLaneTasks: ImmutableArray<Guid>.Empty,
            InactiveLaneTasks:  ImmutableArray<Guid>.Empty 
        );
        var board = new Board(
            Tasks: new Dictionary<Guid, Task>{ [taskId] = task}.ToImmutableDictionary(kvp => kvp.Key, kvp => kvp.Value),
            ReadyLaneTasks: ImmutableArray<Guid>.Empty,
            InProgressLaneTasks: ImmutableArray<Guid>.Empty,
            DoneLaneTasks: ImmutableArray<Guid>.Empty,  
            InactiveLaneTasks: new [] { taskId }.ToImmutableArray() 
        );
        var callCount = 0;
        var infiniteDelayOrder = 0;
        var inactiveTaskDelayOrder = 0;
        var scheduleReadyCallCount = 0;
        var infinitelyLongTask = ThreadTask.Delay(Timeout.InfiniteTimeSpan);
        Func<Board, ThreadTask>? capturedObserver = null;
        var readyStateSet = new TaskCompletionSource();

        m_boardService.Setup(m => m.RegisterObserver(It.IsAny<Func<Board, ThreadTask>>()))
                      .Callback((Func<Board, ThreadTask> observer) =>
                       {
                           capturedObserver = observer;
                       });
        
        m_boardService.Setup(m => m.ReadBoard(It.IsAny<CancellationToken>()))
                      .ReturnsAsync(() => ++readCount == 1 ? emptyBoard : board);

        m_clock.Setup(m => m.Now)
               .Returns(now);

        m_inactiveTaskScheduler.Setup(m => m.ScheduleReady(task, now))
                               .Returns(() => ++scheduleReadyCallCount == 1 ? then : now);
        
        m_threadControl.Setup(m => m.Delay(Timeout.InfiniteTimeSpan, It.IsAny<CancellationToken>()))
                       .Callback(() =>
                        {
                            infiniteDelayOrder = ++callCount;
                        })
                       .Returns(infinitelyLongTask);

        m_threadControl.Setup(m => m.Delay(then - now, It.IsAny<CancellationToken>()))
                       .Callback(() => inactiveTaskDelayOrder = ++callCount)
                       .Returns(ThreadTask.CompletedTask);

        m_boardService.Setup(m => m.MoveTask(taskId, Lane.Ready, 0, It.IsAny<CancellationToken>()))
                      .Callback(() =>
                       {
                           readyStateSet.SetResult();
                           cts.Cancel();
                       })
                      .ReturnsAsync(new BoardAndTask(board, task));
        
        // Act
        var sut = CreateSut();
        await sut.StartAsync(cts.Token);
        capturedObserver.ShouldNotBeNull();
        await capturedObserver.Invoke(board);
        await readyStateSet.Task;
        await sut.StopAsync(CancellationToken.None);
        
        // Assert
        infiniteDelayOrder.ShouldBeLessThan(inactiveTaskDelayOrder);
        m_boardService.Verify(m => m.MoveTask(taskId, Lane.Ready, 0, It.IsAny<CancellationToken>()));
    }
}