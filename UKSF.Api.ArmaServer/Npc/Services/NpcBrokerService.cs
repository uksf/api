using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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

// Turn-serving helpers live in NpcBrokerService.Turns.cs; registration in .Registration.cs;
// guarded orchestration in .Guarded.cs.
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
    private static readonly SemaphoreSlim GuardedTurnLock = new(1, 1);

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
        var learned = new List<(string SpeakerId, string OldDisplay, string NewName)>();
        foreach (var rawTurn in rawTurns)
        {
            var turnDict = ToDict(rawTurn);
            var speakerId = ToSafeString(turnDict.GetValueOrDefault("speakerId"));
            var text = NpcTextSanitiser.Sanitise(ToSafeString(turnDict.GetValueOrDefault("text")));
            if (string.IsNullOrEmpty(text)) continue;

            var learnedName = NpcPlayerRoster.LearnName(sessionId, speakerId, text);
            if (learnedName is not null)
            {
                learned.Add((speakerId, learnedName.Value.OldDisplay, learnedName.Value.NewName));
            }

            var t = (long)ToDouble(turnDict.GetValueOrDefault("t") ?? 0L);
            parsedTurns.Add(
                new NpcTurnDto
                {
                    SpeakerId = speakerId,
                    SpeakerName = NpcPlayerRoster.DisplayName(sessionId, speakerId),
                    Text = text,
                    T = t
                }
            );
        }

        if (parsedTurns.Count == 0) return;

        foreach (var (speakerId, oldDisplay, newName) in learned)
        {
            await RewriteSpeakerAsync(sessionId, speakerId, oldDisplay, newName);
            logger.LogInfo($"npc roster: '{oldDisplay}' is now '{newName}'");
        }

        var gazeAddressed = ParseGazeAddressed(data.GetValueOrDefault("gazeAddressed"));
        var decision = DecideAddress(session, sessionId, parsedTurns[^1].Text, gazeAddressed);
        if (decision == AddressDecision.StaySilent)
        {
            await CancelTurnAsync(apiPort, npcId, gazeAddressed ? "names another NPC" : "not addressed");
            return;
        }

        // Guarded sources fail borderline addressing closed — no classifier, no state change.
        var isGuarded = string.Equals(session.InteractionProfile, NpcInteractionProfiles.Guarded, StringComparison.OrdinalIgnoreCase);
        if (isGuarded && decision == AddressDecision.AskTheBrain)
        {
            await CancelTurnAsync(apiPort, npcId, "guarded borderline address");
            return;
        }

        NormaliseSpeakers(sessionId, session);

        if (isGuarded)
        {
            await HandleGuardedTurnAsync(apiPort, session, npcId, sessionId, turnId, parsedTurns);
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
            TextOnly = !scripted,
            MayNotBeAddressed = decision == AddressDecision.AskTheBrain
        };

        var result = await brainClient.RespondAsync(request);
        if (result is null)
        {
            logger.LogWarning($"npc_turn: brain returned null for npcId '{npcId}' — NPC stays silent this turn");
            await commandSender.SendCommandAsync(apiPort, NpcAudioEnvelopeBuilder.BuildTurnCancel(npcId));
            await SendDebugStateAsync(apiPort, npcId, "", AddressDecisionWire(decision));
            return;
        }

        if (string.Equals(result.Text?.Trim(), "[none]", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInfo($"npc_turn: brain declined turn for '{npcId}' — not addressed");
            await commandSender.SendCommandAsync(apiPort, NpcAudioEnvelopeBuilder.BuildTurnCancel(npcId));
            await SendDebugStateAsync(apiPort, npcId, result.Provider, "none");
            return;
        }

        if (scripted)
        {
            await SendScriptedClip(apiPort, session, npcId, turnId, result);
        }
        else if (!await StreamDynamicTurn(apiPort, npcId, turnId, result))
        {
            await SendDebugStateAsync(apiPort, npcId, result.Provider, AddressDecisionWire(decision));
            return;
        }

        await CommitConversationHistoryAsync(session, npcId, sessionId, parsedTurns, result.Text, result.Mood);
        await SendDebugStateAsync(apiPort, npcId, result.Provider, AddressDecisionWire(decision));
    }

    public async Task HandleMissionEndedAsync(string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId)) return;

        await sessionsContext.DeleteMany(x => x.SessionId == sessionId);
        await clipsContext.DeleteMany(x => x.SessionId == sessionId);
        NpcPlayerRoster.Reset(sessionId);
    }

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
        await sessionsContext.Update(x => x.NpcId != npcId && x.SessionId == sessionId, overheardUpdate);
    }
}
