using System.Globalization;
using System.Net.Http.Headers;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Parsing;
using UKSF.Api.Core.Processes;
using static UKSF.Api.ArmaServer.Converters.PersistenceConversionHelpers;

namespace UKSF.Api.ArmaServer.Services;

public partial class GameServerProcessManager
{
    public async Task<List<DomainGameServer>> GetAllServerStatusesAsync()
    {
        var gameServers = gameServersContext.Get().ToList();

        if (variablesService.GetFeatureState("SKIP_SERVER_STATUS"))
        {
            return gameServers;
        }

        var armaProcesses = gameServerHelpers.GetGameServerArmaProcesses();
        await Task.WhenAll(gameServers.Select(server => ReconcileServerSafely(server, armaProcesses)));
        return gameServers.Select(server => gameServersContext.GetSingle(server.Id) ?? server).ToList();
    }

    private async Task ReconcileServerSafely(DomainGameServer snapshot, IReadOnlyList<ProcessCommandLineInfo> armaProcesses)
    {
        try
        {
            await WithCurrentServerAsync(
                snapshot.Id,
                server => armaProcesses.Count == 0 ? ResetServerToDeadAsync(server, push: false, armaProcesses) : UpdateServerStatus(server, armaProcesses),
                waitForLock: false
            );
        }
        catch (Exception exception)
        {
            logger.LogError($"Error reconciling status for server '{snapshot.Name}'", exception);
        }
    }

    public Task HandleStopEndingAsync(int apiPort, DateTime? enqueuedAt = null) =>
        AdvanceStopPhaseAsync(apiPort, StopPhase.Ending, "shutdown_ending", enqueuedAt);

    public Task HandleStopSavingAsync(int apiPort, DateTime? enqueuedAt = null) =>
        AdvanceStopPhaseAsync(apiPort, StopPhase.Saving, "shutdown_saving", enqueuedAt);

    public Task HandleStopDeliveringAsync(int apiPort, DateTime? enqueuedAt = null) =>
        AdvanceStopPhaseAsync(apiPort, StopPhase.Delivering, "shutdown_delivering", enqueuedAt);

    public Task HandleStopStoppingAsync(int apiPort, DateTime? enqueuedAt = null) =>
        AdvanceStopPhaseAsync(apiPort, StopPhase.Stopping, "shutdown_stopping", enqueuedAt);

    private Task AdvanceStopPhaseAsync(int apiPort, StopPhase phase, string eventName, DateTime? enqueuedAt) =>
        WithCurrentServerAsync(
            apiPort,
            eventName,
            async gameServer =>
            {
                if (PredatesProcess(gameServer, enqueuedAt))
                {
                    logger.LogWarning($"Ignoring {eventName} for '{gameServer.Name}': it was sent before the current process started");
                    return;
                }

                var rank = StopPhaseWatchdog.Rank(phase);
                var currentRank = StopPhaseWatchdog.Rank(gameServer.Status.StopPhase);
                if (rank < currentRank || (rank == currentRank && gameServer.Status.StopPhaseEnteredAt is not null))
                {
                    return;
                }

                var now = DateTime.UtcNow;
                gameServer.Status.StopRequestedAt ??= now;
                gameServer.Status.StopPhase = phase;
                gameServer.Status.StopPhaseEnteredAt = now;
                gameServer.Status.KillAllowedAt = StopPhaseWatchdog.KillOfferAt(gameServer.Status);
                await gameServersContext.Replace(gameServer);
                await PushServerUpdateAsync(gameServer);
            }
        );

    public Task RecordMissionSessionAsync(int apiPort, string sessionId, bool started, DateTime? enqueuedAt = null) =>
        WithCurrentServerAsync(
            apiPort,
            started ? "mission_started" : "mission_ended",
            async gameServer =>
            {
                if (PredatesProcess(gameServer, enqueuedAt))
                {
                    return;
                }

                var current = gameServer.Status.CurrentMissionSessionId;
                var next = started       ? sessionId :
                    current == sessionId ? null : current;
                if (next == current)
                {
                    return;
                }

                gameServer.Status.CurrentMissionSessionId = next;
                if (StatusCache.TryGetValue(gameServer.Id, out var cached))
                {
                    cached.CurrentMissionSessionId = next;
                }

                await gameServersContext.Replace(gameServer);
            }
        );

