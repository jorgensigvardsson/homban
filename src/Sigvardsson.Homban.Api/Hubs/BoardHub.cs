using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using Sigvardsson.Homban.Api.Controllers;
using Board = Sigvardsson.Homban.Api.Services.Board;
using ThreadingTask = System.Threading.Tasks.Task;

namespace Sigvardsson.Homban.Api.Hubs;

[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class BoardHub : Hub
{
    public BoardHub(ILogger<BoardHub> logger)
    {
        logger.LogInformation("BoardHub created.");
    } 
}

public interface IBoardHubService
{
    ThreadingTask SendBoardUpdates(Board board, CancellationToken cancellationToken);
}

public class BoardHubService : IBoardHubService
{
    private readonly IHubContext<BoardHub> m_boardHubContext;
    private readonly IDtoMapper m_dtoMapper;
    private readonly ILogger<BoardHubService> m_logger;

    public BoardHubService(IHubContext<BoardHub> boardHubContext,
                           IDtoMapper dtoMapper,
                           ILogger<BoardHubService> logger)
    {
        m_boardHubContext = boardHubContext ?? throw new ArgumentNullException(nameof(boardHubContext));
        m_dtoMapper = dtoMapper ?? throw new ArgumentNullException(nameof(dtoMapper));
        m_logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    public async ThreadingTask SendBoardUpdates(Board board, CancellationToken cancellationToken)
    {
        try
        {
            await m_boardHubContext.Clients.All.SendAsync("BoardUpdated", m_dtoMapper.FromModel(board), cancellationToken);
        }
        catch (Exception ex)
        {
            m_logger.LogError("Error sending messages to hub: {error}", ex);
        }
    }
}