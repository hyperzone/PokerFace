using PokerFace.Shared;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace PokerFace.Client.Services;

public class PokerService
{
    private readonly HttpClient _http;
    
    // State
    public string? ParticipantToken { get; private set; }
    public string? ModeratorToken { get; private set; }
    public Guid? CurrentTableId { get; private set; }
    public Guid? CurrentParticipantId { get; private set; }
    public bool IsModerator { get; private set; }

    public PokerService(HttpClient http)
    {
        _http = http;
    }

    public void SetTokens(string participantToken, string? moderatorToken, Guid tableId, Guid participantId)
    {
        ParticipantToken = participantToken;
        ModeratorToken = moderatorToken;
        CurrentTableId = tableId;
        CurrentParticipantId = participantId;
        IsModerator = !string.IsNullOrEmpty(moderatorToken);
    }

    public async Task<CreateTableResponse> CreateTable(CreateTableRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/tables", request);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<CreateTableResponse>();
        
        SetTokens(result.ParticipantToken, result.ModeratorToken, result.TableId, result.ParticipantId);
        return result;
    }

    public async Task<JoinTableResponse> JoinTable(Guid tableId, JoinTableRequest request)
    {
        var response = await _http.PostAsJsonAsync($"api/tables/{tableId}/participants", request);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<JoinTableResponse>();
        
        SetTokens(result.Token, null, result.TableId, result.ParticipantId);
        return result;
    }

    public async Task UpdateParticipant(Guid tableId, string newName)
    {
        var request = new UpdateParticipantRequest { DisplayName = newName };
        var msg = new HttpRequestMessage(HttpMethod.Put, $"api/tables/{tableId}/participants/me");
        msg.Content = JsonContent.Create(request);
        msg.Headers.Add("X-Participant-Token", ParticipantToken);
        
        var response = await _http.SendAsync(msg);
        response.EnsureSuccessStatusCode();
    }

    public async Task<TableDto?> GetTable(Guid tableId)
    {
        return await _http.GetFromJsonAsync<TableDto>($"api/tables/{tableId}");
    }

    public async Task CleanupTables()
    {
        try 
        {
            Console.WriteLine("[PokerService] Chiamata all'endpoint cleanup in corso...");
            var msg = new HttpRequestMessage(HttpMethod.Post, "api/tables/cleanup");
            var response = await _http.SendAsync(msg);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[PokerService] Errore chiamata cleanup: {response.StatusCode}");
            }
            else
            {
                Console.WriteLine("[PokerService] Chiamata cleanup riuscita!");
            }
        } 
        catch (Exception ex)
        { 
            Console.WriteLine($"[PokerService] Eccezione durante cleanup: {ex.Message}");
        }
    }

    public async Task StartSession(Guid tableId, string? topic)
    {
        if (ModeratorToken == null) throw new InvalidOperationException("Not moderator");
        
        var request = new StartSessionRequest { Topic = topic };
        var msg = new HttpRequestMessage(HttpMethod.Post, $"api/tables/{tableId}/sessions");
        msg.Content = JsonContent.Create(request);
        msg.Headers.Add("X-Participant-Token", ModeratorToken); // Use moderator token? Docs said ParticipantToken but strictly Moderator needs it? 
        // Docs say: Start Session -> Auth: X-Participant-Token (moderator). 
        // My Api checks ValidateModerator. So I should send Moderator Token? 
        // Wait, the API documentation says "X-Participant-Token: <token>". 
        // If I am moderator, do I send the ModeratorToken OR the ParticipantToken?
        // "ModeratorToken: Permette azioni di gestione tavolo".
        // API implementation `ValidateModerator(tableId, token)` checks `table.ModeratorToken == token`.
        // So I must send ModeratorToken.
        
        var response = await _http.SendAsync(msg);
        response.EnsureSuccessStatusCode();
    } // Wait, docs say "X-Participant-Token" header name, but value should be the moderator token if acting as moderator.

    public async Task Vote(Guid tableId, string value)
    {
        var request = new VoteRequest { Value = value };
        var msg = new HttpRequestMessage(HttpMethod.Post, $"api/tables/{tableId}/sessions/current/votes");
        msg.Content = JsonContent.Create(request);
        msg.Headers.Add("X-Participant-Token", ParticipantToken);
        
        var response = await _http.SendAsync(msg);
        response.EnsureSuccessStatusCode();
    }

    public async Task EndSession(Guid tableId)
    {
        if (ModeratorToken == null) throw new InvalidOperationException("Not moderator");

        var msg = new HttpRequestMessage(HttpMethod.Put, $"api/tables/{tableId}/sessions/current/end");
        msg.Headers.Add("X-Participant-Token", ModeratorToken);
        
        var response = await _http.SendAsync(msg);
        response.EnsureSuccessStatusCode();
    }

    public async Task<AppStatsDto?> GetAppStatsAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<AppStatsDto>("api/stats");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PokerService] Errore recupero statistiche: {ex.Message}");
            return null;
        }
    }
}
