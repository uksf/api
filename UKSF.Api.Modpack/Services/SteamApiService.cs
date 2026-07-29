using System.Text.Json;
using UKSF.Api.Core;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Modpack.Models;

namespace UKSF.Api.Modpack.Services;

public interface ISteamApiService
{
    Task<WorkshopModInfo> GetWorkshopModInfo(string workshopModId);
    Task<Dictionary<string, WorkshopModInfo>> GetWorkshopModInfos(IReadOnlyCollection<string> workshopModIds);
}

public class SteamApiService(IHttpClientFactory httpClientFactory, IUksfLogger logger) : ISteamApiService
{
    public async Task<WorkshopModInfo> GetWorkshopModInfo(string workshopModId)
    {
        var infos = await GetWorkshopModInfos([workshopModId]);
        if (!infos.TryGetValue(workshopModId, out var info))
        {
            throw new BadRequestException($"Workshop mod with Steam ID {workshopModId} not found");
        }

        return info;
    }

    public async Task<Dictionary<string, WorkshopModInfo>> GetWorkshopModInfos(IReadOnlyCollection<string> workshopModIds)
    {
        if (workshopModIds.Count == 0)
        {
            return [];
        }

        using var client = httpClientFactory.CreateClient("Steam");

        var formData = new Dictionary<string, string> { ["itemcount"] = workshopModIds.Count.ToString() };
        foreach (var (workshopModId, index) in workshopModIds.Select((id, index) => (id, index)))
        {
            formData[$"publishedfileids[{index}]"] = workshopModId;
        }

        var response = await client.PostAsync(
            "https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/",
            new FormUrlEncodedContent(formData)
        );
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("response", out var responseElement) ||
                !responseElement.TryGetProperty("publishedfiledetails", out var detailsArray))
            {
                throw new Exception($"Failed getting info for workshop mods {string.Join(", ", workshopModIds)}");
            }

            return detailsArray.EnumerateArray().Select(ReadWorkshopModInfo).Where(x => x is not null).ToDictionary(x => x!.Value.Key, x => x!.Value.Value);
        }
        catch (JsonException exception)
        {
            logger.LogError($"Failed to parse JSON response for workshop mods {string.Join(", ", workshopModIds)}", exception);
            throw;
        }
    }

    private static KeyValuePair<string, WorkshopModInfo>? ReadWorkshopModInfo(JsonElement item)
    {
        if (!item.TryGetProperty("publishedfileid", out var idElement) ||
            (item.TryGetProperty("result", out var resultElement) && resultElement.GetInt32() != 1) ||
            !item.TryGetProperty("title", out var titleElement) ||
            !item.TryGetProperty("time_updated", out var updatedElement) ||
            !updatedElement.TryGetInt64(out var unixTimestamp))
        {
            return null;
        }

        return new KeyValuePair<string, WorkshopModInfo>(
            idElement.GetString()!,
            new WorkshopModInfo
            {
                Name = titleElement.GetString() ?? "NO NAME FOUND", UpdatedDate = DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).UtcDateTime
            }
        );
    }
}
