using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UKSF.Api.Models.Personnel {
    public enum LoaReviewState {
        PENDING,
        APPROVED,
        REJECTED
    }

    public class Loa : DatabaseObject {
        public bool emergency;
        public DateTime end;
        public bool late;
        public string reason;
        [BsonRepresentation(BsonType.ObjectId)] public string recipient;
        public DateTime start;
        public LoaReviewState state;
        public DateTime submitted;
    }
}
