using System;
using System.Collections.Generic;

namespace PokerFace.Api.Models;

public class Table
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string ModeratorToken { get; set; } = string.Empty; // Secret
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public List<Participant> Participants { get; set; } = new();
    public List<Session> Sessions { get; set; } = new();
    
    public Session? CurrentSession => Sessions.Find(s => s.IsActive);
    public bool IsDeleted { get; set; }
}

public class Participant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public bool IsModerator { get; set; }
    public bool IsObserver { get; set; }
    public string Token { get; set; } = ""; // Secret (Identity)
    public DateTime LastHeartbeat { get; set; } = DateTime.UtcNow;
    public string? ConnectionId { get; set; }
    public DateTime? DisconnectedAt { get; set; }
}

public class Session
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int Number { get; set; }
    public string? Topic { get; set; }
    public bool IsActive { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }
    
    public List<Vote> Votes { get; set; } = new();
}

public class Vote
{
    public Guid ParticipantId { get; set; }
    public string Value { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
