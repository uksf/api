using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UKSF.Api.Models.Operations {
    public class Opord {
        [BsonId, BsonRepresentation(BsonType.ObjectId)] public string id;
        public string name, map, type, description;
        public DateTime start, end;
    }
}
