using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UKSF.Api.Admin.Extensions;
using UKSF.Api.Admin.Services;
using UKSF.Api.Base.Events;
using UKSF.Api.Interfaces.Integrations;
using UKSF.Api.Models.Integrations;

namespace UKSF.Api.Services.Integrations {
    public class InstagramService : IInstagramService {
        private readonly IVariablesService variablesService;
        private readonly ILogger logger;
        private List<InstagramImage> images = new List<InstagramImage>();

        public InstagramService(IVariablesService variablesService, ILogger logger) {
            this.variablesService = variablesService;
            this.logger = logger;
        }

        public async Task RefreshAccessToken() {
            try {
                string accessToken = variablesService.GetVariable("INSTAGRAM_ACCESS_TOKEN").AsString();

                using HttpClient client = new HttpClient();
                HttpResponseMessage response = await client.GetAsync($"https://graph.instagram.com/refresh_access_token?access_token={accessToken}&grant_type=ig_refresh_token");
                if (!response.IsSuccessStatusCode) {
                    logger.LogError($"Failed to get instagram access token, error: {response}");
                    return;
                }

                string contentString = await response.Content.ReadAsStringAsync();
                logger.LogInfo($"Instagram response: {contentString}");
                string newAccessToken = JObject.Parse(contentString)["access_token"]?.ToString();

                if (string.IsNullOrEmpty(newAccessToken)) {
                    logger.LogError($"Failed to get instagram access token from response: {contentString}");
                    return;
                }

                await variablesService.Data.Update("INSTAGRAM_ACCESS_TOKEN", newAccessToken);
                logger.LogInfo("Updated Instagram access token");
            } catch (Exception exception) {
                logger.LogError(exception);
            }
        }

        public async Task CacheInstagramImages() {
            try {
                string userId = variablesService.GetVariable("INSTAGRAM_USER_ID").AsString();
                string accessToken = variablesService.GetVariable("INSTAGRAM_ACCESS_TOKEN").AsString();

                using HttpClient client = new HttpClient();
                HttpResponseMessage response = await client.GetAsync($"https://graph.instagram.com/{userId}/media?access_token={accessToken}&fields=id,timestamp,media_type,media_url,permalink");
                if (!response.IsSuccessStatusCode) {
                    logger.LogError($"Failed to get instagram images, error: {response}");
                    return;
                }

                string contentString = await response.Content.ReadAsStringAsync();
                JObject contentObject = JObject.Parse(contentString);
                List<InstagramImage> allMedia = JsonConvert.DeserializeObject<List<InstagramImage>>(contentObject["data"]?.ToString() ?? "");
                allMedia = allMedia.OrderByDescending(x => x.timestamp).ToList();

                if (allMedia.Count == 0) {
                    logger.LogWarning($"Instagram response contains no images: {contentObject}");
                    return;
                }

                if (images.Count > 0 && allMedia.First().id == images.First().id) {
                    return;
                }

                List<InstagramImage> newImages = allMedia.Where(x => x.mediaType == "IMAGE").ToList();

                // // Handle carousel images
                // foreach ((InstagramImage value, int index) instagramImage in newImages.Select((value, index) => ( value, index ))) {
                //     if (instagramImage.value.mediaType == "CAROUSEL_ALBUM ") {
                //
                //     }
                // }

                images = newImages.Take(12).ToList();

                foreach (InstagramImage instagramImage in images) {
                    instagramImage.base64 = await GetBase64(instagramImage);
                }
            } catch (Exception exception) {
                logger.LogError(exception);
            }
        }

        public IEnumerable<InstagramImage> GetImages() => images;

        private static async Task<string> GetBase64(InstagramImage image) {
            using HttpClient client = new HttpClient();
            byte[] bytes = await client.GetByteArrayAsync(image.mediaUrl);
            return "data:image/jpeg;base64," + Convert.ToBase64String(bytes);
        }
    }
}
