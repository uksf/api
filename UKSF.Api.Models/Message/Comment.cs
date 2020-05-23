using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UKSF.Api.Models.Message {
    public class Comment : DatabaseObject {
        [BsonRepresentation(BsonType.ObjectId)] public string author;
        public string content;
        public DateTime timestamp;
    }
}
