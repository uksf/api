using MongoDB.Bson;
using UKSF.Api.ArmaServer.Models;

namespace UKSF.Api.ArmaServer.Services.StatsEventProcessors;

public class FpsEventProcessor : IStatsEventProcessor
{
    public string EventType => "fps";

    public void ProcessForPlayer(BsonDocument evt, PlayerMissionStats stats)
    {
        // FPS stats (min, max, average, P1) are computed on mission end
        // from raw FPS events in the merged batch data.
        // Per-batch processing only counts FPS events in missionStats.eventCounts
        // (handled by the consumer before this method is called).
    }
}
