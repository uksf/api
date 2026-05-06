using System.Globalization;
using System.Text.Json;
using MassTransit;
using MongoDB.Driver;
using UKSF.Api.ArmaServer.Consumers;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core;
using static UKSF.Api.ArmaServer.Converters.PersistenceConversionHelpers;

namespace UKSF.Api.ArmaServer.Services;

public interface IGameServerEventHandler
{
    Task HandleEventAsync(GameServerEvent gameServerEvent);
}

public class GameServerEventHandler(
    IGameServerProcessManager processManager,
    IGameServersContext gameServersContext,
    IPublishEndpoint publishEndpoint,
    IMissionStatsService missionStatsService,
    IPerformanceService performanceService,
    IPersistenceSessionsService persistenceSessionsService,
    IUksfLogger logger
) : IGameServerEventHandler
{
    public async Task HandleEventAsync(GameServerEvent gameServerEvent)
    {
        // Synthetic launches (config export, dev run) reuse the production -apiUrl pipeline.
        // server_status / shutdown_complete reference a gameServers row keyed by apiPort and
        // synthetic ports never have one — gate ONLY those two off. Other event types
        // (mission_stats, performance, persistence_save, mission lifecycle, player presence)
        // are content-driven and have no port dependency, so they should still flow through
        // for synthetic runs (e.g. dev-test-server e2e validation of the persistence pipeline).
        var isSynthetic = SyntheticApiPorts.IsSynthetic(gameServerEvent.ApiPort);

        try
        {
            switch (gameServerEvent.Type)
            {
                case "server_status":
                    if (!isSynthetic) await processManager.HandleServerStatusAsync(gameServerEvent.ApiPort, gameServerEvent.Data);
                    break;
                case "shutdown_complete":
                    if (!isSynthetic) await processManager.HandleShutdownCompleteAsync(gameServerEvent.ApiPort);
                    break;
                case "mission_stats":       await HandleMissionStatsEvent(gameServerEvent.Data); break;
                case "mission_started":     await HandleMissionLifecycleEvent(gameServerEvent.ApiPort, gameServerEvent.Data, true); break;
                case "mission_ended":       await HandleMissionLifecycleEvent(gameServerEvent.ApiPort, gameServerEvent.Data, false); break;
                case "player_connected":    await HandlePlayerPresenceEvent(gameServerEvent.Data, isConnected: true); break;
                case "player_disconnected": await HandlePlayerPresenceEvent(gameServerEvent.Data, isConnected: false); break;
                case "performance":         await HandlePerformanceEvent(gameServerEvent.Data); break;
                case "persistence_save":    await HandlePersistenceSaveEvent(gameServerEvent.Data); break;
                default:                    logger.LogWarning($"Unknown game server event type: {gameServerEvent.Type}"); break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError($"Error handling game server event: {gameServerEvent.Type}", ex);
            if (gameServerEvent.Type == "persistence_save")
            {
                throw;
            }
        }
    }

    private async Task HandleMissionStatsEvent(Dictionary<string, object> data)
    {
        var sessionId = data.TryGetValue("sessionId", out var sessionIdValue) ? sessionIdValue.ToString() : string.Empty;
        var mission = data.TryGetValue("mission", out var missionValue) ? missionValue.ToString() : string.Empty;
        var map = data.TryGetValue("map", out var mapValue) ? mapValue.ToString() : string.Empty;

        if (string.IsNullOrEmpty(sessionId) || string.IsNullOrEmpty(mission) || string.IsNullOrEmpty(map))
        {
            logger.LogWarning("mission_stats event missing sessionId, mission, or map");
            return;
        }

        // Each event in the buffer is a parsed hashmap (List<object> pair-list normalised
        // to Dictionary<string,object>). Re-serialise to JSON for the BsonDocument-based
        // batch consumer, which already expects JSON strings.
        var events = new List<string>();
        if (data.TryGetValue("events", out var eventsObj) && eventsObj is List<object> eventList)
        {
            events.EnsureCapacity(eventList.Count);
            foreach (var entry in eventList)
            {
                events.Add(JsonSerializer.Serialize(entry));
            }
        }

        var receivedAt = DateTime.UtcNow;
        var enqueueAt = receivedAt;
        if (data.TryGetValue("enqueueAt", out var enqueueAtValue) &&
            enqueueAtValue is string enqueueAtString &&
            DateTime.TryParse(enqueueAtString, null, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsedEnqueue))
        {
            enqueueAt = parsedEnqueue;
        }

        await publishEndpoint.Publish(
            new ProcessMissionStatsBatch
            {
                SessionId = sessionId,
                Mission = mission,
                Map = map,
                Events = events,
                ReceivedAt = receivedAt,
                EnqueueAt = enqueueAt
            }
        );
    }

    private async Task HandleMissionLifecycleEvent(int apiPort, Dictionary<string, object> data, bool isStart)
    {
        var sessionId = data.TryGetValue("sessionId", out var sessionIdValue) ? sessionIdValue.ToString() : string.Empty;
        if (string.IsNullOrEmpty(sessionId))
        {
            return;
        }

        var gameServer = gameServersContext.GetSingle(x => x.ApiPort == apiPort);
        if (gameServer is not null)
        {
            var newSessionId = isStart ? sessionId : null;
            await gameServersContext.Update(gameServer.Id, Builders<DomainGameServer>.Update.Set(x => x.Status.CurrentMissionSessionId, newSessionId));
        }

        var now = DateTime.UtcNow;

        if (isStart)
        {
            var mission = data.TryGetValue("mission", out var missionValue) ? missionValue.ToString() : string.Empty;
            var map = data.TryGetValue("map", out var mapValue) ? mapValue.ToString() : string.Empty;
            if (string.IsNullOrEmpty(mission) || string.IsNullOrEmpty(map))
            {
                return;
            }

            await missionStatsService.HandleMissionStartedAsync(sessionId, mission, map, now);
        }
        else
        {
            var duration = data.TryGetValue("duration", out var durationValue) && double.TryParse(durationValue?.ToString(), out var parsed) ? parsed : 0;
            await missionStatsService.HandleMissionEndedAsync(sessionId, duration, now);
        }
    }

    private async Task HandlePlayerPresenceEvent(Dictionary<string, object> data, bool isConnected)
    {
        var sessionId = data.TryGetValue("sessionId", out var sessionIdValue) ? sessionIdValue.ToString() : string.Empty;
        var uid = data.TryGetValue("uid", out var uidValue) ? uidValue.ToString() : string.Empty;

        if (string.IsNullOrEmpty(sessionId) || string.IsNullOrEmpty(uid))
        {
            return;
        }

        var now = DateTime.UtcNow;

        if (isConnected)
        {
            var name = data.TryGetValue("name", out var nameValue) ? nameValue.ToString() : string.Empty;
            await missionStatsService.HandlePlayerConnectedAsync(sessionId, uid, name, now);
        }
        else
        {
            await missionStatsService.HandlePlayerDisconnectedAsync(sessionId, uid, now);
        }
    }

    private async Task HandlePerformanceEvent(Dictionary<string, object> data)
    {
        var sessionId = data.TryGetValue("sessionId", out var sessionIdValue) ? sessionIdValue.ToString() : string.Empty;
        if (string.IsNullOrEmpty(sessionId))
        {
            return;
        }

        var serverFps = ToIntList(data.GetValueOrDefault("server"));

        var headlessClients = new List<HeadlessClientPerformance>();
        if (data.TryGetValue("headlessClients", out var hcValue) && hcValue is List<object> hcList)
        {
            foreach (var entry in hcList)
            {
                var hc = ToDict(entry);
                var name = hc.GetValueOrDefault("name")?.ToString() ?? string.Empty;
                var fps = ToIntList(hc.GetValueOrDefault("fps"));
                if (!string.IsNullOrEmpty(name) && fps.Count > 0)
                {
                    headlessClients.Add(new HeadlessClientPerformance { Name = name, Fps = fps });
                }
            }
        }

        var players = new List<PlayerPerformance>();
        if (data.TryGetValue("players", out var playersValue) && playersValue is List<object> playerList)
        {
            foreach (var entry in playerList)
            {
                var player = ToDict(entry);
                var uid = player.GetValueOrDefault("uid")?.ToString() ?? string.Empty;
                var fps = ToIntList(player.GetValueOrDefault("fps"));
                if (!string.IsNullOrEmpty(uid) && fps.Count > 0)
                {
                    players.Add(new PlayerPerformance { Uid = uid, Fps = fps });
                }
            }
        }

        await performanceService.HandlePerformanceEventAsync(sessionId, serverFps, headlessClients, players);
    }

    private async Task HandlePersistenceSaveEvent(Dictionary<string, object> data)
    {
        var key = data.GetValueOrDefault("key")?.ToString() ?? string.Empty;
        var sessionId = data.GetValueOrDefault("sessionId")?.ToString() ?? string.Empty;
        var sessionData = data.GetValueOrDefault("data");

        logger.LogInfo($"persistence_save received: key='{key}', sessionId='{sessionId}', dataKeys=[{string.Join(",", data.Keys)}]");

        if (string.IsNullOrEmpty(key) || sessionData is null)
        {
            logger.LogWarning($"persistence_save event missing key or data (key='{key}', hasData={sessionData is not null})");
            return;
        }

        await persistenceSessionsService.HandleSaveAsync(key, sessionId, sessionData);
        logger.LogInfo($"persistence_save handled: key='{key}'");
    }

    private static List<int> ToIntList(object value)
    {
        if (value is not List<object> list) return [];
        var result = new List<int>(list.Count);
        foreach (var entry in list)
        {
            switch (entry)
            {
                case long l:   result.Add((int)l); break;
                case int i:    result.Add(i); break;
                case double d: result.Add((int)d); break;
            }
        }

        return result;
    }
}
