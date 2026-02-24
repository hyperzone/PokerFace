using PokerFace.Api.Models;
using PokerFace.Shared;
using System;
using System.Collections.Generic;

namespace PokerFace.Api.Services;

public interface ITableService
{
    CreateTableResponse CreateTable(CreateTableRequest request);
    Table? GetTable(Guid tableId);
    JoinTableResponse JoinTable(Guid tableId, JoinTableRequest request);
    bool ValidateParticipant(Guid tableId, string token, out Participant participant);
    bool ValidateModerator(Guid tableId, string token);
    
    SessionDto StartSession(Guid tableId, string? topic);
    void Vote(Guid tableId, Guid participantId, string value);
    SessionDto EndSession(Guid tableId);
    
    // Updates
    void UpdateParticipant(Guid tableId, Guid participantId, string displayName);
    void UpdateParticipantConnection(Guid tableId, Guid participantId, string connectionId, bool isConnected);
    ParticipantDto? GetParticipant(Guid tableId, Guid participantId);
    
    // Returns true if moderator changed, out newModeratorId
    bool RemoveParticipant(Guid tableId, Guid participantId, out Guid? newModeratorId);
    
    TableDto MapToDto(Table table, bool revealVotes);
    
    void CleanupEmptyTables();
}
