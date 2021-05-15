using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UKSF.Api.Admin.Context;
using UKSF.Api.Admin.Extensions;
using UKSF.Api.Admin.Services;
using UKSF.Api.Integrations.Instagram.Models;
using UKSF.Api.Shared.Events;

namespace UKSF.Api.Integrations.Instagram.Services
{
    public interface IInstagramService
    {
        Task RefreshAccessToken();
        Task CacheInstagramImages();
        IEnumerable<InstagramImage> GetImages();
    }

    public class InstagramService : IInstagramService
    {
        private readonly ILogger _logger;
        private readonly IVariablesContext _variablesContext;
        private readonly IVariablesService _variablesService;
        private List<InstagramImage> _images = new();

        public InstagramService(IVariablesContext variablesContext, IVariablesService variablesService, ILogger logger)
        {
            _variablesContext = variablesContext;
            _variablesService = variablesService;
            _logger = logger;
        }

        public async Task RefreshAccessToken()
        {
            try
            {
                string accessToken = _variablesService.GetVariable("INSTAGRAM_ACCESS_TOKEN").AsString();

                using HttpClient client = new();
                HttpResponseMessage response = await client.GetAsync($"https://graph.instagram.com/refresh_access_token?access_token={accessToken}&grant_type=ig_refresh_token");
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Failed to get instagram access token, error: {response}");
                    return;
                }

                string contentString = await response.Content.ReadAsStringAsync();
                _logger.LogInfo($"Instagram response: {contentString}");
                string newAccessToken = JObject.Parse(contentString)["access_token"]?.ToString();

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
                string userId = _variablesService.GetVariable("INSTAGRAM_USER_ID").AsString();
                string accessToken = _variablesService.GetVariable("INSTAGRAM_ACCESS_TOKEN").AsString();

                using HttpClient client = new();
                HttpResponseMessage response = await client.GetAsync($"https://graph.instagram.com/{userId}/media?access_token={accessToken}&fields=id,timestamp,media_type,media_url,permalink");
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Failed to get instagram images, error: {response}");
                    return;
                }

                string contentString = await response.Content.ReadAsStringAsync();
                JObject contentObject = JObject.Parse(contentString);
                List<InstagramImage> allMedia = JsonConvert.DeserializeObject<List<InstagramImage>>(contentObject["data"]?.ToString() ?? "");
                allMedia = allMedia.OrderByDescending(x => x.Timestamp).ToList();

                if (allMedia.Count == 0)
                {
                    _logger.LogWarning($"Instagram response contains no images: {contentObject}");
                    return;
                }

                if (_images.Count > 0 && allMedia.First().Id == _images.First().Id)
                {
                    return;
                }

                List<InstagramImage> newImages = allMedia.Where(x => x.MediaType == "IMAGE").ToList();

                // // Handle carousel images
                // foreach ((InstagramImage value, int index) instagramImage in newImages.Select((value, index) => ( value, index ))) {
                //     if (instagramImage.value.mediaType == "CAROUSEL_ALBUM ") {
                //
                //     }
                // }

                _images = newImages.Take(12).ToList();

                foreach (InstagramImage instagramImage in _images)
                {
                    instagramImage.Base64 = await GetBase64(instagramImage);
                }
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
            byte[] bytes = await client.GetByteArrayAsync(image.MediaUrl);
            return "data:image/jpeg;base64," + Convert.ToBase64String(bytes);
        }
    }
}
