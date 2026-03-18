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
                var part = bodyPart.AsString;
                stats.WoundsByBodyPart[part] = stats.WoundsByBodyPart.GetValueOrDefault(part) + 1;
            }
        }

        var damageType = evt.GetValue("damageType", "unknown").AsString;
        stats.WoundsByDamageType[damageType] = stats.WoundsByDamageType.GetValueOrDefault(damageType) + 1;
    }
}
