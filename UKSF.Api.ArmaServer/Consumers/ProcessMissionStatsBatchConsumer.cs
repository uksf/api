using MassTransit;
using MongoDB.Bson;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.ArmaServer.Services.StatsEventProcessors;
using UKSF.Api.Core;

namespace UKSF.Api.ArmaServer.Consumers;

public class ProcessMissionStatsBatchConsumer(
    IMissionStatsService missionStatsService,
    IRawEventStore rawEventStore,
    IEnumerable<IStatsEventProcessor> processors,
    IUksfLogger logger
) : IConsumer<ProcessMissionStatsBatch>
{
    private readonly Dictionary<string, IStatsEventProcessor> _processorsByType = processors.ToDictionary(p => p.EventType);

    public async Task Consume(ConsumeContext<ProcessMissionStatsBatch> context)
    {
        var message = context.Message;

        var events = message.Events.Select(BsonDocument.Parse).ToList();

        var session = await missionStatsService.GetOrCreateSessionAsync(message.SessionId, message.Mission, message.Map, message.ReceivedAt);

        if (session.MissionEnded.HasValue && message.EnqueueAt > session.MissionEnded.Value)
        {
            logger.LogInfo($"Rejecting late batch for session '{message.SessionId}' — enqueueAt {message.EnqueueAt:O} > missionEnded {session.MissionEnded:O}");
            return;
        }

        await rawEventStore.StoreAsync(message.SessionId, events);

        var playerStats = new Dictionary<string, PlayerMissionStats>();
        var missionStats = new MissionStats();

        foreach (var evt in events)
        {
            var eventType = evt.GetValue("type", "unknown").AsString;

            if (eventType is "kill")
            {
                ProcessKillEvent(evt, playerStats);

                if (evt.GetValue("targetType", "unknown").AsString == "vehicle")
                {
                    missionStats.VehiclesDestroyed++;
                }

                continue;
            }

            if (!_processorsByType.TryGetValue(eventType, out var processor))
            {
                continue;
            }

            var uid = evt.Contains("uid") ? evt["uid"].AsString : null;
            if (uid is null)
            {
                continue;
            }

            var stats = GetOrCreatePlayerStats(playerStats, uid);
            processor.ProcessForPlayer(evt, stats);
        }

        var playerUpdateTasks = playerStats.Select(kvp => missionStatsService.UpdatePlayerStatsAsync(session.SessionId, kvp.Key, kvp.Value));
        await Task.WhenAll(playerUpdateTasks);

        if (missionStats.VehiclesDestroyed > 0)
        {
            await missionStatsService.UpdateMissionStatsAsync(session.SessionId, missionStats);
        }
    }

    private void ProcessKillEvent(BsonDocument evt, Dictionary<string, PlayerMissionStats> playerStats)
    {
        var killerUid = evt.GetValue("killerUid", "").AsString;
        if (string.IsNullOrEmpty(killerUid) || !_processorsByType.TryGetValue("kill", out var killProcessor))
        {
            return;
        }

        var killerStats = GetOrCreatePlayerStats(playerStats, killerUid);
        killProcessor.ProcessForPlayer(evt, killerStats);
    }

    private static PlayerMissionStats GetOrCreatePlayerStats(Dictionary<string, PlayerMissionStats> playerStats, string uid)
    {
        if (!playerStats.TryGetValue(uid, out var stats))
        {
            stats = new PlayerMissionStats();
            playerStats[uid] = stats;
        }

        return stats;
    }
}
