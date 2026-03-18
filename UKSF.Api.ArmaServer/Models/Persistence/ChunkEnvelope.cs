using System.Text.Json.Serialization;

namespace UKSF.Api.ArmaServer.Models.Persistence;

public class ChunkEnvelope
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = string.Empty;

    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("data")]
    public string Data { get; set; } = string.Empty;
}
