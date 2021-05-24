using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UKSF.Api.Base.Models
{
    public class MongoObject
    {
        [BsonId, BsonRepresentation(BsonType.ObjectId)] public string Id = ObjectId.GenerateNewId().ToString();
    }
}
