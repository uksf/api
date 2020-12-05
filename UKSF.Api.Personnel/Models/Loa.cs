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
        public bool Emergency { get; set; }
        public DateTime End { get; set; }
        public bool Late { get; set; }
        public string Reason { get; set; }
        [BsonRepresentation(BsonType.ObjectId)] public string Recipient { get; set; }
        public DateTime Start { get; set; }
        public LoaReviewState State { get; set; }
        public DateTime Submitted { get; set; }
    }
}
