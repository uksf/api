using System;
using System.Threading.Tasks;
using UKSF.Api.ArmaServer.Npc.Models;

namespace UKSF.Api.ArmaServer.Npc.Services;

// Turn-serving half of the broker. HandleTurnAsync decides scripted vs dynamic and
// hands off here: scripted plays one prerendered clip whole, dynamic streams PCM
// frames from clacks as they are synthesised.
public partial class NpcBrokerService
{
    /// Serve a scripted line from its prerendered clip and send it as one complete clip.
    private async Task SendScriptedClip(int apiPort, DomainNpcSession session, string npcId, string turnId, RespondResult result)
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

        foreach (var cmd in NpcAudioEnvelopeBuilder.BuildAudio(npcId, turnId, Convert.ToBase64String(bytes), clip.DurationMs))
        {
            await commandSender.SendCommandAsync(apiPort, cmd);
        }
    }

    /// Stream a dynamic line. Returns true when at least one TTS frame was emitted (delivery evidence).
    private async Task<bool> StreamDynamicTurn(int apiPort, string npcId, string turnId, RespondResult result)
    {
        if (string.IsNullOrEmpty(result.Text))
        {
            logger.LogWarning($"npc_turn: dynamic response had no text for npcId '{npcId}'");
            return false;
        }

        var voiceId = string.IsNullOrEmpty(result.VoiceId) ? "oracle" : result.VoiceId;
        var seq = 0;
        try
        {
            await clacksClient.SpeakStreamAsync(
                "npc-voice",
                result.Text,
                voiceId,
                async frame =>
                {
                    await commandSender.SendCommandAsync(apiPort, NpcAudioEnvelopeBuilder.BuildAudioFrame(npcId, turnId, seq, frame));
                    seq++;
                }
            );
        }
        catch (Exception exception)
        {
            logger.LogError($"npc_turn: dynamic stream failed for turnId '{turnId}'", exception);
        }

        // Always close so a partial or failed synthesis never leaves a clip hanging open.
        await commandSender.SendCommandAsync(apiPort, NpcAudioEnvelopeBuilder.BuildAudioEnd(npcId, turnId));
        return seq > 0;
    }
}
