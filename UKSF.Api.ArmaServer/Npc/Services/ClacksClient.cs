using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using UKSF.Api.ArmaServer.Npc.Models;
using UKSF.Api.Core;
using UKSF.Api.Core.Services;

namespace UKSF.Api.ArmaServer.Npc.Services;

public class ClacksChatResult
{
    public string Text { get; set; } = string.Empty;
    public string Node { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public long Ms { get; set; }
}

public class ClacksSpeakResult
{
    public string AudioBase64 { get; set; } = string.Empty;
    public long DurationMs { get; set; }
    public string Node { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public long Ms { get; set; }
}

public enum EmoteStatus
{
    Ok,
    NodeDown,
    Failed
}

public class ClacksEmoteResult
{
    public EmoteStatus Status { get; init; }
    public byte[] WavBytes { get; init; }
    public long DurationMs { get; init; }

    public static ClacksEmoteResult NodeDown() => new() { Status = EmoteStatus.NodeDown };
    public static ClacksEmoteResult Failed() => new() { Status = EmoteStatus.Failed };
}

public interface IClacksClient
{
    Task<ClacksChatResult> ChatAsync(string role, string system, string user, bool json, int maxTokens, double temperature, object meta = null);
    Task<ClacksSpeakResult> SpeakAsync(string role, string text, string voiceId);
    Task<bool> PutVoiceAsync(string voiceId, byte[] wavBytes);
    Task<ClacksEmoteResult> EmoteAsync(string voiceId, string text, string emoText, double emoAlpha);
}

// HTTP client for the local clacks daemon (the LLM mesh). The daemon owns all model routing/fallback;
// this client just asks for a role and reads text back.
public class ClacksClient(IHttpClientFactory httpClientFactory, IVariablesService variablesService, IUksfLogger logger) : IClacksClient
{
    public async Task<ClacksChatResult> ChatAsync(string role, string system, string user, bool json, int maxTokens, double temperature, object meta = null)
    {
        // Non-throwing read: AsString() throws on a missing item, which would make this guard dead code
        var baseUrl = variablesService.GetVariable("CLACKS_URL")?.Item?.ToString()?.TrimEnd('/');
        if (string.IsNullOrEmpty(baseUrl))
        {
            logger.LogWarning("CLACKS_URL not configured — clacks call skipped");
            return null;
        }

        try
        {
            using var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);
            var response = await client.PostAsJsonAsync(
                $"{baseUrl}/chat",
                new
                {
                    role,
                    system,
                    user,
                    json,
                    maxTokens,
                    temperature,
                    meta // null is omitted by WhenWritingNull; carries per-role context for the mesh dashboard
                },
                NpcBrainJson.Options
            );
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning($"clacks /chat returned {(int)response.StatusCode} for role '{role}'");
                return null;
            }

            return await response.Content.ReadFromJsonAsync<ClacksChatResult>(NpcBrainJson.Options);
        }
        catch (Exception exception)
        {
            logger.LogError($"clacks /chat call failed for role '{role}'", exception);
            return null;
        }
    }

    public async Task<ClacksSpeakResult> SpeakAsync(string role, string text, string voiceId)
    {
        var baseUrl = variablesService.GetVariable("CLACKS_URL")?.Item?.ToString()?.TrimEnd('/');
        if (string.IsNullOrEmpty(baseUrl))
        {
            logger.LogWarning("CLACKS_URL not configured — clacks call skipped");
            return null;
        }

        try
        {
            using var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(90);
            var response = await client.PostAsJsonAsync(
                $"{baseUrl}/speak",
                new
                {
                    role,
                    text,
                    voiceId
                },
                NpcBrainJson.Options
            );
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning($"clacks /speak returned {(int)response.StatusCode} for role '{role}'");
                return null;
            }

            return await response.Content.ReadFromJsonAsync<ClacksSpeakResult>(NpcBrainJson.Options);
        }
        catch (Exception exception)
        {
            logger.LogError($"clacks /speak call failed for role '{role}'", exception);
            return null;
        }
    }

    public async Task<bool> PutVoiceAsync(string voiceId, byte[] wavBytes)
    {
        var baseUrl = variablesService.GetVariable("CLACKS_URL")?.Item?.ToString()?.TrimEnd('/');
        if (string.IsNullOrEmpty(baseUrl))
        {
            logger.LogWarning("CLACKS_URL not configured — voice push skipped");
            return false;
        }

        try
        {
            using var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);
            using var content = new ByteArrayContent(wavBytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
            var response = await client.PutAsync($"{baseUrl}/voice/{Uri.EscapeDataString(voiceId)}", content);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning($"clacks PUT /voice/{voiceId} returned {(int)response.StatusCode}");
                return false;
            }

            return true;
        }
        catch (Exception exception)
        {
            logger.LogError($"clacks PUT /voice/{voiceId} failed", exception);
            return false;
        }
    }

    public async Task<ClacksEmoteResult> EmoteAsync(string voiceId, string text, string emoText, double emoAlpha)
    {
        var baseUrl = variablesService.GetVariable("CLACKS_URL")?.Item?.ToString()?.TrimEnd('/');
        if (string.IsNullOrEmpty(baseUrl))
        {
            logger.LogWarning("CLACKS_URL not configured — emote skipped");
            return ClacksEmoteResult.Failed();
        }

        try
        {
            using var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(180); // IndexTTS-2 can be ~74s/line on the mac fallback
            var response = await client.PostAsJsonAsync(
                $"{baseUrl}/emote",
                new
                {
                    role = "emotion",
                    voiceId,
                    text,
                    emoText,
                    emoAlpha
                },
                NpcBrainJson.Options
            );
            if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
            {
                return ClacksEmoteResult.NodeDown(); // no emotion node up — requeue
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning($"clacks /emote returned {(int)response.StatusCode} for voice '{voiceId}'");
                return ClacksEmoteResult.Failed();
            }

            var speak = await response.Content.ReadFromJsonAsync<ClacksSpeakResult>(NpcBrainJson.Options);
            if (speak is null || string.IsNullOrEmpty(speak.AudioBase64))
            {
                return ClacksEmoteResult.Failed();
            }

            return new ClacksEmoteResult
            {
                Status = EmoteStatus.Ok,
                WavBytes = Convert.FromBase64String(speak.AudioBase64),
                DurationMs = speak.DurationMs
            };
        }
        catch (Exception exception)
        {
            logger.LogError($"clacks /emote call failed for voice '{voiceId}'", exception);
            return ClacksEmoteResult.Failed();
        }
    }
}
