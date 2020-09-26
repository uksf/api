using System;
using System.Net.Http;
using System.Threading.Tasks;
using UKSF.Api.Models.Integrations;

namespace UKSF.Common {
    public static class InstagramExtensions {
        public static async Task<string> AsBase64(this InstagramImage image) {
            using HttpClient client = new HttpClient();
            byte[] bytes = await client.GetByteArrayAsync(image.mediaUrl);
            return "data:image/jpeg;base64," + Convert.ToBase64String(bytes);
        }
    }
}