    public Task HandleServerStatusAsync(int apiPort, Dictionary<string, object> data) =>
        WithCurrentServerAsync(
            apiPort,
            "server_status",
            async gameServer =>
            {
                if (gameServer.Status.StopPhase != StopPhase.None)
                {
                    return;
                }

                var status = StatusCache.GetOrAdd(gameServer.Id, _ => gameServer.Status ?? new GameServerStatus());
                ApplyStatusFields(status, data);

                if (!(data.TryGetValue("players", out var playersRaw) && playersRaw is List<object>))
                {
                    logger.LogWarning($"server_status 'players' missing or not a list. Keys: {string.Join(", ", data.Keys)}");
                }

                if (status.StartedAt is null &&
                    data.TryGetValue("uptime", out var uptimeRaw) &&
                    float.TryParse(uptimeRaw?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var startUptime))
                {
                    status.StartedAt = DateTime.UtcNow.AddSeconds(-startUptime);
                }

                status.Running = true;
                status.Launching = false;
                status.MaxPlayers = gameServerHelpers.GetMaxPlayerCountFromConfig(gameServer);
                status.LastEventReceived = DateTime.UtcNow;
                status.CurrentMissionSessionId = gameServer.Status.CurrentMissionSessionId;

                gameServer.Status = status;
                await gameServersContext.Replace(gameServer);
                await PushServerUpdateAsync(gameServer);
            }
        );

    private void ApplyStatusFields(GameServerStatus status, IReadOnlyDictionary<string, object> data)
    {
        if (data.TryGetValue("map", out var map)) status.Map = map?.ToString();
        if (data.TryGetValue("mission", out var mission)) status.Mission = mission?.ToString();
        if (data.TryGetValue("players", out var players) && players is List<object> playersList)
        {
            status.Players = playersList.OfType<string>().ToList();
        }

        if (data.TryGetValue("uptime", out var uptime) &&
            float.TryParse(uptime?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var uptimeValue))
        {
            status.Uptime = uptimeValue;
            status.ParsedUptime = gameServerHelpers.StripMilliseconds(TimeSpan.FromSeconds(uptimeValue)).ToString();
        }

        if (data.TryGetValue("entityCount", out var entityCount) && int.TryParse(entityCount?.ToString(), out var entityCountValue))
            status.EntityCount = entityCountValue;
        if (data.TryGetValue("aiCount", out var aiCount) && int.TryParse(aiCount?.ToString(), out var aiCountValue)) status.AiCount = aiCountValue;
        if (data.TryGetValue("headlessClientCount", out var headlessClientCount) &&
            int.TryParse(headlessClientCount?.ToString(), out var headlessClientCountValue)) status.HeadlessClientCount = headlessClientCountValue;
    }

    private void ApplyPolledStatus(DomainGameServer gameServer, string sqfBody)
    {
        if (string.IsNullOrWhiteSpace(sqfBody))
        {
            return;
        }

        Dictionary<string, object> polled;
        try
        {
            polled = ToDict(SqfNotationParser.ParseAndNormalize(sqfBody));
        }
        catch (FormatException exception)
        {
            logger.LogWarning($"Failed to parse polled server status SQF for '{gameServer.Name}': {exception.Message}");
            return;
        }

        ApplyStatusFields(gameServer.Status, polled);

        gameServer.Status.MaxPlayers = gameServerHelpers.GetMaxPlayerCountFromConfig(gameServer);
        gameServer.Status.Running = true;
        gameServer.Status.Launching = false;
    }

