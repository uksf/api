using MongoDB.Bson;
using UKSF.Api.ArmaServer.Models;

namespace UKSF.Api.ArmaServer.Services.StatsEventProcessors;

/// <summary>
/// Processes combatDamage events emitted from the SQF damage providers
/// (infantry via ace_medical_woundReceived, vehicles via ace_vehicle_damage_damageApplied).
/// Represents damage dealt to targets that survived or were removed without
/// an EntityKilled event.
/// </summary>
public class DamageEventProcessor : IStatsEventProcessor
{
    public string EventType => "combatDamage";

    public void ProcessForPlayer(BsonDocument evt, PlayerMissionStats stats)
    {
        var damage = evt.GetValue("damage", 0).ToDouble();
        stats.TotalDamageDealt += damage;

        var damageType = evt.GetValue("damageType", "unknown").AsString;
        if (string.IsNullOrEmpty(damageType))
        {
            damageType = "unknown";
        }

        stats.DamageDealtByAmmo[damageType] = stats.DamageDealtByAmmo.GetValueOrDefault(damageType) + damage;
    }
}
