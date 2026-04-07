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
        var targetClassname = SanitiseMongoKey(evt.GetValue("targetClassname", "unknown").AsString);
        var weapon = evt.GetValue("weapon", "").AsString;
        var ammo = evt.GetValue("ammo", "").AsString;

        if (indirect)
        {
            stats.Kills.Indirect++;
        }
        else
        {
            stats.Kills.Direct++;
        }

        if (!stats.KillsByTargetType.TryGetValue(targetType, out var targetBucket))
        {
            targetBucket = new KillTargetTypeStats();
            stats.KillsByTargetType[targetType] = targetBucket;
        }

        targetBucket.Count++;
        targetBucket.Types[targetClassname] = targetBucket.Types.GetValueOrDefault(targetClassname) + 1;

        // Weapon/ammo only present when the killing hit was attributable (see fnc_providerKills
        // shooter-match logic). Cookoff chains and other unattributed kills emit empty strings.
        if (string.IsNullOrEmpty(weapon))
        {
            return;
        }

        var weaponKey = SanitiseMongoKey(weapon);
        var ammoKey = SanitiseMongoKey(string.IsNullOrEmpty(ammo) ? "unknown" : ammo);

        if (!stats.KillsByWeapon.TryGetValue(weaponKey, out var weaponBucket))
        {
            weaponBucket = new KillWeaponStats();
            stats.KillsByWeapon[weaponKey] = weaponBucket;
        }

        weaponBucket.Count++;
        weaponBucket.Ammo[ammoKey] = weaponBucket.Ammo.GetValueOrDefault(ammoKey) + 1;
    }

    // Mongo rejects empty keys, keys containing '.', and keys starting with '$'.
    // Arma classnames/weapons never hit these, but strip defensively so a malformed
    // payload can't poison the update.
    private static string SanitiseMongoKey(string key)
    {
        key = key.Replace(".", "").TrimStart('$');
        return string.IsNullOrWhiteSpace(key) ? "unknown" : key;
    }
}
