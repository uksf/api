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
        [BsonRepresentation(BsonType.ObjectId)] public string ApplicationCommentThread;
        public DateTime DateAccepted;
        public DateTime DateCreated;
        public Dictionary<string, uint> Ratings = new();
        [BsonRepresentation(BsonType.ObjectId)] public string Recruiter;
        [BsonRepresentation(BsonType.ObjectId)] public string RecruiterCommentThread;
        public ApplicationState State = ApplicationState.WAITING;
    }
}
