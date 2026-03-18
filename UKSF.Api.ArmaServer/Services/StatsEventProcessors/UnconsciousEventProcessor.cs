using MongoDB.Bson;
using UKSF.Api.ArmaServer.Models;

namespace UKSF.Api.ArmaServer.Services.StatsEventProcessors;

public class UnconsciousEventProcessor : IStatsEventProcessor
{
    public string EventType => "unconscious";

    public void ProcessForPlayer(BsonDocument evt, PlayerMissionStats stats)
    {
        stats.TimesUnconscious++;
    }
}
