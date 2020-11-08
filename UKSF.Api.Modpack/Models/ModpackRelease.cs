using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using UKSF.Api.Base.Models;

namespace UKSF.Api.Modpack.Models {
    public class ModpackRelease : DatabaseObject {
        public string changelog;
        [BsonRepresentation(BsonType.ObjectId)] public string creatorId;
        public string description;
        public bool isDraft;
        public DateTime timestamp;
        public string version;
    }
}
