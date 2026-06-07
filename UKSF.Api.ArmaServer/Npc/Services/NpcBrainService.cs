using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UKSF.Api.ArmaServer.Npc.Models;
using UKSF.Api.Core;

namespace UKSF.Api.ArmaServer.Npc.Services;

public interface INpcBrainClient
{
    Task<RespondResult> RespondAsync(RespondRequest request);
    Task<PrerenderResult> PrerenderAsync(PrerenderRequest request);
}

/// <summary>
/// The absorbed arma-npc brain: builds NPC prompts, asks the clacks mesh (role "npc"),
/// resolves scripted line choices, cleans dynamic replies, and voices them (role "voice").
/// Scripted turns use prerendered clips, so only dynamic turns synth at respond time.
/// </summary>
public class NpcBrainService(IClacksClient clacksClient, IUksfLogger logger) : INpcBrainClient
{
    public async Task<RespondResult> RespondAsync(RespondRequest request)
    {
        var system = NpcPromptBuilder.BuildSystemPrompt(request);
        var user = NpcPromptBuilder.BuildUserPrompt(request);
        var scripted = request.Mode == "scripted";

        var result = await clacksClient.ChatAsync("npc", system, user, scripted, 80, 0.7);
        if (result is null) return null;

        var provider = $"{result.Model}@{result.Node}";

        if (scripted)
        {
            var options = request.Scripted ?? new NpcScriptedDto();
            var choice = NpcPromptBuilder.ParseScriptedChoice(result.Text);
            var line = choice is null ? null : options.Lines.FirstOrDefault(l => l.Id == choice);
            var lineId = line is not null ? line.Id : NpcPromptBuilder.Deflection;
            var text = line is not null ? line.Line : options.Deflection;
            return new RespondResult
            {
                Text = text,
                LineId = lineId,
                AudioBase64 = null,
                DurationMs = null,
                Provider = provider
            };
        }

        var cleanText = NpcReplyCleaner.Clean(result.Text);
        var speech = await clacksClient.SpeakAsync("voice", cleanText, request.VoiceId);
        if (speech is null) logger.LogWarning($"NPC speak failed for npcId '{request.NpcId}' — turn will be silent");
        return new RespondResult
        {
            Text = cleanText,
            LineId = null,
            AudioBase64 = speech?.AudioBase64,
            DurationMs = speech?.DurationMs,
            Provider = provider
        };
    }

    public async Task<PrerenderResult> PrerenderAsync(PrerenderRequest request)
    {
        var items = new List<PrerenderResultItem>();
        // Sequential on purpose — one python child per node; parallel submits just queue inside it.
        foreach (var item in request.Items)
        {
            var speech = await clacksClient.SpeakAsync("voice", item.Text, request.VoiceId);
            if (speech is null)
            {
                logger.LogWarning($"NPC prerender failed for clip '{item.Id}' (voiceId '{request.VoiceId}') — skipped");
                continue;
            }

            items.Add(
                new PrerenderResultItem
                {
                    Id = item.Id,
                    AudioBase64 = speech.AudioBase64,
                    DurationMs = speech.DurationMs
                }
            );
        }

        return new PrerenderResult { Items = items };
    }
}
