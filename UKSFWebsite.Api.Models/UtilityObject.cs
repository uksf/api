using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UKSFWebsite.Api.Models {
    public class UtilityObject {
        [BsonId, BsonRepresentation(BsonType.ObjectId)] public string id;
        public Dictionary<string, string> values = new Dictionary<string, string>();
    }
}
