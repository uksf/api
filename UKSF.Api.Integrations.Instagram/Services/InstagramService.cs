using System.Text.Json;
using System.Text.Json.Nodes;
using MoreLinq;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Services;
using UKSF.Api.Integrations.Instagram.Models;

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

            if (allMedia.Count == 0)
            {
                _logger.LogWarning($"Instagram response contains no images: {content}");
                return;
            }

            var newImages = allMedia.Where(x => x.MediaType == "IMAGE" && _images.All(y => x.Id != y.Id)).ToList();
            foreach (var image in newImages)
            {
                var imageData = await GetImageData(image);
                if (!imageData.IsNullOrEmpty())
                {
                    image.Base64 = imageData;
                    _images.Add(image);
                }
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception);
        }
        finally
        {
            _images = _images.Where(x => !x.Base64.IsNullOrEmpty()).ToList();
        }
    }

    public IEnumerable<InstagramImage> GetImages()
    {
        return _images.Shuffle().Take(12);
    }

    private async Task<string> GetImageData(InstagramImage image)
    {
        try
        {
            using HttpClient client = new();
            var bytes = await client.GetByteArrayAsync(image.MediaUrl);
            return "data:image/jpeg;base64," + Convert.ToBase64String(bytes);
        }
        catch (Exception exception)
        {
            _logger.LogError("Failed to get image data", exception);
        }

        return string.Empty;
    }
}
