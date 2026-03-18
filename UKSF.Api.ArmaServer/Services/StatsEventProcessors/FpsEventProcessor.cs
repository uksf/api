using MongoDB.Bson;
using UKSF.Api.ArmaServer.Models;

namespace UKSF.Api.ArmaServer.Services.StatsEventProcessors;

public class FpsEventProcessor : IStatsEventProcessor
{
    public string EventType => "fps";

    public void ProcessForPlayer(BsonDocument evt, PlayerMissionStats stats)
    {
        var value = evt.GetValue("value", 0).ToInt32();

        stats.FpsSampleCount++;
        stats.FpsTotalSum += value;

        if (value < stats.FpsMin)
        {
            stats.FpsMin = value;
        }
    }
}
