using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UKSF.Api.Models {
    public class MongoObject {
        [BsonId, BsonRepresentation(BsonType.ObjectId)] public string id = ObjectId.GenerateNewId().ToString();
    }
}
