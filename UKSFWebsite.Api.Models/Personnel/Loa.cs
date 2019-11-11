using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UKSFWebsite.Api.Models.Personnel {
    public enum LoaReviewState {
        PENDING,
        APPROVED,
        REJECTED
    }

    public class Loa {
        public bool emergency;
        public DateTime end;
        [BsonId, BsonRepresentation(BsonType.ObjectId)] public string id;
        public bool late;
        public string reason;
        [BsonRepresentation(BsonType.ObjectId)] public string recipient;
        public DateTime start;
        public LoaReviewState state;
        public DateTime submitted;
    }
}
