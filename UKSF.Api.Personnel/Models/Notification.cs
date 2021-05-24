using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using UKSF.Api.Base.Models;

namespace UKSF.Api.Personnel.Models
{
    public class Notification : MongoObject
    {
        public string Icon;
        public string Link;
        public string Message;
        [BsonRepresentation(BsonType.ObjectId)] public string Owner;
        public bool Read;
        public DateTime Timestamp = DateTime.Now;
    }
}
