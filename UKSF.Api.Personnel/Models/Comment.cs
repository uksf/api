using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using UKSF.Api.Base.Models;

namespace UKSF.Api.Personnel.Models {
    public class Comment : DatabaseObject {
        [BsonRepresentation(BsonType.ObjectId)] public string author;
        public string content;
        public DateTime timestamp;
    }
}
