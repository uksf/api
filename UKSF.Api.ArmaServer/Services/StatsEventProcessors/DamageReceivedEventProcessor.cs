using MongoDB.Bson;
using UKSF.Api.ArmaServer.Models;

namespace UKSF.Api.ArmaServer.Services.StatsEventProcessors;

public class DamageReceivedEventProcessor : IStatsEventProcessor
{
    public string EventType => "damageReceived";

    public void ProcessForPlayer(BsonDocument evt, PlayerMissionStats stats)
    {
        stats.TimesWounded++;

        if (evt.Contains("bodyParts") && evt["bodyParts"].IsBsonArray)
        {
            foreach (var bodyPart in evt["bodyParts"].AsBsonArray)
            {
                // ACE uses "#structural" (context==0, whole-body damage) with a leading "#" to
                // distinguish from real config hitpoints. Normalise to "structural" downstream.
                var part = bodyPart.AsString;
                if (part.StartsWith('#'))
                {
                    part = part[1..];
                }

                stats.WoundsByBodyPart[part] = stats.WoundsByBodyPart.GetValueOrDefault(part) + 1;
            }
        }

        var damageType = evt.GetValue("damageType", "unknown").AsString;
        stats.WoundsByDamageType[damageType] = stats.WoundsByDamageType.GetValueOrDefault(damageType) + 1;
    }
}
