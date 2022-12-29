using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Sigvardsson.Homban.Api.Services;

namespace Sigvardsson.Homban.Api.Controllers;

[ApiController]
[Route("/api/board")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class BoardController : ControllerBase
{
    private readonly IBoardService m_service;
    private readonly IDtoMapper m_dtoMapper;

    public BoardController(IBoardService service,
                           IDtoMapper dtoMapper)
    {
        m_service = service ?? throw new ArgumentNullException(nameof(service));
        m_dtoMapper = dtoMapper ?? throw new ArgumentNullException(nameof(dtoMapper));
    }

    [HttpGet]
    [Produces("application/json")]
    [ProducesResponseType(typeof(Board), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBoard(CancellationToken cancellationToken)
    {
        return Ok(m_dtoMapper.FromModel(await m_service.ReadBoard(cancellationToken)));
    }
    
    [HttpPut("task/{taskId:guid}/move")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(BoardAndTask), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> MoveTask([FromRoute] Guid taskId, [FromBody] MoveData moveData, CancellationToken cancellationToken)
    {
        return Ok(m_dtoMapper.FromModel(await m_service.MoveTask(taskId, m_dtoMapper.ToModel(moveData.Lane), moveData.Index, cancellationToken)));
    }
    
    [HttpPost("task")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(BoardAndTask), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateTask([FromBody] TaskData taskData, CancellationToken cancellationToken)
    {
        return Ok(m_dtoMapper.FromModel(await m_service.CreateTask(m_dtoMapper.ToModel(taskData), cancellationToken)));
    }
    
    [HttpPut("task/{taskId:guid}")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(BoardAndTask), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateTask([FromRoute] Guid taskId, [FromBody] TaskData taskData, CancellationToken cancellationToken)
    {
        return Ok(m_dtoMapper.FromModel(await m_service.UpdateTask(taskId, m_dtoMapper.ToModel(taskData), cancellationToken)));
    }
    
    [HttpDelete("task/{taskId:guid}")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(Board), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteTask([FromRoute] Guid taskId, CancellationToken cancellationToken)
    {
        return Ok(m_dtoMapper.FromModel(await m_service.DeleteTask(taskId, cancellationToken)));
    }
}
