using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UKSF.Api.Personnel.Models {
    public enum ApplicationState {
        ACCEPTED,
        REJECTED,
        WAITING
    }

    public class Application {
        [BsonRepresentation(BsonType.ObjectId)] public string ApplicationCommentThread { get; set; }
        public DateTime DateAccepted { get; set; }
        public DateTime DateCreated { get; set; }
        public Dictionary<string, uint> Ratings { get; set; } = new();
        [BsonRepresentation(BsonType.ObjectId)] public string Recruiter { get; set; }
        [BsonRepresentation(BsonType.ObjectId)] public string RecruiterCommentThread { get; set; }
        public ApplicationState State { get; set; } = ApplicationState.WAITING;
    }
}
