using MongoDB.Bson;
using UKSF.Api.ArmaServer.Models;

namespace UKSF.Api.ArmaServer.Services.StatsEventProcessors;

public class ExplosivePlacedEventProcessor : IStatsEventProcessor
{
    public string EventType => "explosivePlaced";

    public void ProcessForPlayer(BsonDocument evt, PlayerMissionStats stats)
    {
        stats.ExplosivesPlaced++;
    }
}
