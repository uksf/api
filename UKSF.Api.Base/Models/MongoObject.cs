using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UKSF.Api.Base.Models {
    public record MongoObject {
        [BsonId, BsonRepresentation(BsonType.ObjectId)] public string Id = ObjectId.GenerateNewId().ToString();
    }
}
