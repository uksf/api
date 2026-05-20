using MongoDB.Bson;
using UKSF.Api.ArmaServer.Models;

namespace UKSF.Api.ArmaServer.Services.StatsEventProcessors;

public class CardiacArrestEventProcessor : IStatsEventProcessor
{
    public string EventType => "cardiacArrest";

    public void ProcessForPlayer(BsonDocument evt, PlayerMissionStats stats)
    {
        if (evt.GetValue("value", 1).ToInt32() == 1)
        {
            stats.TimesInCardiacArrest++;
        }
    }
}
