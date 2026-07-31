using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Driver;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Npc.Models;
using UKSF.Api.Core;
using UKSF.Api.Core.Services;
using static UKSF.Api.ArmaServer.Converters.PersistenceConversionHelpers;

namespace UKSF.Api.ArmaServer.Npc.Services;

public interface INpcBrokerService
{
    Task HandleRegisterAsync(int apiPort, Dictionary<string, object> data);
    Task HandleTurnAsync(int apiPort, Dictionary<string, object> data);
    Task HandleMissionEndedAsync(string sessionId);
}

// Turn-serving helpers (scripted clip + dynamic stream) live in NpcBrokerService.Turns.cs.
public partial class NpcBrokerService(
    INpcSessionsContext sessionsContext,
    INpcAudioClipsContext clipsContext,
    INpcBrainClient brainClient,
    IClacksClient clacksClient,
    IGameServerCommandSender commandSender,
    INpcAudioStore audioStore,
    INpcVoiceStore voiceStore,
    INpcVoicesContext voicesContext,
    IVariablesService variablesService,
    IUksfLogger logger
) : INpcBrokerService
{
    private const string DeflectionId = "__deflection__";
    private const int HistoryLimit = 40;

    public async Task HandleRegisterAsync(int apiPort, Dictionary<string, object> data)
    {
        if (!variablesService.GetFeatureState("NPC_BROKER")) return;

        var npcId = ToSafeString(data.GetValueOrDefault("npcId"));
        if (string.IsNullOrEmpty(npcId))
        {
            logger.LogWarning("NPC register event received with empty npcId — ignoring.");
            return;
        }

        // Warm the NPC chat + voice engines while we build the session below, so the prerender
        // (and the first turn) hit a loaded model instead of a cold load. Fire-and-forget — a
        // warm hint must never delay or fail registration (WarmAsync swallows its own errors).
        _ = clacksClient.WarmAsync(NpcWarmKeeper.WarmRoles, NpcWarmKeeper.LeaseMs);

        var sessionId = ToSafeString(data.GetValueOrDefault("sessionId"));
        var knowledge = ToSafeString(data.GetValueOrDefault("knowledge"));
        var voiceId = ToSafeString(data.GetValueOrDefault("voiceId"));
        var mode = ToSafeString(data.GetValueOrDefault("mode"));
        if (string.IsNullOrEmpty(mode)) mode = "dynamic";

        var personaDict = ToDict(data.GetValueOrDefault("persona"));
        var persona = new NpcPersona
        {
            Name = ToSafeString(personaDict.GetValueOrDefault("name")),
            Role = ToSafeString(personaDict.GetValueOrDefault("role")),
            Language = ToSafeString(personaDict.GetValueOrDefault("language")),
            Mood = ToSafeString(personaDict.GetValueOrDefault("mood")),
            AttitudeToPlayers = ToSafeString(personaDict.GetValueOrDefault("attitudeToPlayers"))
        };

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

        var scripted = new NpcScripted { Lines = scriptedLines, Deflection = ToSafeString(scriptedDict.GetValueOrDefault("deflection")) };

        var session = new DomainNpcSession
        {
            NpcId = npcId,
            SessionId = sessionId,
            Persona = persona,
            Knowledge = knowledge,
            Mode = mode,
            Scripted = scripted,
            VoiceId = voiceId,
            History = [],
            CreatedAt = DateTime.UtcNow
        };

        var existing = sessionsContext.GetSingle(x => x.SessionId == session.SessionId && x.NpcId == session.NpcId);
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

    public async Task HandleTurnAsync(int apiPort, Dictionary<string, object> data)
    {
        if (!variablesService.GetFeatureState("NPC_BROKER")) return;

        var npcId = ToSafeString(data.GetValueOrDefault("npcId"));
        var sessionId = ToSafeString(data.GetValueOrDefault("sessionId"));
        var turnId = ToSafeString(data.GetValueOrDefault("turnId"));
        var rawTurns = ToList(data.GetValueOrDefault("newTurns"));

        if (string.IsNullOrEmpty(npcId) || string.IsNullOrEmpty(turnId) || rawTurns.Count == 0)
        {
            logger.LogWarning($"npc_turn received with missing npcId, turnId, or newTurns — npcId='{npcId}', turnId='{turnId}', turns={rawTurns.Count}");
            return;
        }

        var session = sessionsContext.GetSingle(x => x.NpcId == npcId && x.SessionId == sessionId);
        if (session is null)
        {
            logger.LogWarning($"npc_turn for unregistered npcId '{npcId}' (sessionId '{sessionId}') — register must precede turns");
            return;
        }

        var parsedTurns = new List<NpcTurnDto>();
        foreach (var rawTurn in rawTurns)
        {
            var turnDict = ToDict(rawTurn);
            var speakerId = ToSafeString(turnDict.GetValueOrDefault("speakerId"));
            var speakerName = ToSafeString(turnDict.GetValueOrDefault("speakerName"));
            var text = NpcTextSanitiser.Sanitise(ToSafeString(turnDict.GetValueOrDefault("text")));
            if (string.IsNullOrEmpty(text)) continue;

            var t = (long)ToDouble(turnDict.GetValueOrDefault("t") ?? 0L);
            parsedTurns.Add(
                new NpcTurnDto
                {
                    SpeakerId = speakerId,
                    SpeakerName = speakerName,
                    Text = text,
                    T = t
                }
            );
        }

        if (parsedTurns.Count == 0) return;

        // Deconflict before any brain call. The LATEST utterance carries the address
        // intent: a room batches everything said in the debounce window, so classifying
        // the joined text lets an older line to someone else drown a fresh line to this
        // NPC. A player talking to two NPCs side by side must get one reply, not two.
        var allNames = sessionsContext.Get(x => x.SessionId == sessionId)
                                      .Select(s => s.Persona?.Name ?? string.Empty)
                                      .Where(n => !string.IsNullOrEmpty(n))
                                      .ToList();
        var addressed = NpcNameMatcher.Classify(parsedTurns[^1].Text, session.Persona?.Name ?? string.Empty, allNames);
        if (addressed == NpcNameMatcher.Match.Other)
        {
            logger.LogInfo($"npc_turn: utterance names another NPC, '{npcId}' stays silent");
            await commandSender.SendCommandAsync(apiPort, NpcAudioEnvelopeBuilder.BuildTurnCancel(npcId));
            return;
        }

        var scripted = session.Mode == "scripted";
        var request = new RespondRequest
        {
            NpcId = npcId,
            Persona = session.Persona,
            Knowledge = session.Knowledge,
            Mode = session.Mode,
            Scripted = scripted ? new NpcScriptedDto { Lines = session.Scripted.Lines, Deflection = session.Scripted.Deflection } : null,
            VoiceId = session.VoiceId,
            History = NpcHistoryBudget.Trim(session.History),
            NewTurns = parsedTurns,
            TextOnly = !scripted, // dynamic turns stream; the brain returns text + mood only
            MayNotBeAddressed = addressed == NpcNameMatcher.Match.Borderline
        };

        var result = await brainClient.RespondAsync(request);
        if (result is null)
        {
            logger.LogWarning($"npc_turn: brain returned null for npcId '{npcId}' — NPC stays silent this turn");
            await commandSender.SendCommandAsync(apiPort, NpcAudioEnvelopeBuilder.BuildTurnCancel(npcId));
            return;
        }

        // The brain declined the turn: the address check was borderline and it judged the
        // words meant for someone else. No audio, no history entry — as if never asked.
        if (string.Equals(result.Text?.Trim(), "[none]", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInfo($"npc_turn: brain declined turn for '{npcId}' — not addressed");
            await commandSender.SendCommandAsync(apiPort, NpcAudioEnvelopeBuilder.BuildTurnCancel(npcId));
            return;
        }

        if (scripted)
        {
            await SendScriptedClip(apiPort, session, npcId, turnId, result);
        }
        else
        {
            await StreamDynamicTurn(apiPort, npcId, turnId, result);
        }

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
                Text = result.Text,
                Mood = result.Mood,
                T = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            }
        );

        var update = Builders<DomainNpcSession>.Update.PushEach(x => x.History, newEntries, slice: -HistoryLimit);
        await sessionsContext.Update(x => x.NpcId == npcId && x.SessionId == sessionId, update);

        // Nearby NPCs heard this exchange too. Without it, asking one NPC a question and
        // the other a follow-up leaves the second blind to everything just said next to
        // him. Written as overheard so the prompt can mark it as ambient, not addressed.
        var overheard = parsedTurns.Select(turn => new NpcHistoryEntry
                                       {
                                           Role = "overheard",
                                           Speaker = turn.SpeakerId,
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
                Text = result.Text,
                Mood = result.Mood,
                T = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            }
        );
        var overheardUpdate = Builders<DomainNpcSession>.Update.PushEach(x => x.History, overheard, slice: -HistoryLimit);
        await sessionsContext.Update(x => x.NpcId != npcId && x.SessionId == sessionId, overheardUpdate);
    }

    public async Task HandleMissionEndedAsync(string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId)) return;

        await sessionsContext.DeleteMany(x => x.SessionId == sessionId);
        await clipsContext.DeleteMany(x => x.SessionId == sessionId);
    }
}
