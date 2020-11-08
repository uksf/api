using System;
using Newtonsoft.Json;

namespace UKSF.Api.Integration.Instagram.Models {
    public class InstagramImage {
        public string id;

        [JsonProperty("media_type")] public string mediaType;
        [JsonProperty("media_url")] public string mediaUrl;

        public string permalink;
        public DateTime timestamp;
        public string base64;
    }
}
