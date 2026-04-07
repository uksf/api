using MongoDB.Bson;
using UKSF.Api.ArmaServer.Models;

namespace UKSF.Api.ArmaServer.Services.StatsEventProcessors;

public class ShotEventProcessor : IStatsEventProcessor
{
    public string EventType => "shot";

    public void ProcessForPlayer(BsonDocument evt, PlayerMissionStats stats)
    {
        var weapon = evt.GetValue("weapon", "unknown").AsString;
        var ammo = evt.GetValue("ammo", "unknown").AsString;
        var category = evt.GetValue("category", "other").AsString;

        stats.TotalShots++;

        switch (category)
        {
            case "ballistic": stats.BallisticShots++; break;
            case "explosive": stats.ExplosiveShots++; break;
            default:          stats.OtherShots++; break;
        }

        if (!stats.WeaponBreakdown.TryGetValue(weapon, out var weaponStats))
        {
            weaponStats = new WeaponStats();
            stats.WeaponBreakdown[weapon] = weaponStats;
        }

        weaponStats.Shots++;

        if (!weaponStats.AmmoBreakdown.TryGetValue(ammo, out var ammoStats))
        {
            ammoStats = new AmmoStats();
            weaponStats.AmmoBreakdown[ammo] = ammoStats;
        }

        ammoStats.Shots++;
    }
}
