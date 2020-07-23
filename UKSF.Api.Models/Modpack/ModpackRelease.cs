using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UKSF.Api.Models.Modpack {
    public class ModpackRelease : DatabaseObject {
        public string changelog;
        [BsonRepresentation(BsonType.ObjectId)] public string creatorId;
        public string description;
        public bool isDraft;
        public DateTime timestamp;
        public string version;
    }
}
