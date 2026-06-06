using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using UKSF.Api.ArmaServer.Npc.Models;
using UKSF.Api.Core;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Services;

namespace UKSF.Api.ArmaServer.Npc.Services;

public interface INpcBrainClient
{
    Task<RespondResult> RespondAsync(RespondRequest request);
    Task<PrerenderResult> PrerenderAsync(PrerenderRequest request);
}

public class NpcBrainClient(IHttpClientFactory httpClientFactory, IVariablesService variablesService, IUksfLogger logger) : INpcBrainClient
{
    public Task<RespondResult> RespondAsync(RespondRequest request) => PostAsync<RespondRequest, RespondResult>("npc/respond", request);
    public Task<PrerenderResult> PrerenderAsync(PrerenderRequest request) => PostAsync<PrerenderRequest, PrerenderResult>("npc/prerender", request);

    private async Task<TResult> PostAsync<TBody, TResult>(string path, TBody body) where TResult : class
    {
        var baseUrl = variablesService.GetVariable("NPC_BRAIN_URL").AsString().TrimEnd('/');
        if (string.IsNullOrEmpty(baseUrl))
        {
            logger.LogWarning("NPC_BRAIN_URL not configured — NPC brain call skipped");
            return null;
        }

        var url = $"{baseUrl}/{path}";
        try
        {
            using var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);
            var response = await client.PostAsJsonAsync(url, body, NpcBrainJson.Options);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning($"NPC brain {path} returned {(int)response.StatusCode}");
                return null;
            }

            return await response.Content.ReadFromJsonAsync<TResult>(NpcBrainJson.Options);
        }
        catch (Exception exception)
        {
            logger.LogError($"NPC brain call to {url} failed", exception);
            return null;
        }
    }
}
