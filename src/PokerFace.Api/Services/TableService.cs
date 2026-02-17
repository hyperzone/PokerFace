using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using PokerFace.Api.Models;
using PokerFace.Shared;

namespace PokerFace.Api.Services;

public class TableService : ITableService
{
    private readonly ConcurrentDictionary<Guid, Table> _tables = new();

    public CreateTableResponse CreateTable(CreateTableRequest request)
    {
        var table = new Table
        {
            Name = request.TableName,
            ModeratorToken = GenerateToken()
        };

        var moderator = new Participant
        {
            Name = request.ModeratorName,
            IsModerator = true,
            IsObserver = request.IsObserver,
            Token = GenerateToken()
        };
        
        table.Participants.Add(moderator);
        _tables[table.Id] = table;

        return new CreateTableResponse
        {
            TableId = table.Id,
            TableName = table.Name,
            ModeratorToken = table.ModeratorToken,
            ParticipantToken = moderator.Token,
            ParticipantId = moderator.Id
        };
    }

    public Table? GetTable(Guid tableId)
    {
        _tables.TryGetValue(tableId, out var table);
        return table;
    }

    public JoinTableResponse JoinTable(Guid tableId, JoinTableRequest request)
    {
        if (!_tables.TryGetValue(tableId, out var table))
        {
            throw new KeyNotFoundException("Table not found");
        }

        var participant = new Participant
        {
            Name = request.DisplayName,
            IsObserver = request.IsObserver,
            Token = GenerateToken(),
            IsModerator = false
        };

        lock (table)
        {
            table.Participants.Add(participant);
        }

        return new JoinTableResponse
        {
            ParticipantId = participant.Id,
            Token = participant.Token,
            TableId = table.Id,
            TableName = table.Name,
            IsModerator = false
        };
    }

    public bool ValidateParticipant(Guid tableId, string token, out Participant participant)
    {
        participant = null!;
        if (!_tables.TryGetValue(tableId, out var table)) return false;

        var p = table.Participants.FirstOrDefault(x => x.Token == token);
        if (p == null) return false;

        participant = p;
        p.LastHeartbeat = DateTime.UtcNow;
        return true;
    }

    public bool ValidateModerator(Guid tableId, string token)
    {
        if (!_tables.TryGetValue(tableId, out var table)) return false;
        
        // Classic check
        if (table.ModeratorToken == token) return true;
        
        // Participant check
        var p = table.Participants.FirstOrDefault(x => x.Token == token);
        return p != null && p.IsModerator;
    }

    public SessionDto StartSession(Guid tableId, string? topic)
    {
        if (!_tables.TryGetValue(tableId, out var table)) throw new KeyNotFoundException("Table not found");

        lock (table)
        {
            if (table.CurrentSession != null)
            {
               throw new InvalidOperationException("Session already active");
            }

            var session = new Session
            {
                Number = table.Sessions.Count + 1,
                Topic = topic,
                IsActive = true
            };
            table.Sessions.Add(session);
            return MapSessionToDto(session, false);
        }
    }

    public void Vote(Guid tableId, Guid participantId, string value)
    {
        if (!_tables.TryGetValue(tableId, out var table)) return;
        
        lock (table)
        {
            var session = table.CurrentSession;
            if (session == null) throw new InvalidOperationException("No active session");

            var existingVote = session.Votes.FirstOrDefault(v => v.ParticipantId == participantId);
            if (existingVote != null)
            {
                existingVote.Value = value;
                existingVote.Timestamp = DateTime.UtcNow;
            }
            else
            {
                session.Votes.Add(new Vote { ParticipantId = participantId, Value = value });
            }
        }
    }

    public SessionDto EndSession(Guid tableId)
    {
        if (!_tables.TryGetValue(tableId, out var table)) throw new KeyNotFoundException("Table not found");

        lock (table)
        {
            var session = table.CurrentSession;
            if (session == null) throw new InvalidOperationException("No active session");

            session.IsActive = false;
            session.EndedAt = DateTime.UtcNow;
            
            return MapSessionToDto(session, true);
        }
    }

    public void UpdateParticipant(Guid tableId, Guid participantId, string displayName)
    {
        if (!_tables.TryGetValue(tableId, out var table)) throw new KeyNotFoundException("Table not found");
        
        var participant = table.Participants.FirstOrDefault(p => p.Id == participantId);
        if (participant == null) throw new InvalidOperationException("Participant not found");
        
        participant.Name = displayName;
    }

    public void UpdateParticipantConnection(Guid tableId, Guid participantId, string connectionId, bool isConnected)
    {
        if (!_tables.TryGetValue(tableId, out var table)) return;
        lock(table)
        {
            var p = table.Participants.FirstOrDefault(x => x.Id == participantId);
            if (p != null)
            {
                p.ConnectionId = connectionId;
                if (!isConnected)
                {
                    p.DisconnectedAt = DateTime.UtcNow;
                }
                else
                {
                    p.DisconnectedAt = null;
                    p.LastHeartbeat = DateTime.UtcNow;
                }
            }
        }
    }

