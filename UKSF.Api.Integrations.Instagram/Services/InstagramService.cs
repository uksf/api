using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
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
    Task<List<InstagramImage>> GetImagesFromLocalCache();
}

public class InstagramService(IVariablesContext variablesContext, IVariablesService variablesService, IHttpClientFactory httpClientFactory, IUksfLogger logger)
    : IInstagramService
{
    private volatile List<InstagramImage> _images = new();

    public async Task RefreshAccessToken()
    {
        try
        {
            var accessToken = variablesService.GetVariable("INSTAGRAM_ACCESS_TOKEN").AsString();

            var client = httpClientFactory.CreateClient();
            var response = await client.GetAsync($"https://graph.instagram.com/refresh_access_token?access_token={accessToken}&grant_type=ig_refresh_token");
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError($"Failed to get instagram access token, error: {response}");
                return;
            }

            var contentString = await response.Content.ReadAsStringAsync();
            logger.LogInfo($"Instagram response: {contentString}");
            var content = JsonNode.Parse(contentString);
            var newAccessToken = content.GetValueFromObject("access_token");

            if (string.IsNullOrEmpty(newAccessToken))
            {
                logger.LogError($"Failed to get instagram access token from response: {contentString}");
                return;
            }

            await variablesContext.Update("INSTAGRAM_ACCESS_TOKEN", newAccessToken);
            logger.LogInfo("Updated Instagram access token");
        }
        catch (Exception exception)
        {
            logger.LogError(exception);
        }
    }

    public async Task CacheInstagramImages()
    {
        try
        {
            var userId = variablesService.GetVariable("INSTAGRAM_USER_ID").AsString();
            var accessToken = variablesService.GetVariable("INSTAGRAM_ACCESS_TOKEN").AsString();

            var client = httpClientFactory.CreateClient();
            var response = await client.GetAsync(
                $"https://graph.instagram.com/{userId}/media?access_token={accessToken}&fields=id,timestamp,media_type,media_url,permalink"
            );
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError($"Failed to get instagram images, error: {response}");
                return;
            }

            var contentString = await response.Content.ReadAsStringAsync();
            var content = JsonNode.Parse(contentString);
            var allMedia = JsonSerializer.Deserialize<List<InstagramImage>>(content.GetValueFromObject("data") ?? "", DefaultJsonSerializerOptions.Options) ??
                           new List<InstagramImage>();

            if (allMedia.Count == 0)
            {
                logger.LogWarning($"Instagram response contains no images: {content}");
                return;
            }

            var currentImages = _images;
            var newImages = allMedia.Where(x => x.MediaType == "IMAGE" && currentImages.All(y => x.Id != y.Id)).ToList();
            var updatedImages = new List<InstagramImage>(currentImages);
            foreach (var image in newImages)
            {
                var imageData = await GetImageData(image);
                if (!imageData.IsNullOrEmpty())
                {
                    image.Base64 = imageData;
                    updatedImages.Add(image);
                }
            }

            _images = updatedImages.Where(x => !x.Base64.IsNullOrEmpty()).ToList();
        }
        catch (Exception exception)
        {
            logger.LogError(exception);
        }
    }

    public IEnumerable<InstagramImage> GetImages()
    {
        return _images.Shuffle().Take(12);
    }

    public async Task<List<InstagramImage>> GetImagesFromLocalCache()
    {
        var folder = variablesService.GetVariable("INSTAGRAM_LOCAL_CACHE").AsString();
        var imageFiles = Directory.EnumerateFiles(folder).Shuffle().Take(12);
        var images = new ConcurrentBag<InstagramImage>();
        var tasks = imageFiles.Select(async x =>
            {
                var imageData = await GetImageDataFromLocalCache(x);
                images.Add(new InstagramImage { Base64 = imageData });
            }
        );
        await Task.WhenAll(tasks);

        return images.ToList();
    }

    private async Task<string> GetImageDataFromLocalCache(string imagePath)
    {
        return "data:image/jpeg;base64," + Convert.ToBase64String(await File.ReadAllBytesAsync(imagePath));
    }

    private async Task<string> GetImageData(InstagramImage image)
    {
        try
        {
            var client = httpClientFactory.CreateClient();
            var bytes = await client.GetByteArrayAsync(image.MediaUrl);
            return "data:image/jpeg;base64," + Convert.ToBase64String(bytes);
        }
        catch (Exception exception)
        {
            logger.LogError("Failed to get image data", exception);
        }

        return string.Empty;
    }
}
