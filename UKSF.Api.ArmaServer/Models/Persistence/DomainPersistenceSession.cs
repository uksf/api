using MongoDB.Bson.Serialization.Attributes;
using UKSF.Api.Core.Models;

namespace UKSF.Api.ArmaServer.Models.Persistence;

public class DomainPersistenceSession : MongoObject
{
    public string Key { get; set; } = string.Empty;
    public List<PersistenceObject> Objects { get; set; } = [];
    public List<string> DeletedObjects { get; set; } = [];
    public Dictionary<string, PlayerRedeployData> Players { get; set; } = new();
    public List<object[]> Markers { get; set; } = [];
    public int[] DateTime { get; set; } = [];
    public Dictionary<string, object> CustomData { get; set; } = new();

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public System.DateTime SavedAt { get; set; }
}
