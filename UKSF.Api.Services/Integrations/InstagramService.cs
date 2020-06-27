using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UKSF.Api.Interfaces.Integrations;
using UKSF.Api.Models.Integrations;
using UKSF.Api.Services.Message;

namespace UKSF.Api.Services.Integrations {
    public class InstagramService : IInstagramService {
        private List<InstagramImage> images = new List<InstagramImage>();

        public async Task CacheInstagramImages() {
            LogWrapper.Log("Running instagram get");
            using HttpClient client = new HttpClient();
            HttpResponseMessage response = await client.GetAsync("https://www.instagram.com/uksfmilsim/?__a=1");
            if (!response.IsSuccessStatusCode) {
                LogWrapper.Log($"Failed to get instagram images, error: {response}");
                return;
            }

            string contentString = await response.Content.ReadAsStringAsync();
            JObject contentObject = JObject.Parse(contentString);
            JToken imagesToken = contentObject["graphql"]?["user"]?["edge_owner_to_timeline_media"]?["edges"];

            if (imagesToken == null) {
                LogWrapper.Log($"Instagram response contains no images: {contentObject}");
                return;
            }

            List<InstagramImage> allNewImages = imagesToken.Select(x => new InstagramImage { shortcode = x["node"]?["shortcode"]?.ToString(), url = x["node"]?["display_url"]?.ToString() }).ToList();
            if (images.Count > 0 && allNewImages.First().shortcode == images.First().shortcode) {
                // Most recent image is the same, therefore all images are already present
                LogWrapper.Log("No instagram images processed");
                return;
            }

            // Isolate new shortcodes, insert at start of list, and take only 12
            IEnumerable<InstagramImage> newImages = allNewImages.Where(x => images.All(y => x.shortcode != y.shortcode));
            images.InsertRange(0, newImages);
            images = images.Take(12).ToList();
            LogWrapper.Log($"Insta images: {images}");
        }

        public List<InstagramImage> GetImages() => images;
    }
}
