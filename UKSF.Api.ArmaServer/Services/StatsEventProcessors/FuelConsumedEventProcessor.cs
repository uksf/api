using MongoDB.Bson;
using UKSF.Api.ArmaServer.Models;

namespace UKSF.Api.ArmaServer.Services.StatsEventProcessors;

public class FuelConsumedEventProcessor : IStatsEventProcessor
{
    public string EventType => "fuelConsumed";

    public void ProcessForPlayer(BsonDocument evt, PlayerMissionStats stats)
    {
        stats.TotalFuelConsumed += evt.GetValue("amount", 0).ToDouble();
    }
}