    public ParticipantDto? GetParticipant(Guid tableId, Guid participantId)
    {
        if (!_tables.TryGetValue(tableId, out var table)) return null;
        lock(table)
        {
            var p = table.Participants.FirstOrDefault(x => x.Id == participantId);
            if (p == null) return null;
             return new ParticipantDto
            {
                Id = p.Id,
                DisplayName = p.Name,
                IsModerator = p.IsModerator,
                IsObserver = p.IsObserver,
                IsConnected = p.DisconnectedAt == null,
                HasVoted = table.CurrentSession?.Votes.Any(v => v.ParticipantId == p.Id) ?? false
            };
        }
    }

    public bool RemoveParticipant(Guid tableId, Guid participantId, out Guid? newModeratorId)
    {
        newModeratorId = null;
        if (!_tables.TryGetValue(tableId, out var table)) return false;
        
        lock(table)
        {
            var p = table.Participants.FirstOrDefault(x => x.Id == participantId);
            if (p == null) return false;

            table.Participants.Remove(p);

            // If moderator left, reassign
            if (p.IsModerator && table.Participants.Any())
            {
                // Assign random moderator
                var random = new Random();
                var newMod = table.Participants[random.Next(table.Participants.Count)];
                newMod.IsModerator = true;
                
                // Update table moderator token? Logic uses table.ModeratorToken to validate.
                // But the new moderator doesn't know the token!
                // We should probably just rely on the specialized ModeratorToken check becoming a check on user IsModerator claim.
                // CURRENT ARCHITECTURE FLAW: ModeratorToken is shared/static on Table? 
                // Wait, CreateTableResponse returns `ModeratorToken`.
                // If we change moderator, we need to give them power.
                // My `ValidateModerator` checks `table.ModeratorToken == token`.
                // This means the old token is valid.
                // But the new user doesn't have it.
                // We must update `table.ModeratorToken` AND somehow give it to the client?
                // Client has `ParticipantToken`.
                // We need to allow `IsModerator` flag on participant to authorize actions?
                // `ValidateModerator(tableId, token)` checks `table.ModeratorToken`.
                // I should change logic: Authorization should check if the participant associated with the token is a moderator.
                // But `ValidateModerator` logic is entrenched.
                
                // QUICK FIX for this architecture:
                // We can't easily push the "ModeratorToken" to the client securely via SignalR without explicit targeted message.
                // Better: Change `ValidateModerator` to also accept a participant token IF that participant is marked as moderator.
                // But `ValidateModerator` signature takes `token`.
                // Let's check `ValidateModerator` usage in Controller.
                // It uses "X-Participant-Token" header.
                // If I change `ValidateModerator` to check "Is this token the global mod token OR does it belong to a moderator participant?"
                
                // Correct approach:
                // 1. Participant is marked IsModerator = true.
                // 2. ValidateModerator checks:
                //    a) Is token == table.ModeratorToken? (Legacy/Global)
                //    b) Is token a participant token AND that participant.IsModerator == true?
                
                // Let's implement this dual check in ValidateModerator down below.
                
                newModeratorId = newMod.Id;
            }
            
            return true;
        }
    }

    public TableDto MapToDto(Table table, bool revealVotes)
    {
        return new TableDto
        {
            TableId = table.Id,
            TableName = table.Name,
            CreatedAt = table.CreatedAt,
            TotalSessions = table.Sessions.Count,
            CurrentSession = table.CurrentSession != null ? MapSessionToDto(table.CurrentSession, false) : (table.Sessions.LastOrDefault() != null ? MapSessionToDto(table.Sessions.Last(), true) : null),
            Participants = table.Participants.Select(p => new ParticipantDto
            {
                Id = p.Id,
                DisplayName = p.Name,
                IsModerator = p.IsModerator,
                IsObserver = p.IsObserver,
                IsConnected = p.DisconnectedAt == null,
                HasVoted = table.CurrentSession?.Votes.Any(v => v.ParticipantId == p.Id) ?? false
            }).ToList()
        };
    }

    private SessionDto MapSessionToDto(Session session, bool reveal)
    {
        return new SessionDto
        {
            Id = session.Id,
            SessionNumber = session.Number,
            Topic = session.Topic,
            IsActive = session.IsActive,
            StartedAt = session.StartedAt,
            EndedAt = session.EndedAt,
            Votes = session.Votes.Select(v => new VoteDto
            {
                ParticipantId = v.ParticipantId,
                ParticipantName = "", // Populate in controller if needed, or lookup
                Value = reveal ? v.Value : null
            }).ToList()
        };
    }

    private string GenerateToken()
    {
        return Convert.ToBase64String(Guid.NewGuid().ToByteArray()).Replace("/", "_").Replace("+", "-").TrimEnd('=');
    }
}
