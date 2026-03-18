using MongoDB.Bson;
using UKSF.Api.ArmaServer.Models;

namespace UKSF.Api.ArmaServer.Services.StatsEventProcessors;

public class DistanceInVehicleEventProcessor : IStatsEventProcessor
{
    public string EventType => "distanceInVehicle";

    public void ProcessForPlayer(BsonDocument evt, PlayerMissionStats stats)
    {
        stats.DistanceInVehicle += evt.GetValue("metres", 0).ToDouble();
    }
}
