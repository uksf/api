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

        // Prerendered clips are neutral delivery, so they use the neutral variant exactly as a
        // dynamic neutral turn does. Cutting them from the raw seed would leave the filler and
        // the scripted lines as the only audio a player hears in a different voice.
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

        foreach (var (fillerId, _) in Fillers)
        {
            var fillerClip = result.Items.Find(i => i.Id == fillerId);
            if (fillerClip is null)
            {
                logger.LogWarning($"NPC prerender missing filler '{fillerId}' for voiceId '{prerenderVoiceId}'");
                continue;
            }

            foreach (var cmd in NpcAudioEnvelopeBuilder.BuildFiller(npcId, voiceId, fillerId, fillerClip.AudioBase64, fillerClip.DurationMs))
            {
                await commandSender.SendCommandAsync(apiPort, cmd);
            }
        }
    }
}
