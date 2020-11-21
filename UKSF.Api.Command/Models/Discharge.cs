using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using UKSF.Api.Base.Models;

namespace UKSF.Api.Command.Models {
    public record DischargeCollection : MongoObject {
        [BsonRepresentation(BsonType.ObjectId)] public string AccountId;
        public List<Discharge> Discharges = new();
        public string Name;
        public bool Reinstated;
        [BsonIgnore] public bool RequestExists;
    }

    public record Discharge : MongoObject {
        public string DischargedBy;
        public string Rank;
        public string Reason;
        public string Role;
        public DateTime Timestamp = DateTime.Now;
        public string Unit;
    }
}
