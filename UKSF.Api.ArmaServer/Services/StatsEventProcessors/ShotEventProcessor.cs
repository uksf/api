using MongoDB.Bson;
using UKSF.Api.ArmaServer.Models;

namespace UKSF.Api.ArmaServer.Services.StatsEventProcessors;

public class ShotEventProcessor : IStatsEventProcessor
{
    public string EventType => "shot";

    public void ProcessForPlayer(BsonDocument evt, PlayerMissionStats stats)
    {
        var weapon = evt.GetValue("weapon", "unknown").AsString;
        var fireMode = evt.GetValue("fireMode", "unknown").AsString;

        stats.TotalShots++;

        if (!stats.WeaponBreakdown.TryGetValue(weapon, out var weaponStats))
        {
            weaponStats = new WeaponStats();
            stats.WeaponBreakdown[weapon] = weaponStats;
        }

        weaponStats.Shots++;

        weaponStats.FireModes[fireMode] = weaponStats.FireModes.GetValueOrDefault(fireMode) + 1;
    }
}
