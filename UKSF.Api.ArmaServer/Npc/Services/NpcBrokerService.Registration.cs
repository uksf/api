using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UKSF.Api.ArmaServer.Npc.Models;
using static UKSF.Api.ArmaServer.Converters.PersistenceConversionHelpers;

namespace UKSF.Api.ArmaServer.Npc.Services;

// Registration half of the broker: parse payload, validate guarded authoring, upsert session.
public partial class NpcBrokerService
{
    public async Task HandleRegisterAsync(int apiPort, Dictionary<string, object> data)
    {
        if (!variablesService.GetFeatureState("NPC_BROKER")) return;

        var npcId = ToSafeString(data.GetValueOrDefault("npcId"));
        if (string.IsNullOrEmpty(npcId))
        {
            logger.LogWarning("NPC register event received with empty npcId — ignoring.");
            return;
        }

        var sessionId = ToSafeString(data.GetValueOrDefault("sessionId"));
        var knowledge = ToSafeString(data.GetValueOrDefault("knowledge"));
        var voiceId = ToSafeString(data.GetValueOrDefault("voiceId"));
        var mode = ToSafeString(data.GetValueOrDefault("mode"));
        if (string.IsNullOrEmpty(mode)) mode = "dynamic";

        var profileRaw = ToSafeString(data.GetValueOrDefault("interactionProfile"));
        var interactionProfile = string.IsNullOrEmpty(profileRaw) ? NpcInteractionProfiles.Conversation : profileRaw.Trim().ToLowerInvariant();
        if (interactionProfile is not (NpcInteractionProfiles.Conversation or NpcInteractionProfiles.Guarded))
        {
            logger.LogWarning($"NPC register '{npcId}': unknown interactionProfile '{profileRaw}' — ignoring.");
            return;
        }

        var resetGuarded = NpcGuardedRegistration.ToBool(data.GetValueOrDefault("resetGuarded") ?? false);
        var persona = ParsePersona(data);
        var scripted = ParseScripted(data);

        if (interactionProfile == NpcInteractionProfiles.Guarded)
        {
            var (ok, config, error) = NpcGuardedRegistration.ParseAndValidate(data, knowledge, persona, mode);
            if (!ok)
            {
                logger.LogWarning($"NPC register '{npcId}': guarded validation failed — {error}");
                return;
            }

            // Same process-wide lock as turns so reset cannot race an in-flight commit.
            await GuardedTurnLock.WaitAsync();
            try
            {
                await RegisterGuardedAsync(apiPort, npcId, sessionId, knowledge, voiceId, mode, persona, scripted, config, resetGuarded);
            }
            finally
            {
                GuardedTurnLock.Release();
            }

            return;
        }

        // Conversation path unchanged: warm then upsert (no guarded lock).
        _ = clacksClient.WarmAsync(NpcWarmKeeper.WarmRoles, NpcWarmKeeper.LeaseMs);
        await UpsertSessionAsync(apiPort, npcId, sessionId, knowledge, voiceId, mode, NpcInteractionProfiles.Conversation, persona, scripted, null, null);
    }

    private async Task RegisterGuardedAsync(
        int apiPort,
        string npcId,
        string sessionId,
        string knowledge,
        string voiceId,
        string mode,
        NpcPersona persona,
        NpcScripted scripted,
        NpcGuardedConfig guarded,
        bool resetGuarded
    )
    {
        var existing = sessionsContext.GetSingle(x => x.SessionId == sessionId && x.NpcId == npcId);
        if (existing is not null && !resetGuarded)
        {
            if (existing.InteractionProfile == NpcInteractionProfiles.Guarded && NpcGuardedRegistration.ContentEquals(existing.Guarded, guarded))
            {
                existing.Persona = persona;
                existing.Knowledge = knowledge;
                existing.Mode = mode;
                existing.Scripted = scripted;
                existing.VoiceId = voiceId;
                existing.Guarded = guarded;
                existing.InteractionProfile = NpcInteractionProfiles.Guarded;
                existing.GuardedState ??= NpcGuardedRegistration.FreshState();
                // Warm only after duplicate-content rejection path is cleared.
                _ = clacksClient.WarmAsync(NpcWarmKeeper.WarmRoles, NpcWarmKeeper.LeaseMs);
                await sessionsContext.Replace(existing);
                await PrerenderClipsAsync(apiPort, npcId, sessionId, voiceId, mode, scripted);
                return;
            }

            logger.LogWarning($"NPC register '{npcId}': guarded content changed without resetGuarded — rejecting to preserve state.");
            return;
        }

        // First register or explicit reset — warm after rejection gate.
        _ = clacksClient.WarmAsync(NpcWarmKeeper.WarmRoles, NpcWarmKeeper.LeaseMs);
        await UpsertSessionAsync(
            apiPort,
            npcId,
            sessionId,
            knowledge,
            voiceId,
            mode,
            NpcInteractionProfiles.Guarded,
            persona,
            scripted,
            guarded,
            NpcGuardedRegistration.FreshState()
        );
    }

    private async Task UpsertSessionAsync(
        int apiPort,
        string npcId,
        string sessionId,
        string knowledge,
        string voiceId,
        string mode,
        string interactionProfile,
        NpcPersona persona,
        NpcScripted scripted,
        NpcGuardedConfig guarded,
        NpcGuardedState guardedState
    )
    {
        var existing = sessionsContext.GetSingle(x => x.SessionId == sessionId && x.NpcId == npcId);
        var session = new DomainNpcSession
        {
            NpcId = npcId,
            SessionId = sessionId,
            Persona = persona,
            Knowledge = knowledge,
            Mode = mode,
            InteractionProfile = interactionProfile,
            Scripted = scripted,
            Guarded = guarded,
            GuardedState = guardedState,
            VoiceId = voiceId,
            History = [],
            CreatedAt = DateTime.UtcNow
        };

        if (existing is not null)
        {
            session.Id = existing.Id;
            await sessionsContext.Replace(session);
        }
        else
        {
            await sessionsContext.Add(session);
        }

        await PrerenderClipsAsync(apiPort, npcId, sessionId, voiceId, mode, scripted);
    }

    private static NpcPersona ParsePersona(Dictionary<string, object> data)
    {
        var personaDict = ToDict(data.GetValueOrDefault("persona"));
        return new NpcPersona
        {
            Name = ToSafeString(personaDict.GetValueOrDefault("name")),
            Role = ToSafeString(personaDict.GetValueOrDefault("role")),
            Language = ToSafeString(personaDict.GetValueOrDefault("language")),
            Mood = ToSafeString(personaDict.GetValueOrDefault("mood")),
            AttitudeToPlayers = ToSafeString(personaDict.GetValueOrDefault("attitudeToPlayers"))
        };
    }

    private static NpcScripted ParseScripted(Dictionary<string, object> data)
    {
        var scriptedDict = ToDict(data.GetValueOrDefault("scripted"));
        var linesList = ToList(scriptedDict.GetValueOrDefault("lines"));
        var scriptedLines = new List<NpcScriptedLine>();
        foreach (var lineObj in linesList)
        {
            var lineDict = ToDict(lineObj);
            scriptedLines.Add(
                new NpcScriptedLine
                {
                    Id = ToSafeString(lineDict.GetValueOrDefault("id")),
                    Topic = ToSafeString(lineDict.GetValueOrDefault("topic")),
                    Line = ToSafeString(lineDict.GetValueOrDefault("line"))
                }
            );
        }

        return new NpcScripted { Lines = scriptedLines, Deflection = ToSafeString(scriptedDict.GetValueOrDefault("deflection")) };
    }
}
