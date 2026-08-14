using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UKSF.Api.ArmaServer.Npc.Models;

namespace UKSF.Api.ArmaServer.Npc.Services;

// Guarded-source turn path: classify → pure engine → typed reply → TTS evidence → commit.
public partial class NpcBrokerService
{
    private async Task HandleGuardedTurnAsync(
        int apiPort,
        DomainNpcSession session,
        string npcId,
        string sessionId,
        string turnId,
        List<NpcTurnDto> parsedTurns
    )
    {
        await GuardedTurnLock.WaitAsync();
        try
        {
            session = sessionsContext.GetSingle(x => x.NpcId == npcId && x.SessionId == sessionId);
            if (session is null)
            {
                logger.LogWarning($"npc_turn guarded: session vanished for '{npcId}' before work");
                await commandSender.SendCommandAsync(apiPort, NpcAudioEnvelopeBuilder.BuildTurnCancel(npcId));
                await SendDebugStateAsync(apiPort, npcId, "", "stay_silent");
                return;
            }

            if (session.Guarded is null || session.Guarded.Facts.Count != 3)
            {
                logger.LogWarning($"npc_turn guarded: missing config for '{npcId}'");
                await StreamSafeAndSkipCommit(apiPort, session, npcId, turnId, NpcGuardedProfile.SafeDeflection);
                await SendDebugStateAsync(apiPort, npcId, "", "answer", disclosedFactIds: session.GuardedState?.DisclosedFactIds);
                return;
            }

            session.GuardedState ??= new NpcGuardedState();
            var stateSnapshot = session.GuardedState.Clone();
            var topicCues = session.Guarded.Facts.Select(f => (f.Id, f.Topic)).ToList();

            NpcGuardedClassifyResult classify;
            try
            {
                classify = await brainClient.ClassifyGuardedAsync(
                    new NpcGuardedClassifyRequest
                    {
                        NpcId = npcId,
                        Persona = session.Persona,
                        Concern = session.Guarded.Concern,
                        TopicCues = topicCues,
                        State = stateSnapshot,
                        Utterances = parsedTurns
                    }
                );
            }
            catch (Exception ex)
            {
                logger.LogError($"npc_turn guarded: classifier threw for '{npcId}'", ex);
                classify = null;
            }

            if (classify?.Classifications is null)
            {
                await StreamSafeAndSkipCommit(apiPort, session, npcId, turnId, NpcGuardedProfile.SafeDeflection);
                await SendDebugStateAsync(
                    apiPort,
                    npcId,
                    classify?.Provider,
                    "answer",
                    classifyMs: classify?.Ms ?? 0,
                    disclosedFactIds: stateSnapshot.DisclosedFactIds
                );
                return;
            }

            var engine = NpcGuardedProfile.Evaluate(stateSnapshot, session.Guarded, classify.Classifications);
            NpcGuardedReplyResult modelReply;
            try
            {
                modelReply = await brainClient.ReplyGuardedAsync(
                    new NpcGuardedReplyRequest
                    {
                        NpcId = npcId,
                        Persona = session.Persona,
                        Knowledge = session.Knowledge,
                        History = NpcHistoryBudget.Trim(session.History),
                        NewTurns = parsedTurns,
                        Directive = engine.Directive,
                        PermittedFactId = engine.PermittedFactId,
                        PermittedFactTopic = engine.PermittedFactTopic,
                        VoiceId = session.VoiceId
                    }
                );
            }
            catch (Exception ex)
            {
                logger.LogError($"npc_turn guarded: reply threw for '{npcId}'", ex);
                modelReply = null;
            }

            var validated = modelReply is { Ok: true }
                ? NpcGuardedReplyValidator.Validate(
                    new NpcGuardedReplyModelOutput
                    {
                        Text = modelReply.Text,
                        Mood = modelReply.Mood,
                        Emote = modelReply.Emote,
                        DisclosedFactId = modelReply.DisclosedFactId
                    },
                    session.Guarded,
                    engine.PermittedFactId,
                    engine.PermittedFactText,
                    stateSnapshot.DisclosedFactIds
                )
                : null;

            if (modelReply is null || !modelReply.Ok) logger.LogWarning($"npc_turn guarded: reply failed for '{npcId}' — {modelReply?.Failure ?? "null"}");
            if (validated is { Ok: false }) logger.LogWarning($"npc_turn guarded: validation failed for '{npcId}' — {validated.Failure}");

            var (spoken, mood, emote, disclosedId, commitState) = ResolveGuardedOutput(engine, validated);
            // Invalid/model fallback uses neutral voice, never the failed model mood voice.
            var voiceId = validated is { Ok: true }
                ? modelReply?.VoiceId ?? ResolveGuardedVoice(session.VoiceId, mood)
                : ResolveGuardedVoice(session.VoiceId, mood);

            var delivered = await StreamDynamicTurn(
                apiPort,
                npcId,
                turnId,
                new RespondResult
                {
                    Text = spoken,
                    Mood = mood,
                    VoiceId = voiceId
                }
            );

            if (!delivered)
            {
                logger.LogWarning($"npc_turn guarded: zero TTS frames for '{npcId}' turn '{turnId}' — state/history unchanged");
                await SendGuardedDebugStateAsync(apiPort, npcId, classify, session.Guarded, engine, modelReply, stateSnapshot.DisclosedFactIds);
                return;
            }

            if (!commitState)
            {
                await SendGuardedDebugStateAsync(apiPort, npcId, classify, session.Guarded, engine, modelReply, stateSnapshot.DisclosedFactIds);
                return;
            }

            var nextState = engine.NextState.Clone();
            if (!string.IsNullOrEmpty(disclosedId) && !nextState.DisclosedFactIds.Contains(disclosedId)) nextState.DisclosedFactIds.Add(disclosedId);

            var committed = await CommitGuardedAsync(session, npcId, sessionId, parsedTurns, spoken, mood, nextState);
            if (!committed)
            {
                await SendGuardedDebugStateAsync(apiPort, npcId, classify, session.Guarded, engine, modelReply, nextState.DisclosedFactIds);
                return;
            }

            await commandSender.SendCommandAsync(
                apiPort,
                NpcAudioEnvelopeBuilder.BuildGuardedState(
                    npcId,
                    nextState.CooperationBand,
                    nextState.PendingWarning,
                    nextState.Burned,
                    nextState.DisclosedFactIds,
                    engine.PermittedFactId,
                    mood,
                    emote,
                    SummariseReasons(engine, session.Guarded),
                    SummariseEvidence(engine, session.Guarded),
                    classify.Ms,
                    modelReply?.Ms ?? 0
                )
            );

            await SendGuardedDebugStateAsync(apiPort, npcId, classify, session.Guarded, engine, modelReply, nextState.DisclosedFactIds);
        }
        finally
        {
            GuardedTurnLock.Release();
        }
    }

    private static (string Spoken, string Mood, string Emote, string DisclosedId, bool CommitState) ResolveGuardedOutput(
        NpcGuardedEngineResult engine,
        NpcGuardedValidatedReply validated
    )
    {
        if (validated is { Ok: true }) return (validated.SpokenText, validated.Mood, validated.Emote, validated.DisclosedFactId, true);

        var spoken = NpcGuardedProfile.FallbackFor(engine.Directive);
        var commit = engine.Directive is NpcGuardedDirectives.Warn or NpcGuardedDirectives.BackOff or NpcGuardedDirectives.Burned
            or NpcGuardedDirectives.Refuse;
        return (spoken, MoodScripts.Neutral, null, null, commit);
    }
}
