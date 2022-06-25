using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UKSF.Api.Personnel.Models
{
    public enum ApplicationState
    {
        ACCEPTED,
        REJECTED,
        WAITING
    }

    public class Application
    {
        [BsonRepresentation(BsonType.ObjectId)] public string ApplicationCommentThread;
        public DateTime DateAccepted;
        public DateTime DateCreated;
        public Dictionary<string, uint> Ratings = new();
        [BsonRepresentation(BsonType.ObjectId)] public string Recruiter;
        [BsonRepresentation(BsonType.ObjectId)] public string RecruiterCommentThread;
        public ApplicationState State = ApplicationState.WAITING;
    }

    public class DetailedApplication
    {
        public Account Account;
        public ApplicationAge Age;
        public int AcceptableAge;
        public double AverageProcessingTime;
        public double DaysProcessed;
        public double DaysProcessing;
        public string DisplayName;
        public string NextCandidateOp;
        public string Recruiter;
        public string RecruiterId;
        public string SteamProfile;
    }

    public class ApplicationAge
    {
        public int Months;
        public int Years;
    }

    public class WaitingApplication
    {
        public Account Account;
        public double DaysProcessing;
        public double ProcessingDifference;
        public string Recruiter;
        public string SteamProfile;
    }

    public class CompletedApplication
    {
        public Account Account;
        public double DaysProcessed;
        public string DisplayName;
        public string Recruiter;
    }

    public class ApplicationsOverview
    {
        public List<WaitingApplication> AllWaiting;
        public List<CompletedApplication> Complete;
        public List<string> Recruiters;
        public List<WaitingApplication> Waiting;
    }

    public class Recruiter
    {
        public string Id;
        public string Name;
    }
}
