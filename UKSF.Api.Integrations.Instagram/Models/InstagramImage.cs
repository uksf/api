using System;
using Newtonsoft.Json;

namespace UKSF.Api.Integrations.Instagram.Models {
    public class InstagramImage {
        public string Base64 { get; set; }
        public string Id { get; set; }

        [JsonProperty("media_type")] public string MediaType { get; set; }
        [JsonProperty("media_url")] public string MediaUrl { get; set; }

        public string Permalink { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
