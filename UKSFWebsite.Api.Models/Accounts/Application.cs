using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UKSFWebsite.Api.Models.Accounts {
    public enum ApplicationState {
        ACCEPTED,
        REJECTED,
        WAITING
    }

    public class Application {
        [BsonRepresentation(BsonType.ObjectId)] public string applicationCommentThread;
        public DateTime dateAccepted;
        public DateTime dateCreated;
        public Dictionary<string, uint> ratings = new Dictionary<string, uint>();
        [BsonRepresentation(BsonType.ObjectId)] public string recruiter;
        [BsonRepresentation(BsonType.ObjectId)] public string recruiterCommentThread;
        public ApplicationState state = ApplicationState.WAITING;
    }
}
