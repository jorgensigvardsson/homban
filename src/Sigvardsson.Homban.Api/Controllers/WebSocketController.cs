using System;
using System.Diagnostics.CodeAnalysis;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Net.WebSockets;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Sigvardsson.Homban.Api.Services;
using ThreadTask = System.Threading.Tasks.Task;

namespace Sigvardsson.Homban.Api.Controllers;

[ApiController]
[Route("/api/web-socket")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class WebSocketController : ControllerBase
{
    private readonly IBoardService m_boardService;
    private readonly ILogger<WebSocketContext> m_logger;
    private readonly IHttpContextAccessor m_httpContextAccessor;
    private readonly IConfigurableJsonSerializer<ApiJsonSettings> m_jsonSerializer;
    private readonly IDtoMapper m_dtoMapper;
    private readonly TokenValidationParameters m_tokenValidationParameters;

    public WebSocketController(IBoardService boardService,
                               ILogger<WebSocketContext> logger,
                               IHttpContextAccessor httpContextAccessor,
                               IConfigurableJsonSerializer<ApiJsonSettings> jsonSerializer,
                               IDtoMapper dtoMapper,
                               TokenValidationParameters tokenValidationParameters)
    {
        m_boardService = boardService ?? throw new ArgumentNullException(nameof(boardService));
        m_logger = logger ?? throw new ArgumentNullException(nameof(logger));
        m_httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        m_jsonSerializer = jsonSerializer ?? throw new ArgumentNullException(nameof(jsonSerializer));
        m_dtoMapper = dtoMapper ?? throw new ArgumentNullException(nameof(dtoMapper));
        m_tokenValidationParameters = tokenValidationParameters ?? throw new ArgumentNullException(nameof(tokenValidationParameters));
    }

    [HttpGet]
    [AllowAnonymous]
    public async ThreadTask GetWebSocket([FromQuery(Name = "token")] string token, CancellationToken cancellationToken)
    {
        if (!HttpContext.WebSockets.IsWebSocketRequest)
        {
            m_httpContextAccessor.HttpContext!.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        if (!ValidateToken(token))
        {
            m_httpContextAccessor.HttpContext!.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
        await Serve(webSocket, cancellationToken);
    }

    private bool ValidateToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;
        
        var validator = new JwtSecurityTokenHandler();

        if (!validator.CanReadToken(token))
            return false;

        validator.ValidateToken(token, m_tokenValidationParameters, out _);
        return true;
    }

    private async ThreadTask Serve(WebSocket webSocket, CancellationToken cancellationToken)
    {
        using var registration = m_boardService.RegisterObserver(board => HandleBoardEvent(webSocket, m_dtoMapper.FromModel(board), cancellationToken));
        var receiveBuffer = new byte[4096];

        var receiveResult = await webSocket.ReceiveAsync(receiveBuffer, cancellationToken);
        while (!receiveResult.CloseStatus.HasValue)
        {
            // We currently don't interpret any data that comes in on the web socket
            receiveResult = await webSocket.ReceiveAsync(receiveBuffer, cancellationToken);
        }
    }

    private async ThreadTask HandleBoardEvent(WebSocket webSocket, Board board, CancellationToken cancellationToken)
    {
        await using var memoryStream = new MemoryStream();

        m_jsonSerializer.Serialize(memoryStream, new WebSocketMessage(MessageType.Board, board));

        if (!memoryStream.TryGetBuffer(out var segment))
        {
            m_logger.LogError("Failed to get buffer segment after serialization!");
            return;
        }

        await webSocket.SendAsync(segment, WebSocketMessageType.Text, true, cancellationToken);
    }
}

[JsonConverter(typeof(StringEnumConverter))]
public enum MessageType
{
    [EnumMember(Value="board")]
    Board
}

[SuppressMessage("ReSharper", "NotAccessedPositionalProperty.Global")]
public record WebSocketMessage(MessageType Type, object? Payload);