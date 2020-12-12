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

    public class DetailedApplication {
        public Account Account;
        public string DisplayName;
        public ApplicationAge Age;
        public double DaysProcessing;
        public double DaysProcessed;
        public string NextCandidateOp;
        public double AverageProcessingTime;
        public string SteamProfile;
        public string Recruiter;
        public string RecruiterId;
    }

    public class ApplicationAge {
        public int Years;
        public int Months;
    }

    public class WaitingApplication {
        public Account Account;
        public string SteamProfile;
        public double DaysProcessing;
        public double ProcessingDifference;
        public string Recruiter;
    }

    public class CompletedApplication {
        public Account Account;
        public string DisplayName;
        public double DaysProcessed;
        public string Recruiter;
    }

    public class ApplicationsOverview {
        public List<WaitingApplication> Waiting;
        public List<WaitingApplication> AllWaiting;
        public List<CompletedApplication> Complete;
        public List<string> Recruiters;
    }

    public class Recruiter {
        public string Id;
        public string Name;
    }
}
