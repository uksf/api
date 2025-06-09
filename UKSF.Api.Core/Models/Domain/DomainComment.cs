using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UKSF.Api.Core.Models.Domain;

public class DomainComment : MongoObject
{
    [BsonRepresentation(BsonType.ObjectId)]
    public string Author { get; set; }

    public string Content { get; set; }
    public DateTime Timestamp { get; set; }
}
