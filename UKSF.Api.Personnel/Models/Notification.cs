using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using UKSF.Api.Base.Models;

namespace UKSF.Api.Personnel.Models {
    public class Notification : DatabaseObject {
        public string icon;
        public string link;
        public string message;
        [BsonRepresentation(BsonType.ObjectId)] public string owner;
        public bool read;
        public DateTime timestamp = DateTime.Now;
    }
}
