using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using UKSF.Api.Core.Models;

namespace UKSF.Api.ArmaServer.Models.Persistence;

public class DomainPersistenceSession : MongoObject
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("objects")]
    public List<PersistenceObject> Objects { get; set; } = [];

    [JsonPropertyName("deletedObjects")]
    public List<string> DeletedObjects { get; set; } = [];

    [JsonPropertyName("players")]
    public Dictionary<string, PlayerRedeployData> Players { get; set; } = new();

    [JsonPropertyName("markers")]
    public List<List<object>> Markers { get; set; } = [];

    [JsonPropertyName("dateTime")]
    public int[] ArmaDateTime { get; set; } = [];

    [JsonPropertyName("customData")]
    public Dictionary<string, object> CustomData { get; set; } = new();

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    [JsonPropertyName("savedAt")]
    public DateTime SavedAt { get; set; }
}
