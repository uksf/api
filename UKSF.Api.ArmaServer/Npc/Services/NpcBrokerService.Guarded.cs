using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UKSF.Api.ArmaServer.Npc.Models;

namespace UKSF.Api.ArmaServer.Npc.Services;

// Guarded-source turn: one combined model call, engine state, then TTS.
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
                await commandSender.SendCommandAsync(apiPort, NpcAudioEnvelopeBuilder.BuildTurnCancel(npcId, turnId));
                await SendDebugStateAsync(apiPort, npcId, "", "stay_silent");
                return;
            }

            if (session.Guarded is null || session.Guarded.Facts.Count != 3)
            {
                logger.LogWarning($"npc_turn guarded: missing config for '{npcId}'");
                await StreamSafeAndSkipCommit(apiPort, session, npcId, turnId, NpcGuardedProfile.SafeDeflection);
                await SendDebugStateAsync(
                    apiPort,
                    npcId,
                    "",
                    "answer",
                    disclosedFactIds: session.GuardedState?.DisclosedFactIds,
                    spoken: NpcGuardedProfile.SafeDeflection
                );
                return;
            }

            session.GuardedState ??= new NpcGuardedState();
            var stateSnapshot = session.GuardedState.Clone();
            var facts = session.Guarded.Facts;
            var includedNext = NpcGuardedProfile.NextIncludableFact(session.Guarded, stateSnapshot);
            var disclosedFacts = facts.Where(f => stateSnapshot.DisclosedFactIds.Any(id => NpcGuardedFactIds.Same(id, f.Id))).ToList();
            var laterTopics = facts
                              .Where(f => includedNext is null || !NpcGuardedFactIds.Same(f.Id, includedNext.Id))
                              .Where(f => !disclosedFacts.Any(d => NpcGuardedFactIds.Same(d.Id, f.Id)))
                              .Select(f => (f.Id, f.Topic))
                              .ToList();
            var topicCues = facts.Select(f => (f.Id, f.Topic)).ToList();

            NpcGuardedTurnResult turn;
            try
            {
                turn = await brainClient.TurnGuardedAsync(
                    new NpcGuardedTurnRequest
                    {
                        NpcId = npcId,
                        Persona = session.Persona,
                        Knowledge = session.Knowledge,
                        Concern = session.Guarded.Concern,
                        TopicCues = topicCues,
                        DisclosedFacts = disclosedFacts,
                        NextFact = includedNext,
                        LaterTopics = laterTopics,
                        State = stateSnapshot,
                        History = NpcHistoryBudget.Trim(session.History),
                        NewTurns = parsedTurns,
                        VoiceId = session.VoiceId
                    }
                );
            }
            catch (Exception ex)
            {
                logger.LogError($"npc_turn guarded: turn threw for '{npcId}'", ex);
                turn = null;
            }

            var classify = turn?.Classify;
            var modelReply = turn?.Reply;
            if (classify?.Classifications is null)
            {
                await StreamSafeAndSkipCommit(apiPort, session, npcId, turnId, NpcGuardedProfile.SafeDeflection);
                await SendDebugStateAsync(
                    apiPort,
                    npcId,
                    classify?.Provider ?? modelReply?.Provider,
                    "answer",
                    classifyMs: classify?.Ms ?? modelReply?.Ms ?? 0,
                    disclosedFactIds: stateSnapshot.DisclosedFactIds,
                    spoken: NpcGuardedProfile.SafeDeflection
                );
                return;
            }

            var engine = NpcGuardedProfile.Evaluate(stateSnapshot, session.Guarded, classify.Classifications);

            var validated = modelReply is { Ok: true }
                ? NpcGuardedReplyValidator.Validate(
                    new NpcGuardedReplyModelOutput
                    {
                        Text = modelReply.Text,
                        Mood = modelReply.Mood,
                        Emote = modelReply.Emote,
                        DisclosedFactId = modelReply.DisclosedFactId
                    },
                    includedNext
                )
                : null;
            validated = NpcGuardedReplyValidator.Guard(validated, modelReply?.DisclosedFactId, includedNext?.Id, session.Guarded, stateSnapshot.DisclosedFactIds, engine.PermittedFactId);

            if (modelReply is null || !modelReply.Ok) logger.LogWarning($"npc_turn guarded: reply failed for '{npcId}' — {modelReply?.Failure ?? "null"}");
            if (validated is { Ok: false }) logger.LogWarning($"npc_turn guarded: validation failed for '{npcId}' — {validated.Failure}");

            var (spoken, mood, emote, disclosedId, commitState) = ResolveGuardedOutput(validated, modelReply);
            var voiceId = commitState
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
                logger.LogWarning($"npc_turn guarded: stream not delivered for '{npcId}' turn '{turnId}' — state/history unchanged");
                await SendGuardedDebugStateAsync(apiPort, npcId, classify, session.Guarded, engine, modelReply, stateSnapshot.DisclosedFactIds);
                return;
            }

            if (!commitState)
            {
                await SendGuardedDebugStateAsync(apiPort, npcId, classify, session.Guarded, engine, modelReply, stateSnapshot.DisclosedFactIds, spoken);
                return;
            }

            var nextState = engine.NextState.Clone();
            if (!string.IsNullOrEmpty(disclosedId) && !nextState.DisclosedFactIds.Any(id => NpcGuardedFactIds.Same(id, disclosedId))) nextState.DisclosedFactIds.Add(disclosedId);

            var committed = await CommitGuardedAsync(session, npcId, sessionId, parsedTurns, spoken, mood, nextState);
            if (!committed)
            {
                await SendGuardedDebugStateAsync(apiPort, npcId, classify, session.Guarded, engine, modelReply, nextState.DisclosedFactIds, spoken);
                return;
            }

            await commandSender.SendCommandAsync(
                apiPort,
                NpcAudioEnvelopeBuilder.BuildGuardedState(
                    npcId,
                    turnId,
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

            await SendGuardedDebugStateAsync(apiPort, npcId, classify, session.Guarded, engine, modelReply, nextState.DisclosedFactIds, spoken);
        }
        finally
        {
            GuardedTurnLock.Release();
        }
    }

    private static (string Spoken, string Mood, string Emote, string DisclosedId, bool CommitState) ResolveGuardedOutput(
        NpcGuardedValidatedReply validated,
        NpcGuardedReplyResult modelReply
    )
    {
        if (validated is { Ok: true }) return (validated.SpokenText, validated.Mood, validated.Emote, validated.DisclosedFactId, true);
        return (NpcGuardedProfile.SafeDeflection, MoodScripts.Neutral, null, null, false);
    }
}
