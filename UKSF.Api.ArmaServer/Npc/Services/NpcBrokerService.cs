using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Driver;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Npc.Models;
using UKSF.Api.ArmaServer.Npc.Observability;
using UKSF.Api.Core;
using UKSF.Api.Core.Services;
using static UKSF.Api.ArmaServer.Converters.PersistenceConversionHelpers;

namespace UKSF.Api.ArmaServer.Npc.Services;

public interface INpcBrokerService
{
    Task HandleRegisterAsync(int apiPort, Dictionary<string, object> data);
    Task HandleTurnAsync(int apiPort, Dictionary<string, object> data);
    Task HandleMissionEndedAsync(string sessionId);
    Task HandleTraceEventAsync(string type, Dictionary<string, object> data);
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
    IUksfLogger logger,
    INpcTraceRecorder trace = null,
    INpcTraceMissions traceMissions = null
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

        if (string.IsNullOrEmpty(npcId) || string.IsNullOrEmpty(turnId))
        {
            logger.LogWarning($"npc_turn received with missing npcId or turnId — npcId='{npcId}', turnId='{turnId}'");
            return;
        }

        var turnTrace = new NpcTurnTrace(_trace, sessionId, npcId, turnId, UtteranceIds(rawTurns));
        using var scope = NpcTraceScope.Begin(turnTrace);
        try
        {
            if (rawTurns.Count == 0)
            {
                turnTrace.Outcome = "empty";
                logger.LogWarning($"npc_turn received with no newTurns — npcId='{npcId}', turnId='{turnId}'");
                return;
            }

            await HandleTurnCoreAsync(apiPort, data, npcId, sessionId, turnId, rawTurns, turnTrace);
        }
        finally
        {
            turnTrace.Finish();
        }
    }

    private async Task HandleTurnCoreAsync(
        int apiPort,
        Dictionary<string, object> data,
        string npcId,
        string sessionId,
        string turnId,
        List<object> rawTurns,
        NpcTurnTrace turnTrace
    )
    {
        var session = sessionsContext.GetSingle(x => x.NpcId == npcId && x.SessionId == sessionId);
        if (session is null)
        {
            turnTrace.Outcome = "unregistered";
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

        if (parsedTurns.Count == 0)
        {
            turnTrace.Outcome = "empty";
            await commandSender.SendCommandAsync(apiPort, NpcAudioEnvelopeBuilder.BuildTurnCancel(npcId, turnId));
            return;
        }

        foreach (var (speakerId, oldDisplay, newName) in learned)
        {
            await RewriteSpeakerAsync(sessionId, speakerId, oldDisplay, newName);
            logger.LogInfo($"npc roster: '{oldDisplay}' is now '{newName}'");
        }

        var gazeAddressed = ParseGazeAddressed(data.GetValueOrDefault("gazeAddressed"));
        var decision = await DecideAddressAsync(session, sessionId, parsedTurns[^1].Text, gazeAddressed);
        if (decision == AddressDecision.StaySilent)
        {
            turnTrace.Outcome = "not addressed";
            turnTrace.Decided(AddressDecisionWire(decision), gazeAddressed);
            await CancelTurnAsync(apiPort, npcId, turnId, gazeAddressed ? "names another NPC" : "not addressed");
            return;
        }

        // Guarded sources fail borderline addressing closed — no classifier, no state change.
        var isGuarded = string.Equals(session.InteractionProfile, NpcInteractionProfiles.Guarded, StringComparison.OrdinalIgnoreCase);
        if (isGuarded && decision == AddressDecision.AskTheBrain)
        {
            turnTrace.Outcome = "guarded borderline";
            turnTrace.Decided(AddressDecisionWire(decision), gazeAddressed);
            await CancelTurnAsync(apiPort, npcId, turnId, "guarded borderline address");
            return;
        }

        NormaliseSpeakers(sessionId, session);

        if (isGuarded)
        {
            await HandleGuardedTurnAsync(apiPort, session, npcId, sessionId, turnId, parsedTurns, AddressDecisionWire(decision), gazeAddressed);
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
        turnTrace.Decided(AddressDecisionWire(decision), gazeAddressed, result?.Decision);
        turnTrace.Replied(
            result is null
                ? null
                : new
                {
                    text = result.Text,
                    lineId = result.LineId,
                    mood = result.Mood,
                    emote = result.Emote,
                    provider = result.Provider,
                    voice = result.VoiceId
                }
        );
        if (result is null)
        {
            turnTrace.Outcome = "brain failed";
            logger.LogWarning($"npc_turn: brain returned null for npcId '{npcId}' — NPC stays silent this turn");
            await commandSender.SendCommandAsync(apiPort, NpcAudioEnvelopeBuilder.BuildTurnCancel(npcId, turnId));
            await SendDebugStateAsync(apiPort, npcId, "", AddressDecisionWire(decision));
            return;
        }

        if (string.Equals(result.Text?.Trim(), "[none]", StringComparison.OrdinalIgnoreCase))
        {
            turnTrace.Outcome = "declined";
            logger.LogInfo($"npc_turn: brain declined turn for '{npcId}' — not addressed");
            await commandSender.SendCommandAsync(apiPort, NpcAudioEnvelopeBuilder.BuildTurnCancel(npcId, turnId));
            await SendDebugStateAsync(apiPort, npcId, result.Provider, "none");
            return;
        }

        if (scripted)
        {
            if (!await SendScriptedClip(apiPort, session, npcId, turnId, result))
            {
                turnTrace.Outcome = "clip failed";
                await SendDebugStateAsync(apiPort, npcId, result.Provider, AddressDecisionWire(decision));
                return;
            }
        }
        else if (!await StreamDynamicTurn(apiPort, npcId, turnId, result))
        {
            turnTrace.Outcome = "speech failed";
            await SendDebugStateAsync(apiPort, npcId, result.Provider, AddressDecisionWire(decision));
            return;
        }

        turnTrace.EmoteSent = SendEmote(apiPort, npcId, turnId, result.Emote);
        await CommitConversationHistoryAsync(session, npcId, sessionId, parsedTurns, result.Text, result.Mood);
        turnTrace.Committed = true;
        turnTrace.Outcome = "spoke";
        await SendDebugStateAsync(apiPort, npcId, result.Provider, AddressDecisionWire(decision), spoken: result.Text);
    }

    public async Task HandleMissionEndedAsync(string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId)) return;

        TraceMissionEnded(sessionId, sessionsContext.Get(x => x.SessionId == sessionId).ToList());
        await sessionsContext.DeleteMany(x => x.SessionId == sessionId);
        await clipsContext.DeleteMany(x => x.SessionId == sessionId);
        NpcPlayerRoster.Reset(sessionId);
    }
}
