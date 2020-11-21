using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using UKSF.Api.Base.Models;

namespace UKSF.Api.Personnel.Models {
    public enum LoaReviewState {
        PENDING,
        APPROVED,
        REJECTED
    }

    public record Loa : MongoObject {
        public bool Emergency;
        public DateTime End;
        public bool Late;
        public string Reason;
        [BsonRepresentation(BsonType.ObjectId)] public string Recipient;
        public DateTime Start;
        public LoaReviewState State;
        public DateTime Submitted;
    }
}
