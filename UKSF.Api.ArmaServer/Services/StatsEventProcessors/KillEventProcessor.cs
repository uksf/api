using MongoDB.Bson;
using UKSF.Api.ArmaServer.Models;

namespace UKSF.Api.ArmaServer.Services.StatsEventProcessors;

/// <summary>
/// Processes kill events for the killer. Assists are handled separately
/// by the consumer since they affect different players.
/// </summary>
public class KillEventProcessor : IStatsEventProcessor
{
    public string EventType => "kill";

    public void ProcessForPlayer(BsonDocument evt, PlayerMissionStats stats)
    {
        var indirect = evt.GetValue("indirect", false).AsBoolean;
        var targetType = evt.GetValue("targetType", "unknown").AsString;

        if (indirect)
        {
            stats.Kills.Indirect++;
        }
        else
        {
            stats.Kills.Direct++;
        }

        stats.KillsByTargetType[targetType] = stats.KillsByTargetType.GetValueOrDefault(targetType) + 1;
    }
}
