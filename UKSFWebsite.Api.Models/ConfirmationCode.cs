using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UKSFWebsite.Api.Models {
    public class ConfirmationCode {
        [BsonId, BsonRepresentation(BsonType.ObjectId)] public string id;
        public DateTime timestamp = DateTime.UtcNow;
        public string value;
    }
}
