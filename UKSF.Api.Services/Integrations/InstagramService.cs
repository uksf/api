using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UKSF.Api.Interfaces.Integrations;
using UKSF.Api.Models.Integrations;
using UKSF.Api.Services.Admin;
using UKSF.Api.Services.Message;

namespace UKSF.Api.Services.Integrations {
    public class InstagramService : IInstagramService {
        private List<InstagramImage> images = new List<InstagramImage>();

        public async Task RefreshAccessToken() {
            try {
                string accessToken = VariablesWrapper.VariablesDataService().GetSingle("INSTAGRAM_ACCESS_TOKEN").AsString();

                using HttpClient client = new HttpClient();
                HttpResponseMessage response = await client.GetAsync($"https://graph.instagram.com/refresh_access_token?access_token={accessToken}&grant_type=ig_exchange_token");
                if (!response.IsSuccessStatusCode) {
                    LogWrapper.Log($"Failed to get instagram access token, error: {response}");
                    return;
                }

                string contentString = await response.Content.ReadAsStringAsync();
                LogWrapper.Log($"Instagram response: {contentString}");
                string newAccessToken = JObject.Parse(contentString)["access_token"]?.ToString();

                if (string.IsNullOrEmpty(newAccessToken)) {
                    LogWrapper.Log($"Failed to get instagram access token from response: {contentString}");
                    return;
                }

                await VariablesWrapper.VariablesDataService().Update("INSTAGRAM_ACCESS_TOKEN", newAccessToken);
                LogWrapper.Log("Updated Instagram access token");
            } catch (Exception exception) {
                LogWrapper.Log(exception);
            }
        }

        public async Task CacheInstagramImages() {
            try {
                string userId = VariablesWrapper.VariablesDataService().GetSingle("INSTAGRAM_USER_ID").AsString();
                string accessToken = VariablesWrapper.VariablesDataService().GetSingle("INSTAGRAM_ACCESS_TOKEN").AsString();

                using HttpClient client = new HttpClient();
                HttpResponseMessage response = await client.GetAsync($"https://graph.instagram.com/{userId}/media?access_token={accessToken}&fields=id,timestamp,media_type,media_url,permalink");
                if (!response.IsSuccessStatusCode) {
                    LogWrapper.Log($"Failed to get instagram images, error: {response}");
                    return;
                }

                string contentString = await response.Content.ReadAsStringAsync();
                JObject contentObject = JObject.Parse(contentString);
                List<InstagramImage> allNewImages = JsonConvert.DeserializeObject<List<InstagramImage>>(contentObject["data"]?.ToString() ?? "");

                if (allNewImages == null || allNewImages.Count == 0) {
                    LogWrapper.Log($"Instagram response contains no images: {contentObject}");
                    return;
                }

                if (images.Count > 0 && allNewImages.First().id == images.First().id) {
                    // Most recent image is the same, therefore all images are already present
                    return;
                }

                // Isolate new images
                List<InstagramImage> newImages = allNewImages.Where(x => x.mediaType == "IMAGE" && images.All(y => x.id != y.id)).ToList();

                // // Handle carousel images
                // foreach ((InstagramImage value, int index) instagramImage in newImages.Select((value, index) => ( value, index ))) {
                //     if (instagramImage.value.mediaType == "CAROUSEL_ALBUM ") {
                //
                //     }
                // }

                // Insert new images at start of list, and take only 12
                images.InsertRange(0, newImages);
                images = images.Take(12).ToList();
            } catch (Exception exception) {
                LogWrapper.Log(exception);
            }
        }

        public List<InstagramImage> GetImages() => images;
    }
}
