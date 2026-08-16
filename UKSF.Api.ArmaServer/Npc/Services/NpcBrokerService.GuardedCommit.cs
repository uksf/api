using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Driver;
using UKSF.Api.ArmaServer.Npc.Models;

namespace UKSF.Api.ArmaServer.Npc.Services;

// Guarded commit + delivery helpers (split from Guarded.cs for the 300-line cap).
public partial class NpcBrokerService
{
    private async Task StreamSafeAndSkipCommit(int apiPort, DomainNpcSession session, string npcId, string turnId, string text)
    {
        var result = new RespondResult
        {
            Text = text,
            Mood = MoodScripts.Neutral,
            VoiceId = ResolveGuardedVoice(session.VoiceId, MoodScripts.Neutral)
        };
        await StreamDynamicTurn(apiPort, npcId, turnId, result);
    }

    private async Task<bool> CommitGuardedAsync(
        DomainNpcSession session,
        string npcId,
        string sessionId,
        List<NpcTurnDto> parsedTurns,
        string replyText,
        string mood,
        NpcGuardedState nextState
    )
    {
        var newEntries = parsedTurns.Select(turn => new NpcHistoryEntry
                                        {
                                            Role = "player",
                                            Speaker = string.IsNullOrEmpty(turn.SpeakerName) ? turn.SpeakerId : turn.SpeakerName,
                                            Text = turn.Text,
                                            T = turn.T
                                        }
                                    )
                                    .ToList();
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

        // Combined non-upsert update: state + own history. Never recreates a cleaned-up session.
        var update = Builders<DomainNpcSession>.Update.Set(x => x.GuardedState, nextState).PushEach(x => x.History, newEntries, slice: -HistoryLimit);
        await sessionsContext.Update(x => x.NpcId == npcId && x.SessionId == sessionId, update);

        var still = sessionsContext.GetSingle(x => x.NpcId == npcId && x.SessionId == sessionId);
        if (still is null)
        {
            logger.LogWarning($"npc_turn guarded: commit target missing for '{npcId}' session '{sessionId}' after update (mission cleanup?)");
            return false;
        }

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
        try
        {
            var overheardUpdate = Builders<DomainNpcSession>.Update.PushEach(x => x.History, overheard, slice: -HistoryLimit);
            await sessionsContext.Update(x => x.NpcId != npcId && x.SessionId == sessionId, overheardUpdate);
        }
        catch (Exception ex)
        {
            logger.LogError($"npc_turn guarded: overheard history update failed for '{npcId}' session '{sessionId}'", ex);
        }

        return true;
    }

    private string ResolveGuardedVoice(string baseVoiceId, string mood)
    {
        var variant = $"{baseVoiceId}_{mood}";
        return voicesContext.GetSingle(x => x.VoiceId == variant) is not null ? variant : baseVoiceId;
    }

    private static string SummariseReasons(NpcGuardedEngineResult engine, NpcGuardedConfig config)
    {
        var parts = engine.Classifications?.Where(c => !string.IsNullOrEmpty(c.Reason)).Select(c => RedactCanonical(c.Reason, config)).Take(3) ?? [];
        return string.Join("; ", parts);
    }

    private static string SummariseEvidence(NpcGuardedEngineResult engine, NpcGuardedConfig config)
    {
        var parts = engine.Classifications?.Where(c => !string.IsNullOrEmpty(c.Evidence)).Select(c => RedactCanonical(c.Evidence, config)).Take(3) ?? [];
        return string.Join("; ", parts);
    }

    private static string RedactCanonical(string value, NpcGuardedConfig config)
    {
        if (string.IsNullOrEmpty(value)) return value;
        var concern = config?.Concern;
        if (!string.IsNullOrEmpty(concern) && value.Contains(concern, StringComparison.OrdinalIgnoreCase)) return "[redacted]";
        var containsFact = (config?.Facts ?? []).Any(fact => !string.IsNullOrEmpty(fact.Text) && value.Contains(fact.Text, StringComparison.OrdinalIgnoreCase));
        return containsFact ? "[redacted]" : value;
    }
}
