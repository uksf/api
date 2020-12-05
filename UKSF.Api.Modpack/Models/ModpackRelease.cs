using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using UKSF.Api.Base.Models;

namespace UKSF.Api.Modpack.Models {
    public record ModpackRelease : MongoObject {
        public string Changelog { get; set; }
        [BsonRepresentation(BsonType.ObjectId)] public string CreatorId { get; set; }
        public string Description { get; set; }
        public bool IsDraft { get; set; }
        public DateTime Timestamp { get; set; }
        public string Version { get; set; }
    }
}
