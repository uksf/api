using System.Text.Json;
using MassTransit;
using MongoDB.Driver;
using UKSF.Api.ArmaServer.Consumers;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core;

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
        // Synthetic launches (config export, dev run) reuse the production -apiUrl pipeline so
        // the engine emits server_status / shutdown_complete tagged with their reserved apiPorts.
        // None of those ports map to a row in gameServers, so handling would only spam warnings.
        if (SyntheticApiPorts.IsSynthetic(gameServerEvent.ApiPort))
        {
            return;
        }

        try
        {
            switch (gameServerEvent.Type)
            {
                case "server_status":       await processManager.HandleServerStatusAsync(gameServerEvent.ApiPort, gameServerEvent.Data); break;
                case "shutdown_complete":   await processManager.HandleShutdownCompleteAsync(gameServerEvent.ApiPort); break;
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

        var events = new List<string>();
        if (data.TryGetValue("events", out var eventsObj) && eventsObj is JsonElement jsonElement)
        {
            events.EnsureCapacity(jsonElement.GetArrayLength());
            foreach (var element in jsonElement.EnumerateArray())
            {
                events.Add(element.GetRawText());
            }
        }

        var receivedAt = DateTime.UtcNow;
        var enqueueAt = receivedAt;
        if (data.TryGetValue("enqueueAt", out var enqueueAtValue) && enqueueAtValue is not null)
        {
            if (enqueueAtValue is DateTime dt)
            {
                enqueueAt = dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime();
            }
            else if (enqueueAtValue is JsonElement enqueueElement &&
                     enqueueElement.ValueKind == JsonValueKind.String &&
                     DateTime.TryParse(
                         enqueueElement.GetString(),
                         null,
                         System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                         out var parsedEnqueue
                     ))
            {
                enqueueAt = parsedEnqueue;
            }
            else if (enqueueAtValue is string s &&
                     DateTime.TryParse(
                         s,
                         null,
                         System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                         out var parsedString
                     ))
            {
                enqueueAt = parsedString;
            }
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

        var serverFps = new List<int>();
        if (data.TryGetValue("server", out var serverValue) && serverValue is JsonElement serverElement && serverElement.ValueKind == JsonValueKind.Array)
        {
            serverFps = serverElement.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.Number).Select(e => e.GetInt32()).ToList();
        }

        var headlessClients = new List<HeadlessClientPerformance>();
        if (data.TryGetValue("headlessClients", out var hcValue) && hcValue is JsonElement hcElement && hcElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var hc in hcElement.EnumerateArray())
            {
                var name = hc.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : string.Empty;
                var fps = hc.TryGetProperty("fps", out var fpsProp) && fpsProp.ValueKind == JsonValueKind.Array
                    ? fpsProp.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.Number).Select(e => e.GetInt32()).ToList()
                    : [];
                if (!string.IsNullOrEmpty(name) && fps.Count > 0)
                {
                    headlessClients.Add(new HeadlessClientPerformance { Name = name, Fps = fps });
                }
            }
        }

        var players = new List<PlayerPerformance>();
        if (data.TryGetValue("players", out var playersValue) && playersValue is JsonElement playersElement && playersElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var player in playersElement.EnumerateArray())
            {
                var uid = player.TryGetProperty("uid", out var uidProp) ? uidProp.GetString() : string.Empty;
                var fps = player.TryGetProperty("fps", out var fpsProp) && fpsProp.ValueKind == JsonValueKind.Array
                    ? fpsProp.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.Number).Select(e => e.GetInt32()).ToList()
                    : [];
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
        var json = data.GetValueOrDefault("data")?.ToString() ?? string.Empty;

        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(json))
        {
            logger.LogWarning($"persistence_save event missing key or data (key='{key}', dataLength={json.Length})");
            return;
        }

        await persistenceSessionsService.HandleSaveAsync(key, sessionId, json);
    }
}
