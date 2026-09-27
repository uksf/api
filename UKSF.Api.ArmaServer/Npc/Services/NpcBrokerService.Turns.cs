using System;
using System.Diagnostics;
using System.Threading.Tasks;
using UKSF.Api.ArmaServer.Npc.Models;
using UKSF.Api.ArmaServer.Npc.Observability;

namespace UKSF.Api.ArmaServer.Npc.Services;

// Turn-serving half of the broker. HandleTurnAsync decides scripted vs dynamic and
// hands off here: scripted plays one prerendered clip whole, dynamic streams PCM
// frames from clacks as they are synthesised.
public partial class NpcBrokerService
{
    /// Serve a scripted line from its prerendered clip and send it as one complete clip.
    private async Task<bool> SendScriptedClip(int apiPort, DomainNpcSession session, string npcId, string turnId, RespondResult result)
    {
        var lineId = string.IsNullOrEmpty(result.LineId) ? DeflectionId : result.LineId;
        if (NpcTraceScope.Current?.Turn is { } turnTrace) turnTrace.Clip = lineId;
        var clip = clipsContext.GetSingle(x => x.SessionId == session.SessionId && x.NpcId == npcId && x.ClipId == lineId);
        if (clip is null)
        {
            logger.LogWarning($"npc_turn: scripted clip not found for voiceId='{session.VoiceId}', lineId='{lineId}'");
            await commandSender.SendCommandAsync(apiPort, NpcAudioEnvelopeBuilder.BuildTurnCancel(npcId, turnId));
            return false;
        }

        var bytes = await audioStore.ReadAsync(clip.FilePath);
        if (bytes is null)
        {
            logger.LogWarning($"npc_turn: scripted clip file missing '{clip.FilePath}' for lineId '{lineId}'");
            await commandSender.SendCommandAsync(apiPort, NpcAudioEnvelopeBuilder.BuildTurnCancel(npcId, turnId));
            return false;
        }

        foreach (var cmd in NpcAudioEnvelopeBuilder.BuildAudio(npcId, turnId, Convert.ToBase64String(bytes), clip.DurationMs))
        {
            await commandSender.SendCommandAsync(apiPort, cmd);
        }

        return true;
    }

    /// Stream a dynamic line. Returns true when at least one TTS frame was emitted (delivery evidence).
    private async Task<bool> StreamDynamicTurn(int apiPort, string npcId, string turnId, RespondResult result)
    {
        var tts = await StreamDynamicTurnCore(apiPort, npcId, turnId, result);
        if (NpcTraceScope.Current?.Turn is { } turnTrace) turnTrace.Tts = tts;
        return tts.Delivered;
    }

    /// Send a plain turn's emote after its speech is out. Never waits on Arma.
    private async Task<bool?> SendEmoteAsync(int apiPort, string npcId, string turnId, string emote)
    {
        if (string.IsNullOrWhiteSpace(emote)) return null;
        await commandSender.SendCommandAsync(apiPort, NpcAudioEnvelopeBuilder.BuildEmote(npcId, turnId, emote.Trim()));
        return true;
    }

    private async Task<NpcTtsOutcome> StreamDynamicTurnCore(int apiPort, string npcId, string turnId, RespondResult result)
    {
        if (string.IsNullOrEmpty(result.Text))
        {
            logger.LogWarning($"npc_turn: dynamic response had no text for npcId '{npcId}'");
            await commandSender.SendCommandAsync(apiPort, NpcAudioEnvelopeBuilder.BuildTurnCancel(npcId, turnId));
            return NpcTtsOutcome.Skipped;
        }

        var watch = Stopwatch.StartNew();
        long firstFrameMs = 0;
        string error = null;
        var voiceId = string.IsNullOrEmpty(result.VoiceId) ? "oracle" : result.VoiceId;
        var seq = 0;
        var failed = false;
        try
        {
            await clacksClient.SpeakStreamAsync(
                "npc-voice",
                result.Text,
                voiceId,
                async frame =>
                {
                    if (seq == 0) firstFrameMs = watch.ElapsedMilliseconds;
                    await commandSender.SendCommandAsync(apiPort, NpcAudioEnvelopeBuilder.BuildAudioFrame(npcId, turnId, seq, frame));
                    seq++;
                }
            );
        }
        catch (Exception exception)
        {
            failed = true;
            error = exception.GetType().Name;
            logger.LogError($"npc_turn: dynamic stream failed for turnId '{turnId}'", exception);
        }

        if (seq == 0)
        {
            await commandSender.SendCommandAsync(apiPort, NpcAudioEnvelopeBuilder.BuildTurnCancel(npcId, turnId));
            return new NpcTtsOutcome(false, voiceId, result.Text, 0, 0, watch.ElapsedMilliseconds, error ?? "no frames");
        }

        await commandSender.SendCommandAsync(apiPort, NpcAudioEnvelopeBuilder.BuildAudioEnd(npcId, turnId));
        return new NpcTtsOutcome(!failed, voiceId, result.Text, seq, firstFrameMs, watch.ElapsedMilliseconds, error);
    }
}
