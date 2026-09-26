using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using UKSF.Api.ArmaServer.Npc.Models;
using UKSF.Api.Core;
using UKSF.Api.Core.Services;

namespace UKSF.Api.ArmaServer.Npc.Services;

/// One typed Jev question. Noul answers a yes/no probability; Choice picks one criteria key.
public class JevQuestion
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "noul";

    [JsonPropertyName("instructions")]
    public string Instructions { get; init; } = string.Empty;

    [JsonPropertyName("criteria")]
    public Dictionary<string, string> Criteria { get; init; }

    public static JevQuestion Noul(string instructions) => new() { Type = "noul", Instructions = instructions };

    public static JevQuestion Choice(string instructions, Dictionary<string, string> criteria) =>
        new() { Type = "choice", Instructions = instructions, Criteria = criteria };
}

public class JevAnswer
{
    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("noul")]
    public double? Noul { get; set; }

    [JsonPropertyName("choice")]
    public string Choice { get; set; }

    [JsonPropertyName("confidence")]
    public double? Confidence { get; set; }
}

public class JevResult
{
    public Dictionary<string, JevAnswer> Answers { get; init; } = [];
    public long Ms { get; init; }

    /// Probability of yes; a missing or malformed answer reads as 0.
    public double P(string key) => Answers.TryGetValue(key, out var a) && a?.Noul is { } p ? p : 0;

    /// The chosen key, or null when the answer is missing.
    public string Pick(string key) => Answers.TryGetValue(key, out var a) ? a?.Choice : null;
}

public interface INpcJevClient
{
    /// Null when clacks is not configured, unreachable, or answers without every question.
    Task<JevResult> AskAsync(string state, Dictionary<string, JevQuestion> questions, string npcId);
}

/// Jev through the clacks mesh: POST /v1/systemone, which forwards to OpenRouter and logs the spend.
public class NpcJevClient(IHttpClientFactory httpClientFactory, IVariablesService variablesService, IUksfLogger logger) : INpcJevClient
{
    public async Task<JevResult> AskAsync(string state, Dictionary<string, JevQuestion> questions, string npcId)
    {
        var baseUrl = variablesService.GetVariable("CLACKS_URL")?.Item?.ToString()?.TrimEnd('/');
        if (string.IsNullOrEmpty(baseUrl)) return null;

        var watch = Stopwatch.StartNew();
        try
        {
            using var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            var response = await client.PostAsJsonAsync($"{baseUrl}/v1/systemone", new { model = "jev", state, questions }, NpcBrainJson.Options);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning($"clacks /v1/systemone returned {(int)response.StatusCode} for npc '{npcId}'");
                return null;
            }

            var body = await response.Content.ReadFromJsonAsync<JevBody>(NpcBrainJson.Options);
            var answers = body?.Answers ?? [];
            foreach (var key in questions.Keys)
            {
                if (!answers.ContainsKey(key))
                {
                    logger.LogWarning($"jev answered without '{key}' for npc '{npcId}'");
                    return null;
                }
            }

            return new JevResult { Answers = answers, Ms = watch.ElapsedMilliseconds };
        }
        catch (Exception exception)
        {
            logger.LogError($"clacks /v1/systemone call failed for npc '{npcId}'", exception);
            return null;
        }
    }

    private sealed class JevBody
    {
        [JsonPropertyName("answers")]
        public Dictionary<string, JevAnswer> Answers { get; set; }
    }
}
