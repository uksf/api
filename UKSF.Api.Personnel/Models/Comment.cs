using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using UKSF.Api.Base.Models;

namespace UKSF.Api.Personnel.Models {
    public record Comment : MongoObject {
        [BsonRepresentation(BsonType.ObjectId)] public string Author;
        public string Content;
        public DateTime Timestamp;
    }
}
