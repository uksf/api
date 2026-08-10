using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Npc.Models;
using UKSF.Api.Core;

namespace UKSF.Api.ArmaServer.Npc.Services;

public interface INpcBrainClient
{
    Task<RespondResult> RespondAsync(RespondRequest request);
    Task<PrerenderResult> PrerenderAsync(PrerenderRequest request);
    Task<NpcGuardedClassifyResult> ClassifyGuardedAsync(NpcGuardedClassifyRequest request);
    Task<NpcGuardedReplyResult> ReplyGuardedAsync(NpcGuardedReplyRequest request);
}

/// <summary>
/// The absorbed arma-npc brain: builds NPC prompts, asks the clacks mesh (role "npc"),
/// resolves scripted line choices, cleans dynamic replies, and voices them (role "npc-voice").
/// Scripted turns use prerendered clips, so only dynamic turns synth at respond time.
/// </summary>
public class NpcBrainService(IClacksClient clacksClient, INpcVoicesContext voicesContext, IUksfLogger logger) : INpcBrainClient
{
    public async Task<RespondResult> RespondAsync(RespondRequest request)
    {
        var system = NpcPromptBuilder.BuildSystemPrompt(request);
        var user = NpcPromptBuilder.BuildUserPrompt(request);
        var scripted = request.Mode == "scripted";

        var result = await clacksClient.ChatAsync(
            "npc",
            system,
            user,
            scripted,
            80,
            0.7,
            new
            {
                npcId = request.NpcId,
                persona = request.Persona?.Name,
                mode = request.Mode
            }
        );
        if (result is null) return null;

        var provider = $"{result.Model}@{result.Node}";
        logger.LogInfo($"NPC turn npcId '{request.NpcId}' ({request.Mode}) served by {provider}");

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

        if (string.Equals(result.Text?.Trim(), "[none]", StringComparison.OrdinalIgnoreCase))
        {
            return new RespondResult { Text = "[none]", Provider = provider };
        }

        var (mood, body) = NpcReplyCleaner.ExtractMood(result.Text);
        var cleanText = NpcReplyCleaner.Clean(body);
        if (request.TextOnly)
        {
            return new RespondResult
            {
                Text = cleanText,
                LineId = null,
                AudioBase64 = null,
                DurationMs = null,
                Provider = provider,
                Mood = mood,
                VoiceId = ResolveVoiceId(request.VoiceId, mood)
            };
        }

        var voiceId = ResolveVoiceId(request.VoiceId, mood);
        var speech = cleanText.Length == 0 ? null : await clacksClient.SpeakAsync("npc-voice", cleanText, voiceId);
        if (speech is null) logger.LogWarning($"NPC speak failed for npcId '{request.NpcId}' — turn will be silent");
        return new RespondResult
        {
            Text = cleanText,
            LineId = null,
            AudioBase64 = speech?.AudioBase64,
            DurationMs = speech?.DurationMs,
            Provider = provider,
            Mood = mood
        };
    }

    public async Task<NpcGuardedClassifyResult> ClassifyGuardedAsync(NpcGuardedClassifyRequest request)
    {
        var system = NpcGuardedPromptBuilder.BuildClassifierSystemPrompt(request);
        var user = NpcGuardedPromptBuilder.BuildClassifierUserPrompt(request);
        var result = await clacksClient.ChatAsync(
            "npc",
            system,
            user,
            json: true,
            maxTokens: 400,
            temperature: 0,
            meta: new { npcId = request.NpcId, kind = "guarded-classify" }
        );
        if (result is null) return null;

        var provider = $"{result.Model}@{result.Node}";
        logger.LogInfo($"NPC guarded classify npcId '{request.NpcId}' served by {provider} ({result.Ms}ms)");

        try
        {
            var parsed = JsonSerializer.Deserialize<GuardedClassifyJson>(result.Text ?? "", NpcBrainJson.Options);
            if (parsed?.Classifications is null) return null;

            // Untrusted model output: exact count/order/t + known tags + evidence contract.
            var cleaned = NpcGuardedClassificationValidator.Validate(parsed.Classifications, request.Utterances);
            if (cleaned is null)
            {
                logger.LogWarning($"NPC guarded classify rejected for '{request.NpcId}' — contract mismatch");
                return null;
            }

            return new NpcGuardedClassifyResult
            {
                Classifications = cleaned,
                Provider = provider,
                Ms = result.Ms
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning($"NPC guarded classify parse failed for '{request.NpcId}': {ex.Message}");
            return null;
        }
    }

    public async Task<NpcGuardedReplyResult> ReplyGuardedAsync(NpcGuardedReplyRequest request)
    {
        var system = NpcGuardedPromptBuilder.BuildReplySystemPrompt(request);
        var user = NpcGuardedPromptBuilder.BuildReplyUserPrompt(request);
        var result = await clacksClient.ChatAsync(
            "npc",
            system,
            user,
            json: true,
            maxTokens: 160,
            temperature: 0.4,
            meta: new { npcId = request.NpcId, kind = "guarded-reply" }
        );
        if (result is null) return new NpcGuardedReplyResult { Ok = false, Failure = "null model" };

        var provider = $"{result.Model}@{result.Node}";
        logger.LogInfo($"NPC guarded reply npcId '{request.NpcId}' served by {provider} ({result.Ms}ms)");

        try
        {
            var parsed = JsonSerializer.Deserialize<NpcGuardedReplyModelOutput>(result.Text ?? "", NpcBrainJson.Options);
            if (parsed is null)
                return new NpcGuardedReplyResult
                {
                    Ok = false,
                    Failure = "null json",
                    Provider = provider,
                    Ms = result.Ms
                };

            var mood = string.IsNullOrWhiteSpace(parsed.Mood) ? MoodScripts.Neutral : parsed.Mood.Trim().ToLowerInvariant();
            return new NpcGuardedReplyResult
            {
                Ok = true,
                Text = parsed.Text ?? "",
                Mood = mood,
                Emote = parsed.Emote,
                DisclosedFactId = parsed.DisclosedFactId,
                Provider = provider,
                VoiceId = ResolveVoiceId(request.VoiceId, mood),
                Ms = result.Ms
            };
        }
        catch (Exception ex)
        {
            return new NpcGuardedReplyResult
            {
                Ok = false,
                Failure = $"parse: {ex.Message}",
                Provider = provider,
                Ms = result.Ms
            };
        }
    }

    public async Task<PrerenderResult> PrerenderAsync(PrerenderRequest request)
    {
        var items = new List<PrerenderResultItem>();
        foreach (var item in request.Items)
        {
            var speech = await clacksClient.SpeakAsync("npc-voice", item.Text, request.VoiceId);
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

    private string ResolveVoiceId(string baseVoiceId, string mood)
    {
        var variant = $"{baseVoiceId}_{mood}";
        return voicesContext.GetSingle(x => x.VoiceId == variant) is not null ? variant : baseVoiceId;
    }

    private sealed class GuardedClassifyJson
    {
        public List<NpcGuardedClassification> Classifications { get; set; }
    }
}
