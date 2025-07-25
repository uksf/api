using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using UKSF.Api.Core.Models;

namespace UKSF.Api.Modpack.Models;

public class DomainModpackRelease : MongoObject
{
    public string Changelog { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string CreatorId { get; set; }

    public bool IsDraft { get; set; }
    public DateTime Timestamp { get; set; }
    public string Version { get; set; }
}
