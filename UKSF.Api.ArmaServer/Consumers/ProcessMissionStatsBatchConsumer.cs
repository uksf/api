using MassTransit;
using MongoDB.Bson;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.ArmaServer.Services.StatsEventProcessors;
using UKSF.Api.Core;

namespace UKSF.Api.ArmaServer.Consumers;

public class ProcessMissionStatsBatchConsumer(IMissionStatsService missionStatsService, IEnumerable<IStatsEventProcessor> processors, IUksfLogger logger)
    : IConsumer<ProcessMissionStatsBatch>
{
    private readonly Dictionary<string, IStatsEventProcessor> _processorsByType = processors.ToDictionary(p => p.EventType);

    public async Task Consume(ConsumeContext<ProcessMissionStatsBatch> context)
    {
        var message = context.Message;

        var session = await missionStatsService.FindOrCreateSessionAsync(message.Mission, message.Map, message.ReceivedAt);
        await missionStatsService.StoreRawBatchAsync(session.Id, message.Mission, message.Map, message.Events, message.ReceivedAt);

        var playerStats = new Dictionary<string, PlayerMissionStats>();
        var missionStats = new MissionStats();

        foreach (var evt in message.Events)
        {
            var eventType = evt.GetValue("type", "unknown").AsString;

            missionStats.EventCounts[eventType] = missionStats.EventCounts.GetValueOrDefault(eventType) + 1;

            if (!_processorsByType.TryGetValue(eventType, out var processor))
            {
                logger.LogDebug($"No processor registered for event type '{eventType}'");
                continue;
            }

            var uid = evt.Contains("uid") ? evt["uid"].AsString : null;
            if (uid is null)
            {
                continue;
            }

            if (!playerStats.TryGetValue(uid, out var stats))
            {
                stats = new PlayerMissionStats();
                playerStats[uid] = stats;
            }

            processor.ProcessForPlayer(evt, stats);
        }

        foreach (var (uid, stats) in playerStats)
        {
            await missionStatsService.UpdatePlayerStatsAsync(session.Id, uid, stats);
        }

        if (missionStats.EventCounts.Count > 0)
        {
            await missionStatsService.UpdateMissionStatsAsync(session.Id, missionStats);
        }
    }
}
