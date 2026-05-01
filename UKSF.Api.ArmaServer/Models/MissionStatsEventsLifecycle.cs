using MongoDB.Bson;
using UKSF.Api.Core.Models;

namespace UKSF.Api.ArmaServer.Models;

public class MissionStatsEventsLifecycle : MongoObject
{
    public string MissionSessionId { get; set; } = string.Empty;
    public List<BsonDocument> Events { get; set; } = [];
}
