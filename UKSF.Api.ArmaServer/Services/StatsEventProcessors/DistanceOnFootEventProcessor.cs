using MongoDB.Bson;
using UKSF.Api.ArmaServer.Models;

namespace UKSF.Api.ArmaServer.Services.StatsEventProcessors;

public class DistanceOnFootEventProcessor : IStatsEventProcessor
{
    public string EventType => "distanceOnFoot";

    public void ProcessForPlayer(BsonDocument evt, PlayerMissionStats stats)
    {
        stats.DistanceOnFoot += evt.GetValue("metres", 0).ToDouble();
    }
}
