using MassTransit;
using MongoDB.Bson;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.ArmaServer.Services.StatsEventProcessors;

namespace UKSF.Api.ArmaServer.Consumers;

public class ProcessMissionStatsBatchConsumer(IMissionStatsService missionStatsService, IEnumerable<IStatsEventProcessor> processors)
    : IConsumer<ProcessMissionStatsBatch>
{
    private readonly Dictionary<string, IStatsEventProcessor> _processorsByType = processors.ToDictionary(p => p.EventType);

    public async Task Consume(ConsumeContext<ProcessMissionStatsBatch> context)
    {
        var message = context.Message;

        var events = message.Events.Select(BsonDocument.Parse).ToList();

        var session = await missionStatsService.GetOrCreateSessionAsync(message.SessionId, message.Mission, message.Map, message.ReceivedAt);
        await missionStatsService.StoreRawBatchAsync(message.SessionId, message.Mission, message.Map, events, message.ReceivedAt);

        var playerStats = new Dictionary<string, PlayerMissionStats>();
        var missionStats = new MissionStats();

        foreach (var evt in events)
        {
            var eventType = evt.GetValue("type", "unknown").AsString;

            missionStats.EventCounts[eventType] = missionStats.EventCounts.GetValueOrDefault(eventType) + 1;

            // Kill events use "killerUid" instead of "uid" and have assists that affect other players
            if (eventType is "kill")
            {
                ProcessKillEvent(evt, playerStats);
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

        var playerUpdateTasks = playerStats.Select(kvp => missionStatsService.UpdatePlayerStatsAsync(session.Id, kvp.Key, kvp.Value));
        await Task.WhenAll(playerUpdateTasks);

        if (missionStats.EventCounts.Count > 0)
        {
            await missionStatsService.UpdateMissionStatsAsync(session.Id, missionStats);
        }
    }

    private void ProcessKillEvent(BsonDocument evt, Dictionary<string, PlayerMissionStats> playerStats)
    {
        // Process the kill for the killer
        var killerUid = evt.GetValue("killerUid", "").AsString;
        if (!string.IsNullOrEmpty(killerUid) && _processorsByType.TryGetValue("kill", out var killProcessor))
        {
            var killerStats = GetOrCreatePlayerStats(playerStats, killerUid);
            killProcessor.ProcessForPlayer(evt, killerStats);
        }

        // Process assists — each assist entry affects a different player
        if (!evt.Contains("assists") || !evt["assists"].IsBsonArray)
        {
            return;
        }

        foreach (var assistEntry in evt["assists"].AsBsonArray)
        {
            if (!assistEntry.IsBsonDocument)
            {
                continue;
            }

            var assistDoc = assistEntry.AsBsonDocument;
            var assistUid = assistDoc.GetValue("uid", "").AsString;
            var assistDamage = assistDoc.GetValue("totalDamage", 0).ToDouble();

            if (string.IsNullOrEmpty(assistUid))
            {
                continue;
            }

            var assistStats = GetOrCreatePlayerStats(playerStats, assistUid);
            assistStats.Kills.Assists++;
            assistStats.Kills.TotalAssistDamage += assistDamage;
        }
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
