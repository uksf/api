using System.Text.Json;
using System.Text.Json.Nodes;
using UKSF.Api.Integrations.Instagram.Models;
using UKSF.Api.Shared;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Extensions;
using UKSF.Api.Shared.Services;

namespace UKSF.Api.Integrations.Instagram.Services;

public interface IInstagramService
{
    Task RefreshAccessToken();
    Task CacheInstagramImages();
    IEnumerable<InstagramImage> GetImages();
}

public class InstagramService : IInstagramService
{
    private readonly IUksfLogger _logger;
    private readonly IVariablesContext _variablesContext;
    private readonly IVariablesService _variablesService;
    private List<InstagramImage> _images = new();

    public InstagramService(IVariablesContext variablesContext, IVariablesService variablesService, IUksfLogger logger)
    {
        _variablesContext = variablesContext;
        _variablesService = variablesService;
        _logger = logger;
    }

    public async Task RefreshAccessToken()
    {
        try
        {
            var accessToken = _variablesService.GetVariable("INSTAGRAM_ACCESS_TOKEN").AsString();

            using HttpClient client = new();
            var response = await client.GetAsync($"https://graph.instagram.com/refresh_access_token?access_token={accessToken}&grant_type=ig_refresh_token");
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"Failed to get instagram access token, error: {response}");
                return;
            }

            var contentString = await response.Content.ReadAsStringAsync();
            _logger.LogInfo($"Instagram response: {contentString}");
            var content = JsonNode.Parse(contentString);
            var newAccessToken = content.GetValueFromObject("access_token");

            if (string.IsNullOrEmpty(newAccessToken))
            {
                _logger.LogError($"Failed to get instagram access token from response: {contentString}");
                return;
            }

            await _variablesContext.Update("INSTAGRAM_ACCESS_TOKEN", newAccessToken);
            _logger.LogInfo("Updated Instagram access token");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception);
        }
    }

    public async Task CacheInstagramImages()
    {
        try
        {
            var userId = _variablesService.GetVariable("INSTAGRAM_USER_ID").AsString();
            var accessToken = _variablesService.GetVariable("INSTAGRAM_ACCESS_TOKEN").AsString();

            using HttpClient client = new();
            var response = await client.GetAsync(
                $"https://graph.instagram.com/{userId}/media?access_token={accessToken}&fields=id,timestamp,media_type,media_url,permalink"
            );
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"Failed to get instagram images, error: {response}");
                return;
            }

            var contentString = await response.Content.ReadAsStringAsync();
            var content = JsonNode.Parse(contentString);
            var allMedia = JsonSerializer.Deserialize<List<InstagramImage>>(content.GetValueFromObject("data") ?? "", DefaultJsonSerializerOptions.Options) ??
                           new List<InstagramImage>();
            allMedia = allMedia.OrderByDescending(x => x.Timestamp).ToList();

            if (allMedia.Count == 0)
            {
                _logger.LogWarning($"Instagram response contains no images: {content}");
                return;
            }

            if (_images.Count > 0 && allMedia.First().Id == _images.First().Id)
            {
                return;
            }

            var newImages = allMedia.Where(x => x.MediaType == "IMAGE").ToList();

            // // Handle carousel images
            // foreach ((InstagramImage value, int index) instagramImage in newImages.Select((value, index) => ( value, index ))) {
            //     if (instagramImage.value.mediaType == "CAROUSEL_ALBUM ") {
            //
            //     }
            // }

            _images = newImages.Take(12).ToList();

            await Task.WhenAll(_images.Select(x => Task.Run(async () => x.Base64 = await GetBase64(x))));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception);
        }
    }

    public IEnumerable<InstagramImage> GetImages()
    {
        return _images;
    }

    private static async Task<string> GetBase64(InstagramImage image)
    {
        using HttpClient client = new();
        var bytes = await client.GetByteArrayAsync(image.MediaUrl);
        return "data:image/jpeg;base64," + Convert.ToBase64String(bytes);
    }
}

