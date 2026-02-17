using Microsoft.AspNetCore.SignalR.Client;
using PokerFace.Shared;
using System;
using System.Threading.Tasks;

namespace PokerFace.Client.Services;

public class PokerRealTimeService : IAsyncDisposable
{
    private HubConnection? _hubConnection;
    
    public event Action<Guid, string>? OnUserJoined;
    public event Action<Guid>? OnUserConnected;
    public event Action<Guid>? OnUserDisconnected;
    public event Action<Guid>? OnUserLeft;
    public event Action<Guid, string>? OnModeratorChanged;
    public event Action<Guid, string>? OnUserUpdated; // ParticipantId, NewDisplayName
    public event Action<Guid>? OnUserVoted; // ParticipantId
    public event Action<SessionDto>? OnSessionStarted;
    public event Action<SessionDto>? OnSessionEnded;

    public async Task Connect(string hubUrl, Guid tableId, Guid participantId)
    {
        _hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect()
            .Build();

        _hubConnection.On<Guid, string>("UserJoined", (id, name) => OnUserJoined?.Invoke(id, name));
        _hubConnection.On<Guid, string>("UserUpdated", (id, name) => OnUserUpdated?.Invoke(id, name));
        _hubConnection.On<Guid>("UserConnected", (id) => OnUserConnected?.Invoke(id));
        _hubConnection.On<Guid>("UserDisconnected", (id) => OnUserDisconnected?.Invoke(id));
        _hubConnection.On<Guid>("UserLeft", (id) => OnUserLeft?.Invoke(id));
        _hubConnection.On<Guid, string>("ModeratorChanged", (id, name) => OnModeratorChanged?.Invoke(id, name));
        
        _hubConnection.On<Guid>("UserVoted", (id) => OnUserVoted?.Invoke(id));
        _hubConnection.On<SessionDto>("SessionStarted", (session) => OnSessionStarted?.Invoke(session));
        _hubConnection.On<SessionDto>("SessionEnded", (session) => OnSessionEnded?.Invoke(session));

        await _hubConnection.StartAsync();
        await _hubConnection.InvokeAsync("JoinTableGroup", tableId, participantId);
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null)
        {
            await _hubConnection.DisposeAsync();
        }
    }
}
