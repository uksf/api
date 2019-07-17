using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UKSFWebsite.Api.Models {
    public class DischargeCollection {
        [BsonRepresentation(BsonType.ObjectId)] public string accountId;
        [BsonId, BsonRepresentation(BsonType.ObjectId)] public string id;
        public List<Discharge> discharges = new List<Discharge>();
        public bool reinstated;
        public string name;
        [BsonIgnore] public bool requestExists;
    }
    
    public class Discharge {
        public string dischargedBy;
        [BsonId, BsonRepresentation(BsonType.ObjectId)] public string id;
        public string rank;
        public string reason;
        public string role;
        public DateTime timestamp = DateTime.Now;
        public string unit;
    }
}
