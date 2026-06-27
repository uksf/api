using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using UKSF.Api.Core.Models;

namespace UKSF.Api.ArmaServer.Models;

public enum OpStatus
{
    Scheduled,
    Complete
}

public class DomainOp : MongoObject
{
    [BsonRepresentation(BsonType.ObjectId)]
    public string CampaignId { get; set; }

    public string Title { get; set; }
    public DateTime ScheduledTime { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string ServerId { get; set; }

    public string MissionName { get; set; }
    public string Warno { get; set; }
    public OpStatus Status { get; set; } = OpStatus.Scheduled;

    // Captured asynchronously from the game when the op's launch produces a session.
    public string SessionId { get; set; }

    // Snapshot of what actually launched (distinct from the editable plan pins above).
    [BsonRepresentation(BsonType.ObjectId)]
    public string LaunchedServerId { get; set; }

    public string LaunchedMission { get; set; }
    public DateTime? LaunchedAt { get; set; }
}
