using System.Text.Json.Serialization;

namespace UKSF.Api.Integrations.Instagram.Models;

public class InstagramImage
{
    public string Base64 { get; set; }
    public string Id { get; set; }

    [JsonPropertyName("media_type")]
    public string MediaType { get; set; }

    [JsonPropertyName("media_url")]
    public string MediaUrl { get; set; }

    public string Permalink { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}
