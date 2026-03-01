using MongoDB.Bson;
using UKSF.Api.ArmaServer.Models;

namespace UKSF.Api.ArmaServer.Services.StatsEventProcessors;

public class HitEventProcessor : IStatsEventProcessor
{
    public string EventType => "hit";

    public void ProcessForPlayer(BsonDocument evt, PlayerMissionStats stats)
    {
        var weapon = evt.GetValue("weapon", "unknown").AsString;
        var bodyPart = evt.GetValue("bodyPart", "unknown").AsString;
        var distance = evt.GetValue("distance", 0).ToInt32();

        stats.TotalHits++;
        stats.TotalDistance += distance;

        if (!stats.WeaponBreakdown.TryGetValue(weapon, out var weaponStats))
        {
            weaponStats = new WeaponStats();
            stats.WeaponBreakdown[weapon] = weaponStats;
        }

        weaponStats.Hits++;

        if (!stats.BodyPartHits.ContainsKey(bodyPart))
        {
            stats.BodyPartHits[bodyPart] = 0;
        }

        stats.BodyPartHits[bodyPart]++;
    }
}
