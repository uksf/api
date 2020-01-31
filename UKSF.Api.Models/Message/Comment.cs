using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UKSF.Api.Models.Message {
    public class Comment : MongoObject {
        [BsonRepresentation(BsonType.ObjectId)] public string author;
        public string content;
        public DateTime timestamp;
    }
}
