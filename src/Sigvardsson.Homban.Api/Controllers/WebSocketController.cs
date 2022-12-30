using System;
using System.Diagnostics.CodeAnalysis;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Net.WebSockets;
using System.Runtime.Serialization;
using System.Threading;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
        await HandlePingPong(webSocket, receiveBuffer, receiveResult, cancellationToken);
        while (!receiveResult.CloseStatus.HasValue)
        {
            // We currently don't interpret any data that comes in on the web socket
            receiveResult = await webSocket.ReceiveAsync(receiveBuffer, cancellationToken);
            await HandlePingPong(webSocket, receiveBuffer, receiveResult, cancellationToken);
        }
    }

    private async ThreadTask HandlePingPong(WebSocket webSocket, byte[] buffer, WebSocketReceiveResult receiveResult, CancellationToken cancellationToken)
    {
        try
        {
            if (receiveResult.MessageType != WebSocketMessageType.Text)
            {
                m_logger.LogWarning("Received non text web socket message");
                return;
            }

            if (!receiveResult.EndOfMessage)
            {
                m_logger.LogWarning("Received fragmented text message over web socket");
                return;
            }

            using var memoryStream = new MemoryStream(buffer, 0, receiveResult.Count);
            var message = m_jsonSerializer.Deserialize<WebSocketMessage>(memoryStream);
            if (message == null)
            {
                m_logger.LogWarning("Failed to deserialize web socket text message as a WebSocketMessage");
                return;
            }

            if (message.Type != MessageType.Ping)
            {
                m_logger.LogWarning("Unexpected WebSocketMessage: {Type}", message.Type.ToString("G"));
                return;
            }

            await SendPong(webSocket, cancellationToken);
        }
        catch (Exception ex)
        {
            m_logger.LogError(ex, "Failed to handle ping pong: {ErrorMessage}", ex.Message);
        }
    }

    private async ThreadTask SendPong(WebSocket webSocket, CancellationToken cancellationToken)
    {
        await using var memoryStream = new MemoryStream();

        m_jsonSerializer.Serialize(memoryStream, new WebSocketMessage { Type = MessageType.Pong });

        if (!memoryStream.TryGetBuffer(out var segment))
        {
            m_logger.LogError("Failed to get buffer segment after serialization!");
            return;
        }

        await webSocket.SendAsync(segment, WebSocketMessageType.Text, true, cancellationToken);
    }

    private async ThreadTask HandleBoardEvent(WebSocket webSocket, Board board, CancellationToken cancellationToken)
    {
        await using var memoryStream = new MemoryStream();

        m_jsonSerializer.Serialize(memoryStream, new WebSocketMessage { Type = MessageType.Board, Payload = board });

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
    Board,
    [EnumMember(Value="ping")]
    Ping,
    [EnumMember(Value="pong")]
    Pong
}

[SuppressMessage("ReSharper", "NotAccessedPositionalProperty.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class WebSocketMessage
{
    public required MessageType Type { get; init; }
    public object? Payload { get; init; }
}