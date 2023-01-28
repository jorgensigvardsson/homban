using System.Threading;
using Sigvardsson.Homban.Api.Hubs;
using Sigvardsson.Homban.Api.Services;
using Task = System.Threading.Tasks.Task;

namespace Sigvardsson.Homban.Api.UnitTests.TestClasses.Services;

public class FakeBoardHubService : IBoardHubService
{
    public Task SendBoardUpdates(Board board, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}