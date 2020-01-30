using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UKSF.Api.Models.Message {
    public class Comment {
        [BsonRepresentation(BsonType.ObjectId)] public string author;
        public string content;
        [BsonId, BsonRepresentation(BsonType.ObjectId)] public string id;
        public DateTime timestamp;
    }
}
