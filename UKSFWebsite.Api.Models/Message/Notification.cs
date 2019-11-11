using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UKSFWebsite.Api.Models.Message {
    public class Notification {
        public string icon;
        [BsonId, BsonRepresentation(BsonType.ObjectId)] public string id;
        public string link;
        public string message;
        [BsonRepresentation(BsonType.ObjectId)] public string owner;
        public bool read = false;
        public DateTime timestamp = DateTime.Now;
    }
}
