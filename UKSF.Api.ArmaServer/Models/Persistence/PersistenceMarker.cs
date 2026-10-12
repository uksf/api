using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization.Attributes;

namespace UKSF.Api.ArmaServer.Models.Persistence;

public class PersistenceMarker
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("position")]
    public double[] Position { get; set; } = [];

    [JsonPropertyName("shape")]
    public string Shape { get; set; } = string.Empty;

    [BsonIgnoreIfNull]
    [JsonPropertyName("type")]
    public string Type { get; set; }

    [BsonIgnoreIfNull]
    [JsonPropertyName("brush")]
    public string Brush { get; set; }

    [BsonIgnoreIfNull]
    [JsonPropertyName("size")]
    public double[] Size { get; set; }

    [JsonPropertyName("colour")]
    public string Colour { get; set; } = string.Empty;

    [JsonPropertyName("alpha")]
    public double Alpha { get; set; }

    [BsonIgnoreIfNull]
    [JsonPropertyName("direction")]
    public double? Direction { get; set; }

    [BsonIgnoreIfNull]
    [JsonPropertyName("text")]
    public string Text { get; set; }

    [BsonIgnoreIfNull]
    [JsonPropertyName("polyline")]
    public double[] Polyline { get; set; }
}
