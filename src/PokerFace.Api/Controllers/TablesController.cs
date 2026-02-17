using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using PokerFace.Api.Hubs;
using PokerFace.Api.Services;
using PokerFace.Shared;
using System;
using System.Threading.Tasks;

namespace PokerFace.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TablesController : ControllerBase
{
    private readonly ITableService _tableService;
    private readonly IHubContext<PokerHub> _hubContext;

    public TablesController(ITableService tableService, IHubContext<PokerHub> hubContext)
    {
        _tableService = tableService;
        _hubContext = hubContext;
    }

    [HttpPost]
    public ActionResult<CreateTableResponse> CreateTable([FromBody] CreateTableRequest request)
    {
        var result = _tableService.CreateTable(request);
        return CreatedAtAction(nameof(GetTable), new { tableId = result.TableId }, result);
    }

    [HttpGet("{tableId}")]
    public ActionResult<TableDto> GetTable(Guid tableId)
    {
        var table = _tableService.GetTable(tableId);
        if (table == null) return NotFound();

        // Check if requester is moderator to reveal votes? 
        // For now, adhere to "reveal" flag in logic (only if session ended). 
        // The service logic handles hiding votes if session is active.
        return Ok(_tableService.MapToDto(table, false)); 
    }

    [HttpPost("{tableId}/participants")]
    public async Task<ActionResult<JoinTableResponse>> JoinTable(Guid tableId, [FromBody] JoinTableRequest request)
    {
        try
        {
            var result = _tableService.JoinTable(tableId, request);
            
            // Notify group
            await _hubContext.Clients.Group(tableId.ToString()).SendAsync("UserJoined", result.ParticipantId, request.DisplayName);
            
            return CreatedAtAction(nameof(GetTable), new { tableId = tableId }, result);
        }
        catch (System.Collections.Generic.KeyNotFoundException)
        {
            return NotFound("Table not found");
        }
    }

    [HttpPut("{tableId}/participants/me")]
    public async Task<IActionResult> UpdateParticipant(Guid tableId, [FromBody] UpdateParticipantRequest request, [FromHeader(Name = "X-Participant-Token")] string token)
    {
        if (!_tableService.ValidateParticipant(tableId, token, out var participant)) return Unauthorized();

        try
        {
            _tableService.UpdateParticipant(tableId, participant.Id, request.DisplayName);
            
            // Notify group about name change
            await _hubContext.Clients.Group(tableId.ToString()).SendAsync("UserUpdated", participant.Id, request.DisplayName);
            
            return Ok();
        }
        catch (System.Collections.Generic.KeyNotFoundException)
        {
            return NotFound("Table not found");
        }
    }

    [HttpPost("{tableId}/sessions")]
    public async Task<ActionResult<SessionDto>> StartSession(Guid tableId, [FromBody] StartSessionRequest request, [FromHeader(Name = "X-Participant-Token")] string token)
    {
        if (!_tableService.ValidateModerator(tableId, token)) return Forbid();

        try
        {
            var session = _tableService.StartSession(tableId, request.Topic);
            
            // Notify group
            await _hubContext.Clients.Group(tableId.ToString()).SendAsync("SessionStarted", session);
            
            return CreatedAtAction(nameof(GetSession), new { tableId = tableId }, session);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
        catch (System.Collections.Generic.KeyNotFoundException)
        {
            return NotFound("Table not found");
        }
    }

    [HttpGet("{tableId}/sessions/current")]
    public ActionResult<SessionDto> GetSession(Guid tableId)
    {
        var table = _tableService.GetTable(tableId);
        if (table == null) return NotFound();
        if (table.CurrentSession == null) return NotFound("No active session");
        
        // Service handles hiding votes
        return Ok(_tableService.MapToDto(table, false).CurrentSession); 
    }

    [HttpPost("{tableId}/sessions/current/votes")]
    public async Task<IActionResult> Vote(Guid tableId, [FromBody] VoteRequest request, [FromHeader(Name = "X-Participant-Token")] string token)
    {
        if (!_tableService.ValidateParticipant(tableId, token, out var participant)) return Unauthorized();
        if (participant.IsObserver) return BadRequest("Observers cannot vote");

        try
        {
            _tableService.Vote(tableId, participant.Id, request.Value);
            
            // Notify group (Identity hidden? No, just "X voted")
            await _hubContext.Clients.Group(tableId.ToString()).SendAsync("UserVoted", participant.Id);
            
            return Ok();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{tableId}/sessions/current/end")]
    public async Task<ActionResult<SessionDto>> EndSession(Guid tableId, [FromHeader(Name = "X-Participant-Token")] string token)
    {
        if (!_tableService.ValidateModerator(tableId, token)) return Forbid();

        try
        {
            var session = _tableService.EndSession(tableId);
            
            // Notify group with results
            await _hubContext.Clients.Group(tableId.ToString()).SendAsync("SessionEnded", session);
            
            return Ok(session);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (System.Collections.Generic.KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
