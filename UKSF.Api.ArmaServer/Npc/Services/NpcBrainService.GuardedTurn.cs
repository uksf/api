using System;
using System.Text.Json;
using System.Threading.Tasks;
using UKSF.Api.ArmaServer.Npc.Models;

namespace UKSF.Api.ArmaServer.Npc.Services;

public partial class NpcBrainService
{
    public async Task<NpcGuardedTurnResult> TurnGuardedAsync(NpcGuardedTurnRequest request)
    {
        var system = NpcGuardedPromptBuilder.BuildTurnSystemPrompt(request);
        var user = NpcGuardedPromptBuilder.BuildTurnUserPrompt(request);
        var result = await clacksClient.ChatAsync(
            "npc",
            system,
            user,
            json: true,
            maxTokens: 400,
            temperature: 0.3,
            meta: new { npcId = request.NpcId, kind = "guarded-turn" }
        );
        if (result is null)
        {
            return new NpcGuardedTurnResult { Reply = new NpcGuardedReplyResult { Ok = false, Failure = "null model" } };
        }

        var provider = $"{result.Model}@{result.Node}";
        logger.LogInfo($"NPC guarded turn npcId '{request.NpcId}' served by {provider} ({result.Ms}ms)");

        try
        {
            var parsed = JsonSerializer.Deserialize<GuardedTurnJson>(result.Text ?? "", NpcBrainJson.Options);
            if (parsed is null)
            {
                return new NpcGuardedTurnResult
                {
                    Reply = new NpcGuardedReplyResult
                    {
                        Ok = false,
                        Failure = "null json",
                        Provider = provider,
                        Ms = result.Ms
                    }
                };
            }

            var cleaned = NpcGuardedClassificationValidator.Validate(parsed.Classifications, request.NewTurns);
            NpcGuardedClassifyResult classify = null;
            if (cleaned is null)
            {
                logger.LogWarning($"NPC guarded turn classify rejected for '{request.NpcId}'");
            }
            else
            {
                classify = new NpcGuardedClassifyResult
                {
                    Classifications = cleaned,
                    Provider = provider,
                    Ms = result.Ms
                };
            }

            var mood = MoodScripts.Normalise(parsed.Mood);
            return new NpcGuardedTurnResult
            {
                Classify = classify,
                Reply = new NpcGuardedReplyResult
                {
                    Ok = true,
                    Text = parsed.Text ?? "",
                    Mood = mood,
                    Emote = parsed.Emote,
                    DisclosedFactId = parsed.DisclosedFactId,
                    Provider = provider,
                    VoiceId = ResolveVoiceId(request.VoiceId, mood),
                    Ms = result.Ms
                }
            };
        }
        catch (Exception ex)
        {
            return new NpcGuardedTurnResult
            {
                Reply = new NpcGuardedReplyResult
                {
                    Ok = false,
                    Failure = $"parse: {ex.Message}",
                    Provider = provider,
                    Ms = result.Ms
                }
            };
        }
    }

    private sealed class GuardedTurnJson : NpcGuardedReplyModelOutput
    {
        [System.Text.Json.Serialization.JsonPropertyName("classifications")]
        public System.Collections.Generic.List<NpcGuardedClassification> Classifications { get; set; }
    }
}
