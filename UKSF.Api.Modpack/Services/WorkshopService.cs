using System.Text.Json;
using UKSF.Api.Core;

namespace UKSF.Api.Modpack.Services;

public interface IWorkshopService
{
    Task<DateTime> GetWorkshopModUpdatedDate(string workshopModId);
}

public class WorkshopService(IUksfLogger logger) : IWorkshopService
{
    public async Task<DateTime> GetWorkshopModUpdatedDate(string workshopModId)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add(
            "User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0 Safari/537.36"
        );

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

                if (item.TryGetProperty("time_updated", out var tsElement) && tsElement.TryGetInt64(out var unixTimestamp))
                {
                    return DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).UtcDateTime;
                }

                if (item.TryGetProperty("result", out _))
                {
                    throw new Exception($"Workshop mod with id {workshopModId} not found");
                }
            }

            throw new Exception($"Failed getting updated date for workshop mod id {workshopModId}");
        }
        catch (JsonException exception)
        {
            logger.LogError($"Failed to parse JSON response for workshop mod id {workshopModId}", exception);
            throw;
        }
    }
}
