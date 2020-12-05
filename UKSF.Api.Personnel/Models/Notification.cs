using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using UKSF.Api.Base.Models;

namespace UKSF.Api.Personnel.Models {
    public record Notification : MongoObject {
        public string Icon { get; set; }
        public string Link { get; set; }
        public string Message { get; set; }
        [BsonRepresentation(BsonType.ObjectId)] public string Owner { get; set; }
        public bool Read { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}
