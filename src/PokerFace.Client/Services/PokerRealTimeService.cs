using Microsoft.AspNetCore.SignalR.Client;
using PokerFace.Shared;
using System;
using System.Threading.Tasks;

namespace PokerFace.Client.Services;

public class PokerRealTimeService : IAsyncDisposable
{
    private HubConnection? _hubConnection;

    // Store connection parameters for reconnection
    private string? _hubUrl;
    private Guid _tableId;
    private Guid _participantId;

    public event Action<Guid, string>? OnUserJoined;
    public event Action<Guid>? OnUserConnected;
    public event Action<Guid>? OnUserDisconnected;
    public event Action<Guid>? OnUserLeft;
    public event Action<Guid, string>? OnModeratorChanged;
    public event Action<Guid, string>? OnUserUpdated; // ParticipantId, NewDisplayName
    public event Action<Guid>? OnUserVoted; // ParticipantId
    public event Action<SessionDto>? OnSessionStarted;
    public event Action<SessionDto>? OnSessionEnded;
    public event Action<Guid, Guid, string>? OnEmojiThrown; // SenderId, TargetId, Emoji

    public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

    public async Task Connect(string hubUrl, Guid tableId, Guid participantId)
    {
        _hubUrl = hubUrl;
        _tableId = tableId;
        _participantId = participantId;

        // Dispose existing connection if any
        if (_hubConnection != null)
        {
            await _hubConnection.DisposeAsync();
        }

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
        _hubConnection.On<Guid, Guid, string>("EmojiThrown", (sender, target, emoji) => OnEmojiThrown?.Invoke(sender, target, emoji));

        // Re-join the SignalR group after automatic reconnection
        _hubConnection.Reconnected += async (connectionId) =>
        {
            await _hubConnection.InvokeAsync("JoinTableGroup", _tableId, _participantId);
        };

        await _hubConnection.StartAsync();
        await _hubConnection.InvokeAsync("JoinTableGroup", tableId, participantId);
    }

    /// <summary>
    /// Disconnects the hub connection immediately.
    /// Called by CircuitHandler when the Blazor circuit goes down (browser closed/tab closed).
    /// This triggers PokerHub.OnDisconnectedAsync so the 3-second removal timer starts right away.
    /// </summary>
    public async Task DisconnectAsync()
    {
        if (_hubConnection is not null)
        {
            await _hubConnection.DisposeAsync();
            _hubConnection = null;
        }
    }

    /// <summary>
    /// Reconnects to the hub after a circuit reconnection.
    /// Called by CircuitHandler when the Blazor circuit comes back up.
    /// </summary>
    public async Task ReconnectAsync()
    {
        if (_hubUrl != null)
        {
            await Connect(_hubUrl, _tableId, _participantId);
        }
    }

    public async Task ThrowEmoji(Guid targetId, string emoji)
    {
        if (_hubConnection is not null && IsConnected)
        {
            await _hubConnection.InvokeAsync("ThrowEmoji", targetId, emoji);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null)
        {
            await _hubConnection.DisposeAsync();
            _hubConnection = null;
        }
    }
}
