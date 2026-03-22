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

        stats.TotalHits++;
        stats.HitsByTargetType[targetType] = stats.HitsByTargetType.GetValueOrDefault(targetType) + 1;

        if (!stats.WeaponBreakdown.TryGetValue(weapon, out var weaponStats))
        {
            weaponStats = new WeaponStats();
            stats.WeaponBreakdown[weapon] = weaponStats;
        }

        weaponStats.Hits++;
        weaponStats.EngagementDistanceSum += distance2D;
        weaponStats.MinEngagementDistance = Math.Min(weaponStats.MinEngagementDistance, distance2D);
        weaponStats.MaxEngagementDistance = Math.Max(weaponStats.MaxEngagementDistance, distance2D);

        if (!weaponStats.AmmoBreakdown.TryGetValue(ammo, out var ammoStats))
        {
            ammoStats = new AmmoStats();
            weaponStats.AmmoBreakdown[ammo] = ammoStats;
        }

        ammoStats.Hits++;
        ammoStats.EngagementDistanceSum += distance2D;
        ammoStats.MinEngagementDistance = Math.Min(ammoStats.MinEngagementDistance, distance2D);
        ammoStats.MaxEngagementDistance = Math.Max(ammoStats.MaxEngagementDistance, distance2D);

        if (!string.IsNullOrEmpty(bodyPart))
        {
            stats.BodyPartHits[bodyPart] = stats.BodyPartHits.GetValueOrDefault(bodyPart) + 1;
            ammoStats.BodyPartHits[bodyPart] = ammoStats.BodyPartHits.GetValueOrDefault(bodyPart) + 1;
        }
    }
}
