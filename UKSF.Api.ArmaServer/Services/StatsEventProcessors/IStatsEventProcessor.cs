using MongoDB.Bson;
using UKSF.Api.ArmaServer.Models;

namespace UKSF.Api.ArmaServer.Services.StatsEventProcessors;

public interface IStatsEventProcessor
{
    string EventType { get; }
    void ProcessForPlayer(BsonDocument evt, PlayerMissionStats stats);
    void ProcessForMission(BsonDocument evt, MissionStats stats);
}
