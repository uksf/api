using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using UKSF.Api.Core.Models;

namespace UKSF.Api.ArmaServer.Models;

public enum MissionStatus
{
    Scheduled = 0,
    Complete = 1
}

public class DomainMission : MongoObject
{
    [BsonRepresentation(BsonType.ObjectId)]
    public string OperationId { get; set; }

    public string Title { get; set; }
    public DateTime ScheduledTime { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string ServerId { get; set; }

    public string MissionName { get; set; }
    public string Warno { get; set; }
    public MissionStatus Status { get; set; } = MissionStatus.Scheduled;
    public bool AutoLaunch { get; set; }
    public string SessionId { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string LaunchedServerId { get; set; }

    public string LaunchedMission { get; set; }
    public DateTime? LaunchedAt { get; set; }
}
