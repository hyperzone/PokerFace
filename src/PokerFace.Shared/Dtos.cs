using System;
using System.Collections.Generic;

namespace PokerFace.Shared;

public class TableDto
{
    public Guid TableId { get; set; }
    public string TableName { get; set; } = string.Empty;
    public bool IsClosed { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<ParticipantDto> Participants { get; set; } = new();
    public SessionDto? CurrentSession { get; set; }
    public int TotalSessions { get; set; }
}

public class ParticipantDto
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public bool IsModerator { get; set; }
    public bool IsObserver { get; set; }
    public bool IsConnected { get; set; }
    public bool HasVoted { get; set; }
}

public class SessionDto
{
    public Guid Id { get; set; }
    public int SessionNumber { get; set; }
    public string? Topic { get; set; }
    public bool IsActive { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public List<VoteDto> Votes { get; set; } = new();
}

public class VoteDto
{
    public Guid ParticipantId { get; set; }
    public string ParticipantName { get; set; } = string.Empty;
    public string? Value { get; set; } // Null if hidden (session active)
}

public class CreateTableRequest
{
    public string TableName { get; set; } = string.Empty;
    public string ModeratorName { get; set; } = string.Empty;
    public bool IsObserver { get; set; }
}

public class CreateTableResponse
{
    public Guid TableId { get; set; }
    public string TableName { get; set; } = string.Empty;
    public string ModeratorToken { get; set; } = string.Empty;
    public string ParticipantToken { get; set; } = string.Empty;
    public Guid ParticipantId { get; set; }
}

public class JoinTableRequest
{
    public string DisplayName { get; set; } = string.Empty;
    public bool IsObserver { get; set; }
}

public class JoinTableResponse
{
    public Guid ParticipantId { get; set; }
    public string Token { get; set; } = string.Empty;
    public Guid TableId { get; set; }
    public string TableName { get; set; } = string.Empty;
    public bool IsModerator { get; set; }
}

public class StartSessionRequest
{
    public string? Topic { get; set; }
}

public class UpdateParticipantRequest
{
    public string DisplayName { get; set; } = string.Empty;
}

public class VoteRequest
{
    public string Value { get; set; } = string.Empty;
}
