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
/// resolves scripted line choices, and cleans dynamic replies. Audio fields stay null
/// until TTS is served by clacks (voicebox follow-up build) — the broker treats a
/// missing-audio turn as silent, and prerender stores no clips.
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

        return new RespondResult
        {
            Text = NpcReplyCleaner.Clean(result.Text),
            LineId = null,
            AudioBase64 = null,
            DurationMs = null,
            Provider = provider
        };
    }

    public Task<PrerenderResult> PrerenderAsync(PrerenderRequest request)
    {
        logger.LogWarning("NPC prerender skipped — TTS not yet served by clacks (voicebox build pending)");
        return Task.FromResult<PrerenderResult>(null);
    }
}