    private async Task UpdateServerStatus(DomainGameServer gameServer, IReadOnlyList<ProcessCommandLineInfo> armaProcesses)
    {
        var matchingProcess = FindMatchingServerProcess(gameServer, armaProcesses);
        if (matchingProcess is null)
        {
            await HandleProcessGone(gameServer, armaProcesses);
            return;
        }

        var tracked = gameServer.ProcessId == matchingProcess.ProcessId ? ServerProcessOf(gameServer) : Track(matchingProcess.ProcessId);
        if (tracked is null)
        {
            await HandleProcessGone(gameServer, armaProcesses);
            return;
        }

        gameServer.ProcessId = tracked.ProcessId;
        gameServer.ProcessStartedAt = tracked.StartedAt;
        gameServer.HeadlessClients = armaProcesses.Where(p => MatchesPort(p, gameServer.Port) && p.CommandLine.Contains("-client"))
                                                  .Select(p => gameServer.HeadlessClients.FirstOrDefault(known => known.ProcessId == p.ProcessId) ??
                                                               Track(p.ProcessId)
                                                  )
                                                  .Where(process => process is not null)
                                                  .ToList();
        gameServer.Status.Launching = true;

        using var client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));
        client.Timeout = TimeSpan.FromSeconds(5);
        try
        {
            var response = await client.GetAsync($"http://127.0.0.1:{gameServer.ApiPort}/server");
            if (!response.IsSuccessStatusCode)
            {
                var statusCode = (int)response.StatusCode;
                if (statusCode is 502 or 504)
                {
                    gameServer.Status.Running = false;
                }
                else if (statusCode == 503) { }
                else
                {
                    var body = await response.Content.ReadAsStringAsync();
                    logger.LogWarning($"Status endpoint returned {statusCode} for '{gameServer.Name}': {body}");
                }
            }
            else
            {
                var content = await response.Content.ReadAsStringAsync();
                ApplyPolledStatus(gameServer, content);
            }
        }
        catch (HttpRequestException)
        {
            gameServer.Status.Running = false;
        }
        catch (TaskCanceledException)
        {
            gameServer.Status.Running = false;
        }
        catch (Exception exception)
        {
            logger.LogError($"Unexpected error getting game server status for '{gameServer.Name}'", exception);
            gameServer.Status.Running = false;
        }

        if (StatusCache.TryGetValue(gameServer.Id, out var cachedStatus) && cachedStatus.LastEventReceived > DateTime.UtcNow.AddSeconds(-30))
        {
            gameServer.Status.Map = cachedStatus.Map;
            gameServer.Status.Mission = cachedStatus.Mission;
            gameServer.Status.Players = cachedStatus.Players;
            gameServer.Status.Uptime = cachedStatus.Uptime;
            gameServer.Status.ParsedUptime = cachedStatus.ParsedUptime;
            gameServer.Status.StartedAt = cachedStatus.StartedAt;
            gameServer.Status.EntityCount = cachedStatus.EntityCount;
            gameServer.Status.AiCount = cachedStatus.AiCount;
            gameServer.Status.HeadlessClientCount = cachedStatus.HeadlessClientCount;
            gameServer.Status.MaxPlayers = cachedStatus.MaxPlayers;
            gameServer.Status.Running = cachedStatus.Running;
            gameServer.Status.Launching = cachedStatus.Launching;
            gameServer.Status.LastEventReceived = cachedStatus.LastEventReceived;
        }

        await gameServersContext.Replace(gameServer);
    }

    private static ProcessCommandLineInfo FindMatchingServerProcess(DomainGameServer gameServer, IReadOnlyList<ProcessCommandLineInfo> armaProcesses)
    {
        return armaProcesses.FirstOrDefault(p => MatchesPort(p, gameServer.Port) && p.CommandLine.Contains("-config=") && !p.CommandLine.Contains("-client"));
    }

    private static bool MatchesPort(ProcessCommandLineInfo process, int port)
    {
        var portArg = $"-port={port} ";
        var portArgEnd = $"-port={port}";
        return process.CommandLine.Contains(portArg) || process.CommandLine.EndsWith(portArgEnd);
    }
}
