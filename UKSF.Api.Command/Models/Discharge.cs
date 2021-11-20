using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using UKSF.Api.Base.Models;

namespace UKSF.Api.Command.Models
{
    public class DischargeCollection : MongoObject
    {
        [BsonRepresentation(BsonType.ObjectId)] public string AccountId;
        public List<Discharge> Discharges = new();
        public string Name;
        public bool Reinstated;
        [BsonIgnore] public bool RequestExists;
    }

    public class Discharge : MongoObject
    {
        public string DischargedBy;
        public string Rank;
        public string Reason;
        public string Role;
        public DateTime Timestamp = DateTime.UtcNow;
        public string Unit;
    }
}
