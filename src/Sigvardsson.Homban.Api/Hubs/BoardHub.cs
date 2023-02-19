using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Sigvardsson.Homban.Api.Controllers;
using Sigvardsson.Homban.Api.Services;
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

public class BoardHubMiddleware
{
    private readonly RequestDelegate m_next;

    public BoardHubMiddleware(RequestDelegate next)
    {
        m_next = next;
    }

    public async ThreadingTask Invoke(HttpContext httpContext)
    {
        var request = httpContext.Request;

        // web sockets cannot pass headers so we must take the access token from query param and
        // add it to the header before authentication middleware runs
        if (request.Path.StartsWithSegments("/api/board-hub", StringComparison.OrdinalIgnoreCase) &&
            request.Query.TryGetValue("access_token", out var token))
        {
            request.Headers.Add("Authorization", $"Bearer {token}");
        }

        await m_next(httpContext);
    }
}

public class BoardHubService : BackgroundService
{
    private readonly IHubContext<BoardHub> m_boardHubContext;
    private readonly IDtoMapper m_dtoMapper;
    private readonly ILogger<BoardHubService> m_logger;
    private readonly IBoardService m_boardService;

    public BoardHubService(IHubContext<BoardHub> boardHubContext,
                           IDtoMapper dtoMapper,
                           ILogger<BoardHubService> logger,
                           IBoardService boardService)
    {
        m_boardHubContext = boardHubContext ?? throw new ArgumentNullException(nameof(boardHubContext));
        m_dtoMapper = dtoMapper ?? throw new ArgumentNullException(nameof(dtoMapper));
        m_logger = logger ?? throw new ArgumentNullException(nameof(logger));
        m_boardService = boardService ?? throw new ArgumentNullException(nameof(boardService));
    }

    protected override async ThreadingTask ExecuteAsync(CancellationToken stoppingToken)
    {
        using var observerRegistration = m_boardService.RegisterObserver(board => OnBoardChanged(board, stoppingToken));

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await ThreadingTask.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
        catch (Exception exception)
        {
            m_logger.LogError("BoardHubService crashed: {Error}", exception);
        }
    }

    private async ThreadingTask OnBoardChanged(Board newBoard, CancellationToken cancellationToken)
    {
        try
        {
            await m_boardHubContext.Clients.All.SendAsync("BoardUpdated", m_dtoMapper.FromModel(newBoard), cancellationToken);
        }
        catch (Exception ex)
        {
            m_logger.LogError("Error sending messages to hub: {error}", ex);
        }
    }
}