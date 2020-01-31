using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UKSF.Api.Models.Personnel {
    public class DischargeCollection : MongoObject {
        [BsonRepresentation(BsonType.ObjectId)] public string accountId;
        public List<Discharge> discharges = new List<Discharge>();
        public string name;
        public bool reinstated;
        [BsonIgnore] public bool requestExists;
    }

    public class Discharge : MongoObject {
        public string dischargedBy;
        public string rank;
        public string reason;
        public string role;
        public DateTime timestamp = DateTime.Now;
        public string unit;
    }
}
