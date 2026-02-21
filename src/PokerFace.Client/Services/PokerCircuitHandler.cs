using Microsoft.AspNetCore.Components.Server.Circuits;

namespace PokerFace.Client.Services;

/// <summary>
/// Detects Blazor circuit disconnection (browser closed/tab closed) and immediately
/// disconnects the PokerHub SignalR connection. Without this, the server-side HubConnection
/// stays alive until the circuit retention period expires (~3 minutes), preventing the
/// 3-second participant removal logic from firing promptly.
/// </summary>
public class PokerCircuitHandler : CircuitHandler
{
    private readonly PokerRealTimeService _realTime;

    public PokerCircuitHandler(PokerRealTimeService realTime)
    {
        _realTime = realTime;
    }

    public override async Task OnConnectionDownAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        await _realTime.DisconnectAsync();
    }

    public override async Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        await _realTime.ReconnectAsync();
    }
}
