using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using UKSF.Api.Base.Models;

namespace UKSF.Api.Modpack.Models {
    public class ModpackRelease : MongoObject {
        public string Changelog;
        [BsonRepresentation(BsonType.ObjectId)] public string CreatorId;
        public string Description;
        public bool IsDraft;
        public DateTime Timestamp;
        public string Version;
    }
}
