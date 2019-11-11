using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UKSFWebsite.Api.Models.Utility {
    public class VariableItem {
        [BsonId, BsonRepresentation(BsonType.ObjectId)] public string id;
        public object item;
        public string key;
    }
}
