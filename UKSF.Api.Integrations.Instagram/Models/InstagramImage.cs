using System;
using Newtonsoft.Json;

namespace UKSF.Api.Integrations.Instagram.Models {
    public class InstagramImage {
        public string Base64;
        public string Id;

        [JsonProperty("media_type")] public string MediaType;
        [JsonProperty("media_url")] public string MediaUrl;

        public string Permalink;
        public DateTime Timestamp;
    }
}
