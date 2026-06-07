using System;
using System.Net.Http;
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

public interface IClacksClient
{
    Task<ClacksChatResult> ChatAsync(string role, string system, string user, bool json, int maxTokens, double temperature);
    Task<ClacksSpeakResult> SpeakAsync(string role, string text, string voiceId);
}

// HTTP client for the local clacks daemon (the LLM mesh). The daemon owns all model routing/fallback;
// this client just asks for a role and reads text back.
public class ClacksClient(IHttpClientFactory httpClientFactory, IVariablesService variablesService, IUksfLogger logger) : IClacksClient
{
    public async Task<ClacksChatResult> ChatAsync(string role, string system, string user, bool json, int maxTokens, double temperature)
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
                    temperature
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
}
