using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using AutoFixture;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Sigvardsson.Homban.Api.Services;
using Xunit;
using Task = Sigvardsson.Homban.Api.Services.Task;
using ThreadTask = System.Threading.Tasks.Task;

namespace Sigvardsson.Homban.Api.UnitTests.TestClasses;

[SuppressMessage("ReSharper", "AccessToDisposedClosure")]
public class BoardSchedulerTests
{
    private readonly Fixture m_fixture = new ();
    
    private readonly Mock<IBoardService> m_boardService;
    private readonly Mock<IInactiveTaskScheduler> m_inactiveTaskScheduler;
    private readonly Mock<IThreadControl> m_threadControl;
    private readonly Mock<IClock> m_clock;

    public BoardSchedulerTests()
    {
        m_boardService = m_fixture.Freeze<Mock<IBoardService>>(c => c.OmitAutoProperties());
        m_inactiveTaskScheduler = m_fixture.Freeze<Mock<IInactiveTaskScheduler>>(c => c.OmitAutoProperties());
        m_threadControl = m_fixture.Freeze<Mock<IThreadControl>>(c => c.OmitAutoProperties());
        m_clock = m_fixture.Freeze<Mock<IClock>>(c => c.OmitAutoProperties());
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

    private BoardScheduler CreateSut()
    {
        return new BoardScheduler(m_boardService.Object,
                                  new Mock<ILogger<BoardScheduler>>().Object,
                                  m_inactiveTaskScheduler.Object,
                                  m_threadControl.Object,
                                  m_clock.Object
        );
    }

    [Fact]
    public async ThreadTask ServiceWaitsForTheNextTaskToBeScheduled_NoDoneOrInactiveTasks_WaitsForever()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        
        var board = new Board(
            new []
            {
                m_fixture.Create<Task>() with { State = State.Ready }
            }.ToImmutableArray()
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
    public async ThreadTask ServiceWaitsForTheNextTaskToBeScheduled_DoneTaskIsMovedToInactiveAfterTimeBasedOnLastChange()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        var now = m_fixture.Create<DateTimeOffset>();
        var then = now + TimeSpan.FromHours(2);
        var lastChange = now - TimeSpan.FromMinutes(1);
        var task = m_fixture.Create<Task>() with {State = State.Done, LastChange = lastChange};
        var hasSlept = false; 
        var board = new Board(
            new [] { task }.ToImmutableArray()
        );
        
        m_boardService.Setup(m => m.ReadBoard(It.IsAny<CancellationToken>()))
                      .ReturnsAsync(board);

        m_clock.Setup(m => m.Now)
               .Returns(() => hasSlept ? then : now);

        m_threadControl.Setup(m => m.Delay(TimeSpan.FromHours(2) - TimeSpan.FromMinutes(1), It.IsAny<CancellationToken>()))
                       .Callback(() => hasSlept = true)
                       .Returns(ThreadTask.CompletedTask);

        m_boardService.Setup(m => m.SetTaskState(task.Id, State.Inactive, It.IsAny<CancellationToken>()))
                      .Callback(() => cts.Cancel())
                      .ReturnsAsync(new BoardAndTask(board, task));
        
        // Act
        var sut = CreateSut();
        await sut.StartAsync(cts.Token);

        // Assert
        m_threadControl.Verify(m => m.Delay(TimeSpan.FromHours(2) - TimeSpan.FromMinutes(1), It.IsAny<CancellationToken>()));
        m_boardService.Verify(m => m.SetTaskState(task.Id, State.Inactive, It.IsAny<CancellationToken>()));
    }
    
    [Fact]
    public async ThreadTask ServiceWaitsForTheNextTaskToBeScheduled_InactiveTaskIsMovedToReadyAfterCalculatedTime()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        var now = m_fixture.Create<DateTimeOffset>();
        var then = now + TimeSpan.FromMinutes(1);
        var callCount = 0;
        var task = m_fixture.Create<Task>() with {State = State.Inactive};
        var board = new Board(
            new [] { task }.ToImmutableArray()
        );
        
        m_boardService.Setup(m => m.ReadBoard(It.IsAny<CancellationToken>()))
                      .ReturnsAsync(board);

        m_clock.Setup(m => m.Now)
               .Returns(now);

        m_inactiveTaskScheduler.Setup(m => m.ScheduleReady(task, now))
                               .Returns(() => ++callCount == 1 ? then : now);

        m_threadControl.Setup(m => m.Delay(then - now, It.IsAny<CancellationToken>()))
                       .Returns(ThreadTask.CompletedTask);

        m_boardService.Setup(m => m.SetTaskState(task.Id, State.Ready, It.IsAny<CancellationToken>()))
                      .Callback(() => cts.Cancel())
                      .ReturnsAsync(new BoardAndTask(board, task));
        
        // Act
        var sut = CreateSut();
        await sut.StartAsync(cts.Token);

        // Assert
        m_threadControl.Verify(m => m.Delay(then - now, It.IsAny<CancellationToken>()));
        m_boardService.Verify(m => m.SetTaskState(task.Id, State.Ready, It.IsAny<CancellationToken>()));
    }
    
    [Fact]
    public async ThreadTask ServiceWakesUpWhenBoardHasChanged()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var readCount = 0;
        var now = m_fixture.Create<DateTimeOffset>();
        var then = now + TimeSpan.FromMinutes(1);
        var task = m_fixture.Create<Task>() with {State = State.Inactive};
        var emptyBoard = new Board(ImmutableArray<Task>.Empty);
        var board = new Board(
            new [] { task }.ToImmutableArray()
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

        m_boardService.Setup(m => m.SetTaskState(task.Id, State.Ready, It.IsAny<CancellationToken>()))
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
        m_boardService.Verify(m => m.SetTaskState(task.Id, State.Ready, It.IsAny<CancellationToken>()));
    }
}