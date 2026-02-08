using System.Text.Json;
using UKSF.Api.Core;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Modpack.Models;

namespace UKSF.Api.Modpack.Services;

public interface ISteamApiService
{
    Task<WorkshopModInfo> GetWorkshopModInfo(string workshopModId);
}

public class SteamApiService(IHttpClientFactory httpClientFactory, IUksfLogger logger) : ISteamApiService
{
    public async Task<WorkshopModInfo> GetWorkshopModInfo(string workshopModId)
    {
        using var client = httpClientFactory.CreateClient("Steam");

        var formData = new Dictionary<string, string> { ["itemcount"] = "1", ["publishedfileids[0]"] = workshopModId };
        var content = new FormUrlEncodedContent(formData);

        try
        {
            var response = await client.PostAsync("https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/", content);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("response", out var resp) &&
                resp.TryGetProperty("publishedfiledetails", out var detailsArray) &&
                detailsArray.GetArrayLength() > 0)
            {
                var item = detailsArray[0];

                if (item.TryGetProperty("result", out var resultElement) && resultElement.GetInt32() != 1)
                {
                    throw new BadRequestException($"Workshop mod with Steam ID {workshopModId} not found");
                }

                if (item.TryGetProperty("title", out var titleElement) &&
                    item.TryGetProperty("time_updated", out var tsElement) &&
                    tsElement.TryGetInt64(out var unixTimestamp))
                {
                    return new WorkshopModInfo
                    {
                        Name = titleElement.GetString() ?? "NO NAME FOUND", UpdatedDate = DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).UtcDateTime
                    };
                }
            }

            throw new Exception($"Failed getting info for workshop mod id {workshopModId}");
        }
        catch (JsonException exception)
        {
            logger.LogError($"Failed to parse JSON response for workshop mod id {workshopModId}", exception);
            throw;
        }
    }
}
