using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using UKSF.Api.Base.Models;

namespace UKSF.Api.Command.Models {
    public record DischargeCollection : MongoObject {
        [BsonRepresentation(BsonType.ObjectId)] public string AccountId { get; set; }
        public List<Discharge> Discharges { get; set; } = new();
        public string Name { get; set; }
        public bool Reinstated { get; set; }
        [BsonIgnore] public bool RequestExists { get; set; }
    }

    public record Discharge : MongoObject {
        public string DischargedBy { get; set; }
        public string Rank { get; set; }
        public string Reason { get; set; }
        public string Role { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string Unit { get; set; }
    }
}
