using System;
using System.Collections.Immutable;
using System.Threading;
using AutoFixture;
using Moq;
using Shouldly;
using Sigvardsson.Homban.Api.Services;
using Sigvardsson.Homban.Api.UnitTests.Infrastructure;
using Xunit;

namespace Sigvardsson.Homban.Api.UnitTests.TestClasses.Services;

public class BoardServiceTests : TestBase<BoardService>
{
    private readonly Mock<IBackingStoreService> m_backingStoreService;
    private readonly Mock<IGuidGenerator> m_guidGenerator;
    private readonly Mock<IClock> m_clock;

    public BoardServiceTests()
    {
        m_backingStoreService = InjectMock<IBackingStoreService>();
        m_guidGenerator = InjectMock<IGuidGenerator>();
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

    private Board CreateFakeBoard()
    {
        return new Board(ImmutableDictionary<Guid, Task>.Empty, ImmutableArray<Guid>.Empty, ImmutableArray<Guid>.Empty, ImmutableArray<Guid>.Empty, ImmutableArray<Guid>.Empty);
    }

    [Fact]
    public async System.Threading.Tasks.Task ReadBoard()
    {
        // Arrange
        var board = CreateFakeBoard();
        
        m_backingStoreService.Setup(m => m.Get(It.IsAny<CancellationToken>()))
                             .ReturnsAsync(board);
        
        // Act
        var sut = CreateSut();
        var result = await sut.ReadBoard(CancellationToken.None);
        var result2 = await sut.ReadBoard(CancellationToken.None);
        
        // Assert
        result.ShouldBeSameAs(board);
        result2.ShouldBeSameAs(board);
        m_backingStoreService.Verify(m => m.Get(It.IsAny<CancellationToken>()), Times.Once);
    }
    
    [Fact]
    public async System.Threading.Tasks.Task CreateTask_TaskIsPutInTheReadyLane()
    {
        // Arrange
        var board = CreateFakeBoard();
        var taskData = CreateSpecimen<TaskData>();
        var now = DateTimeOffset.Now;
        var taskId = CreateSpecimen<Guid>();
        Board? savedBoard = null;
        
        m_backingStoreService.Setup(m => m.Get(It.IsAny<CancellationToken>()))
                             .ReturnsAsync(board);

        m_backingStoreService.Setup(m => m.Set(It.IsAny<Board>(), It.IsAny<CancellationToken>()))
                             .Callback((Board b, CancellationToken _) =>
                              {
                                  savedBoard = b;
                              })
                             .Returns(System.Threading.Tasks.Task.CompletedTask);
        

        m_clock.Setup(m => m.Now)
               .Returns(now);

        m_guidGenerator.Setup(m => m.NewGuid())
                       .Returns(taskId);
        
        // Act
        var sut = CreateSut();
        var boardAndTask = await sut.CreateTask(taskData, CancellationToken.None);
        var boardAfterUpdate = await sut.ReadBoard(CancellationToken.None);
        
        // Assert
        boardAndTask.Task.Id.ShouldBe(taskId);
        boardAndTask.Task.Title.ShouldBe(taskData.Title);
        boardAndTask.Task.Description.ShouldBe(taskData.Description);
        boardAndTask.Task.Schedule.ShouldBe(taskData.Schedule);
        boardAndTask.Task.LastChange.ShouldBe(now);
        boardAndTask.Task.LastMovedOnToBoardTime.ShouldBe(now);
        boardAndTask.Task.LastMovedOffTheBoardTime.ShouldBeNull();
        boardAndTask.Board.ReadyLaneTasks.ShouldContain(taskId);
        boardAndTask.Board.InProgressLaneTasks.ShouldBeEmpty();
        boardAndTask.Board.DoneLaneTasks.ShouldBeEmpty();
        boardAndTask.Board.InactiveLaneTasks.ShouldBeEmpty();
        boardAndTask.Board.ShouldBeSameAs(boardAfterUpdate);
        boardAndTask.Board.ShouldBeSameAs(savedBoard);
        
        m_backingStoreService.Verify(m => m.Get(It.IsAny<CancellationToken>()), Times.Once);
        m_backingStoreService.Verify(m => m.Set(boardAfterUpdate, It.IsAny<CancellationToken>()), Times.Once);
    }
    
    [Fact]
    public async System.Threading.Tasks.Task UpdateTask_UpdatesTaskData()
    {
        // Arrange
        var board = CreateFakeBoard();
        var createTaskData = CreateSpecimen<TaskData>();
        var updateTaskData = CreateSpecimen<TaskData>();
        var now = DateTimeOffset.Now;
        var taskId = CreateSpecimen<Guid>();
        Board? savedBoard = null;
        
        m_backingStoreService.Setup(m => m.Get(It.IsAny<CancellationToken>()))
                             .ReturnsAsync(board);

        m_backingStoreService.Setup(m => m.Set(It.IsAny<Board>(), It.IsAny<CancellationToken>()))
                             .Callback((Board b, CancellationToken _) =>
                              {
                                  savedBoard = b;
                              })
                             .Returns(System.Threading.Tasks.Task.CompletedTask);
        

        m_clock.Setup(m => m.Now)
               .Returns(now);

        m_guidGenerator.Setup(m => m.NewGuid())
                       .Returns(taskId);
        
        // Act
        var sut = CreateSut();
        var boardAndTaskAfterCreate = await sut.CreateTask(createTaskData, CancellationToken.None);
        var boardAndTaskAfterUpdate = await sut.UpdateTask(boardAndTaskAfterCreate.Task.Id, updateTaskData, CancellationToken.None);
        var boardAfterUpdate = await sut.ReadBoard(CancellationToken.None);
        
        // Assert
        boardAndTaskAfterUpdate.Task.Id.ShouldBe(taskId);
        boardAndTaskAfterUpdate.Task.Title.ShouldBe(updateTaskData.Title);
        boardAndTaskAfterUpdate.Task.Description.ShouldBe(updateTaskData.Description);
        boardAndTaskAfterUpdate.Task.Schedule.ShouldBe(updateTaskData.Schedule);
        boardAndTaskAfterUpdate.Task.LastChange.ShouldBe(now);
        boardAndTaskAfterUpdate.Task.LastMovedOnToBoardTime.ShouldBe(now);
        boardAndTaskAfterUpdate.Task.LastMovedOffTheBoardTime.ShouldBeNull();
        boardAndTaskAfterUpdate.Board.ReadyLaneTasks.ShouldContain(taskId);
        boardAndTaskAfterUpdate.Board.InProgressLaneTasks.ShouldBeEmpty();
        boardAndTaskAfterUpdate.Board.DoneLaneTasks.ShouldBeEmpty();
        boardAndTaskAfterUpdate.Board.InactiveLaneTasks.ShouldBeEmpty();
        boardAndTaskAfterUpdate.Board.ShouldBeSameAs(boardAfterUpdate);
        boardAndTaskAfterUpdate.Board.ShouldBeSameAs(savedBoard);
        
        m_backingStoreService.Verify(m => m.Get(It.IsAny<CancellationToken>()), Times.Once);
        m_backingStoreService.Verify(m => m.Set(boardAndTaskAfterCreate.Board, It.IsAny<CancellationToken>()), Times.Once);
        m_backingStoreService.Verify(m => m.Set(boardAndTaskAfterUpdate.Board, It.IsAny<CancellationToken>()), Times.Once);
    }
    
    [Fact]
    public async System.Threading.Tasks.Task DeleteTask_DeletesTaskAndLaneReference()
    {
        // Arrange
        var board = CreateFakeBoard();
        var createTaskData = CreateSpecimen<TaskData>();
        var now = DateTimeOffset.Now;
        var taskId = CreateSpecimen<Guid>();
        
        m_backingStoreService.Setup(m => m.Get(It.IsAny<CancellationToken>()))
                             .ReturnsAsync(board);

        m_backingStoreService.Setup(m => m.Set(It.IsAny<Board>(), It.IsAny<CancellationToken>()))
                             .Returns(System.Threading.Tasks.Task.CompletedTask);
        

        m_clock.Setup(m => m.Now)
               .Returns(now);

        m_guidGenerator.Setup(m => m.NewGuid())
                       .Returns(taskId);
        
        // Act
        var sut = CreateSut();
        var boardAndTaskAfterCreate = await sut.CreateTask(createTaskData, CancellationToken.None);
        var boardAfterDelete = await sut.DeleteTask(boardAndTaskAfterCreate.Task.Id, CancellationToken.None);
        
        // Assert
        boardAfterDelete.Tasks.ShouldBeEmpty();
        boardAfterDelete.ReadyLaneTasks.ShouldBeEmpty();
        boardAfterDelete.InProgressLaneTasks.ShouldBeEmpty();
        boardAfterDelete.DoneLaneTasks.ShouldBeEmpty();
        boardAfterDelete.InactiveLaneTasks.ShouldBeEmpty();
        
        m_backingStoreService.Verify(m => m.Get(It.IsAny<CancellationToken>()), Times.Once);
        m_backingStoreService.Verify(m => m.Set(boardAndTaskAfterCreate.Board, It.IsAny<CancellationToken>()), Times.Once);
        m_backingStoreService.Verify(m => m.Set(boardAfterDelete, It.IsAny<CancellationToken>()), Times.Once);
    }
    
    [Fact]
    public async System.Threading.Tasks.Task MoveTask()
    {
        // Arrange
        var board = CreateFakeBoard();
        var createTaskData1 = CreateSpecimen<TaskData>();
        var createTaskData2 = CreateSpecimen<TaskData>();
        var now = DateTimeOffset.Now;
        Guid currentTaskId = default;
        var taskId1 = CreateSpecimen<Guid>();
        var taskId2 = CreateSpecimen<Guid>();
        
        m_backingStoreService.Setup(m => m.Get(It.IsAny<CancellationToken>()))
                             .ReturnsAsync(board);

        m_clock.Setup(m => m.Now)
               .Returns(now);

        m_guidGenerator.Setup(m => m.NewGuid())
                        // ReSharper disable once AccessToModifiedClosure
                       .Returns(() => currentTaskId);
        
        // Act & Assert
        var sut = CreateSut();
        currentTaskId = taskId1;
        var task1Id = (await sut.CreateTask(createTaskData1, CancellationToken.None)).Task.Id;
        currentTaskId = taskId2;
        var task2Id = (await sut.CreateTask(createTaskData2, CancellationToken.None)).Task.Id;

        var boardAfterCreate = await sut.ReadBoard(CancellationToken.None);
        
        boardAfterCreate.ReadyLaneTasks.ShouldContain(task1Id);
        boardAfterCreate.ReadyLaneTasks.ShouldContain(task2Id);
        boardAfterCreate.InProgressLaneTasks.ShouldBeEmpty();
        boardAfterCreate.DoneLaneTasks.ShouldBeEmpty();
        boardAfterCreate.InactiveLaneTasks.ShouldBeEmpty();

        await sut.MoveTask(task1Id, Lane.InProgress, 0, CancellationToken.None);
        var boardAfterMove1 = await sut.ReadBoard(CancellationToken.None);
        boardAfterMove1.ReadyLaneTasks.ShouldContain(task2Id);
        boardAfterMove1.InProgressLaneTasks.ShouldContain(task1Id);
        boardAfterMove1.DoneLaneTasks.ShouldBeEmpty();
        boardAfterMove1.InactiveLaneTasks.ShouldBeEmpty();
        
        await sut.MoveTask(task2Id, Lane.InProgress, 0, CancellationToken.None);
        var boardAfterMove2 = await sut.ReadBoard(CancellationToken.None);
        boardAfterMove2.ReadyLaneTasks.ShouldBeEmpty();
        boardAfterMove2.InProgressLaneTasks.ShouldContain(task2Id);
        boardAfterMove2.InProgressLaneTasks.ShouldContain(task1Id);
        boardAfterMove2.InProgressLaneTasks.IndexOf(task2Id).ShouldBeLessThan(boardAfterMove2.InProgressLaneTasks.IndexOf(task1Id));
        boardAfterMove2.DoneLaneTasks.ShouldBeEmpty();
        boardAfterMove2.InactiveLaneTasks.ShouldBeEmpty();
        
        await sut.MoveTask(task1Id, Lane.InProgress, 0, CancellationToken.None);
        var boardAfterMove3 = await sut.ReadBoard(CancellationToken.None);
        boardAfterMove3.ReadyLaneTasks.ShouldBeEmpty();
        boardAfterMove3.InProgressLaneTasks.ShouldContain(task2Id);
        boardAfterMove3.InProgressLaneTasks.ShouldContain(task1Id);
        boardAfterMove3.InProgressLaneTasks.IndexOf(task1Id).ShouldBeLessThan(boardAfterMove3.InProgressLaneTasks.IndexOf(task2Id));
        boardAfterMove3.DoneLaneTasks.ShouldBeEmpty();
        boardAfterMove3.InactiveLaneTasks.ShouldBeEmpty();
        
        await sut.MoveTask(task1Id, Lane.Inactive, 0, CancellationToken.None);
        var boardAfterMove4 = await sut.ReadBoard(CancellationToken.None);
        boardAfterMove4.ReadyLaneTasks.ShouldBeEmpty();
        boardAfterMove4.InProgressLaneTasks.ShouldContain(task2Id);
        boardAfterMove4.DoneLaneTasks.ShouldBeEmpty();
        boardAfterMove4.InactiveLaneTasks.ShouldContain(task1Id);
        boardAfterMove4.Tasks[taskId1].LastMovedOffTheBoardTime.ShouldBe(now);
    }
    
    [Fact]
    public async System.Threading.Tasks.Task ObserversAreNotified()
    {
        // Arrange
        var board = CreateFakeBoard();
        var createTaskData = CreateSpecimen<TaskData>();
        var now = DateTimeOffset.Now;
        var taskId = CreateSpecimen<Guid>();
        Board? observedBoard = null;
        
        m_backingStoreService.Setup(m => m.Get(It.IsAny<CancellationToken>()))
                             .ReturnsAsync(board);

        m_clock.Setup(m => m.Now)
               .Returns(now);

        m_guidGenerator.Setup(m => m.NewGuid())
                        // ReSharper disable once AccessToModifiedClosure
                       .Returns(taskId);
        
        // Act & Assert
        var sut = CreateSut();
        var registration = sut.RegisterObserver(ob =>
        {
            observedBoard = ob;
            return System.Threading.Tasks.Task.CompletedTask;
        });
        await sut.CreateTask(createTaskData, CancellationToken.None);
        observedBoard.ShouldNotBeNull();
        observedBoard.Tasks.ShouldContainKey(taskId);
        observedBoard.ReadyLaneTasks.ShouldContain(taskId);
        registration.Dispose();

        observedBoard = null;
        await sut.DeleteTask(taskId, CancellationToken.None);
        observedBoard.ShouldBeNull();
    }
}