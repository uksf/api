using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using UKSF.Api.ArmaServer.Npc.Models;
using UKSF.Api.ArmaServer.Npc.Observability;

namespace UKSF.Api.ArmaServer.Npc.Services;

// Chat half of the clacks client. The body is serialised once so the exact request and raw
// response can be kept in the NPC trace.
public partial class ClacksClient
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

        var watch = Stopwatch.StartNew();
        var request = JsonSerializer.Serialize(
            new
            {
                model = ClacksCandidates.NpcChatModel,
                effort = ClacksCandidates.NpcChatEffort,
                fallbacks = ClacksCandidates.NpcChatFallbacks,
                // No service_tier: "fast" hangs the codex path (70s+ stall vs 1s without).
                messages = new[] { new { role = "system", content = system }, new { role = "user", content = user } },
                json,
                max_tokens = maxTokens,
                temperature,
                meta // null is omitted by WhenWritingNull; carries per-call context for the mesh dashboard
            },
            NpcBrainJson.Options
        );
        string raw = null;
        var status = "ok";
        try
        {
            using var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(60); // a fallback may need a cold model load
            var response = await client.PostAsync($"{baseUrl}/v1/chat/completions", new StringContent(request, Encoding.UTF8, "application/json"));
            raw = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                status = $"http {(int)response.StatusCode}";
                logger.LogWarning($"clacks /v1/chat/completions returned {(int)response.StatusCode} for role '{role}'");
                return null;
            }

            var v1 = JsonSerializer.Deserialize<V1ChatResponse>(raw, NpcBrainJson.Options);
            if (v1 is null)
            {
                status = "empty";
                return null;
            }

            return new ClacksChatResult
            {
                Text = v1.Choices?.Count > 0 ? v1.Choices[0].Message?.Content ?? string.Empty : string.Empty,
                Model = v1.Model ?? string.Empty,
                Node = v1.Clacks?.Node ?? string.Empty,
                Ms = v1.Clacks?.Ms ?? 0
            };
        }
        catch (Exception exception)
        {
            status = $"error {exception.GetType().Name}";
            logger.LogError($"clacks /v1/chat/completions call failed for role '{role}'", exception);
            return null;
        }
        finally
        {
            NpcTraceScope.Add(new NpcModelCall("chat", request, raw, watch.ElapsedMilliseconds, status));
        }
    }
}
