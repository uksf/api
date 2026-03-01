using MongoDB.Bson;
using UKSF.Api.Core.Models;

namespace UKSF.Api.ArmaServer.Models;

public class MissionStatsBatch : MongoObject
{
    public string MissionSessionId { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; }
    public string Mission { get; set; } = string.Empty;
    public string Map { get; set; } = string.Empty;
    public List<BsonDocument> Events { get; set; } = [];
}
