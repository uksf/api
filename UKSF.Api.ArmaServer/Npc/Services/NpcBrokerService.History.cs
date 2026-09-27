using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Driver;
using UKSF.Api.ArmaServer.Npc.Models;

namespace UKSF.Api.ArmaServer.Npc.Services;

// Conversation history commit for plain NPC turns: the NPC's own history, then overheard copies.
public partial class NpcBrokerService
{
    private async Task CommitConversationHistoryAsync(
        DomainNpcSession session,
        string npcId,
        string sessionId,
        List<NpcTurnDto> parsedTurns,
        string replyText,
        string mood
    )
    {
        var newEntries = new List<NpcHistoryEntry>();
        foreach (var turn in parsedTurns)
        {
            newEntries.Add(
                new NpcHistoryEntry
                {
                    Role = "player",
                    Speaker = string.IsNullOrEmpty(turn.SpeakerName) ? turn.SpeakerId : turn.SpeakerName,
                    Text = turn.Text,
                    T = turn.T
                }
            );
        }

        newEntries.Add(
            new NpcHistoryEntry
            {
                Role = "npc",
                Speaker = string.Empty,
                Text = replyText,
                Mood = mood,
                T = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            }
        );

        var update = Builders<DomainNpcSession>.Update.PushEach(x => x.History, newEntries, slice: -HistoryLimit);
        await sessionsContext.Update(x => x.NpcId == npcId && x.SessionId == sessionId, update);

        var overheard = parsedTurns.Select(turn => new NpcHistoryEntry
                                       {
                                           Role = "overheard",
                                           Speaker = string.IsNullOrEmpty(turn.SpeakerName) ? turn.SpeakerId : turn.SpeakerName,
                                           Text = turn.Text,
                                           T = turn.T
                                       }
                                   )
                                   .ToList();
        overheard.Add(
            new NpcHistoryEntry
            {
                Role = "overheard",
                Speaker = session.Persona?.Name ?? npcId,
                Text = replyText,
                Mood = mood,
                T = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            }
        );
        var overheardUpdate = Builders<DomainNpcSession>.Update.PushEach(x => x.History, overheard, slice: -HistoryLimit);
        await sessionsContext.UpdateMany(x => x.NpcId != npcId && x.SessionId == sessionId, overheardUpdate);
    }
}
