using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UKSF.Api.Models.Utility {
    public class ConfirmationCode {
        [BsonId, BsonRepresentation(BsonType.ObjectId)] public string id;
        public DateTime timestamp = DateTime.UtcNow;
        public string value;
    }
}
