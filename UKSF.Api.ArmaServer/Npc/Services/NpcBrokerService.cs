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

public class NpcBrokerService(
    INpcSessionsContext sessionsContext,
    INpcAudioClipsContext clipsContext,
    INpcBrainClient brainClient,
    IGameServerCommandSender commandSender,
    INpcAudioStore audioStore,
    IVariablesService variablesService,
    IUksfLogger logger
) : INpcBrokerService
{
    private const string DeflectionId = "__deflection__";
    private const int HistoryLimit = 40;

    private static readonly (string Id, string Text)[] Fillers = [("f0", "hmm"), ("f1", "let me think"), ("f2", "give me a sec"), ("f3", "hold on")];

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

        var items = new List<PrerenderItem>();
        if (mode == "scripted")
        {
            foreach (var line in scripted.Lines)
            {
                items.Add(new PrerenderItem { Id = line.Id, Text = line.Line });
            }

            items.Add(new PrerenderItem { Id = DeflectionId, Text = scripted.Deflection });
        }

        foreach (var (fillerId, fillerText) in Fillers)
        {
            items.Add(new PrerenderItem { Id = fillerId, Text = fillerText });
        }

        var result = await brainClient.PrerenderAsync(new PrerenderRequest { VoiceId = voiceId, Items = items });
        if (result is null)
        {
            logger.LogWarning($"NPC prerender returned null for npcId '{npcId}' — no clips stored.");
            return;
        }

        foreach (var item in result.Items)
        {
            string filePath;
            try
            {
                filePath = await audioStore.SaveAsync(sessionId, npcId, item.Id, Convert.FromBase64String(item.AudioBase64));
            }
            catch (Exception exception)
            {
                logger.LogError($"NPC clip save failed for clipId '{item.Id}' — clip skipped", exception);
                continue;
            }

            var clip = new DomainNpcAudioClip
            {
                NpcId = npcId,
                VoiceId = voiceId,
                ClipId = item.Id,
                FilePath = filePath,
                DurationMs = item.DurationMs,
                SessionId = sessionId
            };

            var existingClip = clipsContext.GetSingle(x => x.SessionId == sessionId && x.NpcId == npcId && x.ClipId == item.Id);
            if (existingClip is not null)
            {
                clip.Id = existingClip.Id;
                await clipsContext.Replace(clip);
            }
            else
            {
                await clipsContext.Add(clip);
            }
        }

        foreach (var (fillerId, _) in Fillers)
        {
            var fillerClip = result.Items.Find(i => i.Id == fillerId);
            if (fillerClip is null)
            {
                logger.LogWarning($"NPC prerender missing filler '{fillerId}' for voiceId '{voiceId}'");
                continue;
            }

            foreach (var cmd in NpcAudioEnvelopeBuilder.BuildFiller(npcId, voiceId, fillerId, fillerClip.AudioBase64, fillerClip.DurationMs))
            {
                await commandSender.SendCommandAsync(apiPort, cmd);
            }
        }
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
            var text = NpcTextSanitiser.Sanitise(ToSafeString(turnDict.GetValueOrDefault("text")));
            if (string.IsNullOrEmpty(text)) continue;

            var t = (long)ToDouble(turnDict.GetValueOrDefault("t") ?? 0L);
            parsedTurns.Add(
                new NpcTurnDto
                {
                    SpeakerId = speakerId,
                    Text = text,
                    T = t
                }
            );
        }

        if (parsedTurns.Count == 0) return;

        var request = new RespondRequest
        {
            NpcId = npcId,
            Persona = session.Persona,
            Knowledge = session.Knowledge,
            Mode = session.Mode,
            Scripted = session.Mode == "scripted" ? new NpcScriptedDto { Lines = session.Scripted.Lines, Deflection = session.Scripted.Deflection } : null,
            VoiceId = session.VoiceId,
            History = session.History,
            NewTurns = parsedTurns
        };

        var result = await brainClient.RespondAsync(request);
        if (result is null)
        {
            logger.LogWarning($"npc_turn: brain returned null for npcId '{npcId}' — NPC stays silent this turn");
            return;
        }

        string audioBase64;
        long durationMs;

        if (session.Mode == "scripted")
        {
            var lineId = string.IsNullOrEmpty(result.LineId) ? DeflectionId : result.LineId;
            var clip = clipsContext.GetSingle(x => x.SessionId == session.SessionId && x.NpcId == npcId && x.ClipId == lineId);
            if (clip is null)
            {
                logger.LogWarning($"npc_turn: scripted clip not found for voiceId='{session.VoiceId}', lineId='{lineId}'");
                return;
            }

            var bytes = await audioStore.ReadAsync(clip.FilePath);
            if (bytes is null)
            {
                logger.LogWarning($"npc_turn: scripted clip file missing '{clip.FilePath}' for lineId '{lineId}'");
                return;
            }

            audioBase64 = Convert.ToBase64String(bytes);
            durationMs = clip.DurationMs;
        }
        else
        {
            if (string.IsNullOrEmpty(result.AudioBase64))
            {
                logger.LogWarning($"npc_turn: dynamic response had no audio for npcId '{npcId}'");
                return;
            }

            audioBase64 = result.AudioBase64;
            durationMs = result.DurationMs ?? 0;
        }

        foreach (var cmd in NpcAudioEnvelopeBuilder.BuildAudio(npcId, turnId, audioBase64, durationMs))
        {
            await commandSender.SendCommandAsync(apiPort, cmd);
        }

        if (session.Mode != "scripted")
        {
            // Archive is best-effort — players already heard the clip.
            try
            {
                await audioStore.SaveAsync(sessionId, npcId, turnId, Convert.FromBase64String(audioBase64));
            }
            catch (Exception exception)
            {
                logger.LogError($"npc_turn: dynamic clip archive failed for turnId '{turnId}'", exception);
            }
        }

        var newEntries = new List<NpcHistoryEntry>();
        foreach (var turn in parsedTurns)
        {
            newEntries.Add(
                new NpcHistoryEntry
                {
                    Role = "player",
                    Speaker = turn.SpeakerId,
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
    }

    public async Task HandleMissionEndedAsync(string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId)) return;

        await sessionsContext.DeleteMany(x => x.SessionId == sessionId);
        await clipsContext.DeleteMany(x => x.SessionId == sessionId);
    }
}
