using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UKSF.Api.Core.Models.Domain;

public enum ApplicationState
{
    Accepted = 0,
    Rejected = 1,
    Waiting = 2
}

public class DomainApplication
{
    [BsonRepresentation(BsonType.ObjectId)]
    public string ApplicationCommentThread { get; set; }

    public DateTime DateAccepted { get; set; }
    public DateTime DateCreated { get; set; }
    public Dictionary<string, uint> Ratings { get; set; } = new();

    [BsonRepresentation(BsonType.ObjectId)]
    public string Recruiter { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string RecruiterCommentThread { get; set; }

    public ApplicationState State { get; set; } = ApplicationState.Waiting;
}

public class DetailedApplication
{
    public int AcceptableAge { get; set; }
    public Account Account { get; set; }
    public ApplicationAge Age { get; set; }
    public double AverageProcessingTime { get; set; }
    public double DaysProcessed { get; set; }
    public double DaysProcessing { get; set; }
    public string DisplayName { get; set; }
    public string NextCandidateOp { get; set; }
    public string Recruiter { get; set; }
    public string RecruiterId { get; set; }
    public string SteamProfile { get; set; }
}

public class ApplicationAge
{
    public int Months { get; set; }
    public int Years { get; set; }
}

public class ActiveApplication
{
    public Account Account { get; set; }
    public double DaysProcessing { get; set; }
    public double ProcessingDifference { get; set; }
    public string Recruiter { get; set; }
    public string SteamProfile { get; set; }
}

public class CompletedApplication
{
    public Account Account { get; set; }
    public double DaysProcessed { get; set; }
    public string DisplayName { get; set; }
    public string Recruiter { get; set; }
}

public class Recruiter
{
    public string Id { get; set; }
    public string Name { get; set; }
    public bool Active { get; set; }
}
