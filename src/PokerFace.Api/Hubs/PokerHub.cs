using Microsoft.AspNetCore.SignalR;
using PokerFace.Api.Services;
using System;
using System.Threading.Tasks;

namespace PokerFace.Api.Hubs;

public class PokerHub : Hub
{
    private readonly ITableService _tableService;

    public PokerHub(ITableService tableService)
    {
        _tableService = tableService;
    }

    // Clients join groups based on their TableId to receive updates
    public async Task JoinTableGroup(Guid tableId, Guid participantId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, tableId.ToString());
        
        // Track connection
        Context.Items["TableId"] = tableId;
        Context.Items["ParticipantId"] = participantId;
        
        _tableService.UpdateParticipantConnection(tableId, participantId, Context.ConnectionId, true);
        
        // Notify others to update "Connected" status dot
        await Clients.Group(tableId.ToString()).SendAsync("UserConnected", participantId);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.Items.TryGetValue("TableId", out var tableIdObj) && tableIdObj is Guid tableId &&
            Context.Items.TryGetValue("ParticipantId", out var participantIdObj) && participantIdObj is Guid participantId)
        {
            // Mark as disconnected immediately
            _tableService.UpdateParticipantConnection(tableId, participantId, Context.ConnectionId, false);
            await Clients.Group(tableId.ToString()).SendAsync("UserDisconnected", participantId);

            // Wait 5 seconds to see if they reconnect
            // Note: This holds the Hub context/thread. In high scale this is bad, 
            // but for this app it's a simple way to implement the requirement.
            await Task.Delay(5000);

            // Check if still disconnected
            var p = _tableService.GetParticipant(tableId, participantId);
            if (p != null && !p.IsConnected)
            {
                // Remove them
                if (_tableService.RemoveParticipant(tableId, participantId, out var newModeratorId))
                {
                    await Clients.Group(tableId.ToString()).SendAsync("UserLeft", participantId);
                    
                    if (newModeratorId.HasValue)
                    {
                        var newMod = _tableService.GetParticipant(tableId, newModeratorId.Value);
                        if (newMod != null)
                        {
                            await Clients.Group(tableId.ToString()).SendAsync("ModeratorChanged", newModeratorId.Value, newMod.DisplayName);
                        }
                    }
                }
            }
        }
        
        await base.OnDisconnectedAsync(exception);
    }
}
