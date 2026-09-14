using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UKSF.Api.ArmaServer.Npc.Models;

namespace UKSF.Api.ArmaServer.Npc.Services;

// Prerender half of the broker: the clips that exist before a player says anything —
// scripted lines, the deflection, and the latency fillers. All are neutral delivery and
// are cut once per registration.
public partial class NpcBrokerService
{
    private async Task PrerenderClipsAsync(int apiPort, string npcId, string sessionId, string voiceId, string mode, NpcScripted scripted)
    {
        await PushFillersAsync(apiPort, npcId, voiceId);

        var items = new List<PrerenderItem>();
        if (mode == "scripted")
        {
            foreach (var line in scripted.Lines)
            {
                items.Add(new PrerenderItem { Id = line.Id, Text = line.Line });
            }

            items.Add(new PrerenderItem { Id = DeflectionId, Text = scripted.Deflection });
        }

        if (items.Count == 0) return; // dynamic mode prerenders nothing

        // Prerendered clips are neutral delivery, so they use the neutral variant exactly as a
        // dynamic neutral turn does. Cutting them from the raw seed would leave the scripted
        // lines as the only audio a player hears in a different voice.
        var neutralVariant = $"{voiceId}_{MoodScripts.Neutral}";
        var prerenderVoiceId = voicesContext.GetSingle(x => x.VoiceId == neutralVariant) is not null ? neutralVariant : voiceId;

        var result = await brainClient.PrerenderAsync(new PrerenderRequest { VoiceId = prerenderVoiceId, Items = items });
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
                VoiceId = prerenderVoiceId,
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
    }

    /// Push the pre-rendered filler clips for this voice. Fillers are voice assets rendered
    /// offline by the mood worker; registration only reads and forwards what is on disk.
    private async Task PushFillersAsync(int apiPort, string npcId, string voiceId)
    {
        foreach (var (fillerId, _) in NpcFillers.Table)
        {
            var bytes = await voiceStore.ReadAsync(NpcFillers.RelativePath(voiceId, fillerId));
            if (bytes is null)
            {
                logger.LogWarning($"filler '{fillerId}' missing for voice '{voiceId}' — run generate-moods to render it");
                continue;
            }

            var base64 = Convert.ToBase64String(bytes);
            foreach (var cmd in NpcAudioEnvelopeBuilder.BuildFiller(npcId, voiceId, fillerId, base64, WavLoudness.DurationMs(bytes)))
            {
                await commandSender.SendCommandAsync(apiPort, cmd);
            }
        }
    }
}
