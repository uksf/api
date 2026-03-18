using MongoDB.Bson;
using UKSF.Api.ArmaServer.Models;

namespace UKSF.Api.ArmaServer.Services.StatsEventProcessors;

/// <summary>
/// Processes standalone damage events from ledger cleanup.
/// These represent damage dealt to targets that survived or were
/// removed without an EntityKilled event.
/// </summary>
public class DamageEventProcessor : IStatsEventProcessor
{
    public string EventType => "damage";

    public void ProcessForPlayer(BsonDocument evt, PlayerMissionStats stats)
    {
        var totalDamage = evt.GetValue("totalDamage", 0).ToDouble();
        stats.TotalDamageDealt += totalDamage;
    }
}
