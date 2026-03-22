using MongoDB.Bson;
using UKSF.Api.ArmaServer.Models;

namespace UKSF.Api.ArmaServer.Services.StatsEventProcessors;

public class HitEventProcessor : IStatsEventProcessor
{
    public string EventType => "hit";

    public void ProcessForPlayer(BsonDocument evt, PlayerMissionStats stats)
    {
        var weapon = evt.GetValue("weapon", "unknown").AsString;
        var ammo = evt.GetValue("ammo", "unknown").AsString;
        var bodyPart = evt.GetValue("bodyPart", "").AsString;
        var targetType = evt.GetValue("targetType", "unknown").AsString;
        var distance2D = evt.GetValue("distance2D", 0).ToDouble();
        var distance3D = evt.GetValue("distance3D", 0).ToDouble();

        stats.TotalHits++;
        stats.HitsByTargetType[targetType] = stats.HitsByTargetType.GetValueOrDefault(targetType) + 1;

        if (!stats.WeaponBreakdown.TryGetValue(weapon, out var weaponStats))
        {
            weaponStats = new WeaponStats();
            stats.WeaponBreakdown[weapon] = weaponStats;
        }

        weaponStats.Hits++;
        weaponStats.TotalEngagementDistance2D += distance2D;
        weaponStats.TotalEngagementDistance3D += distance3D;

        if (distance2D > weaponStats.MaxEngagementDistance2D)
        {
            weaponStats.MaxEngagementDistance2D = distance2D;
        }

        if (!weaponStats.AmmoBreakdown.TryGetValue(ammo, out var ammoStats))
        {
            ammoStats = new AmmoStats();
            weaponStats.AmmoBreakdown[ammo] = ammoStats;
        }

        ammoStats.Hits++;
        ammoStats.TotalEngagementDistance2D += distance2D;
        ammoStats.TotalEngagementDistance3D += distance3D;

        if (distance2D > ammoStats.MaxEngagementDistance2D)
        {
            ammoStats.MaxEngagementDistance2D = distance2D;
        }

        if (!string.IsNullOrEmpty(bodyPart))
        {
            stats.BodyPartHits[bodyPart] = stats.BodyPartHits.GetValueOrDefault(bodyPart) + 1;
            ammoStats.BodyPartHits[bodyPart] = ammoStats.BodyPartHits.GetValueOrDefault(bodyPart) + 1;
        }
    }
}
