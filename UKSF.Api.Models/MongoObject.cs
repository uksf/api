using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UKSF.Api.Models {
    public class MongoObject {
        [BsonId, BsonRepresentation(BsonType.ObjectId)] public string id;

        public MongoObject() => id = ObjectId.GenerateNewId().ToString();

        public MongoObject(string id) => this.id = id;
    }
}
