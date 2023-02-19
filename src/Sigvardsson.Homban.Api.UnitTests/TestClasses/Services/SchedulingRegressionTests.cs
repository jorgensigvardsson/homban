using System;
using System.Text;
using System.Threading;
using Sigvardsson.Homban.Api.Services;
using Sigvardsson.Homban.Api.UnitTests.Infrastructure;
using Xunit;
using Xunit.Abstractions;
using Task = System.Threading.Tasks.Task;

namespace Sigvardsson.Homban.Api.UnitTests.TestClasses.Services;

public class SchedulingRegressionTests : TestBase
{
    private readonly BoardScheduler m_boardScheduler;
    private readonly BoardService m_boardService;
    private readonly InactiveTaskScheduler m_inactiveTaskScheduler;
    private readonly FakeCpuControl m_fakeCpuControl;
    private readonly InMemoryBackingStoreService m_backingStoreService;
    
    
    public SchedulingRegressionTests(ITestOutputHelper outputHelper)
    {
        m_fakeCpuControl = new FakeCpuControl();
        m_inactiveTaskScheduler = new InactiveTaskScheduler();
        m_backingStoreService = new InMemoryBackingStoreService("ensure-move-inactive-off-board-backing-store.json", new ConfigurableJsonSerializer<StorageJsonSettings>(new StorageJsonSettings(), Encoding.UTF8), new TestLogger<BackingStoreService>(outputHelper));
        m_boardService = new BoardService(new TestLogger<BoardService>(outputHelper), m_backingStoreService, new GuidGenerator(), m_fakeCpuControl);
        m_boardScheduler = new BoardScheduler(m_boardService, new TestLogger<BoardScheduler>(outputHelper), m_inactiveTaskScheduler, m_fakeCpuControl, m_fakeCpuControl);
    }

    [Fact]
    public async Task RenameThisTest()
    {
        using var cts = new CancellationTokenSource();
        m_fakeCpuControl.Now = new DateTimeOffset(2023, 1, 22, 00, 59, 00, TimeSpan.Zero);
        var board = await m_boardService.ReadBoard(CancellationToken.None);
        //await m_boardScheduler.StartAsync(CancellationToken.None);
        await m_boardService.MoveTask(Guid.Parse("737d52b8-0e79-4a45-82af-a27c5bb3cdc1"), Lane.Done, 0, CancellationToken.None);
        //m_fakeCpuControl.Tick();
        //await Task.Delay(TimeSpan.FromSeconds(100));
    }
}