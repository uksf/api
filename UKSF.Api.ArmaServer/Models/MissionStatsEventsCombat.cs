using MongoDB.Bson;
using UKSF.Api.Core.Models;

namespace UKSF.Api.ArmaServer.Models;

public class MissionStatsEventsCombat : MongoObject
{
    public const int MaxEventsPerBucket = 5000;

    public string MissionSessionId { get; set; } = string.Empty;
    public int BucketIndex { get; set; }
    public int EventCount { get; set; }
    public List<BsonDocument> Events { get; set; } = [];
}
