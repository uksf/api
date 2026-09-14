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
        if (armaProcesses.Count == 0)
        {
            await Task.WhenAll(gameServers.Select(server => ReconcileServerSafely(server, () => ResetServerToDeadAsync(server, push: false))));
            return gameServers;
        }

        await Task.WhenAll(gameServers.Select(server => ReconcileServerSafely(server, () => UpdateServerStatus(server, armaProcesses))));
        return gameServers;
    }

    // Poll-path reconciliation runs under the same per-server lock the monitor uses, so poll and monitor
    // cannot concurrently reset the same server (which would double-finalise its session). WaitAsync(0)
    // skips a server another operation already owns; per-server try/catch keeps one failure from failing
    // the whole poll batch.
    private async Task ReconcileServerSafely(DomainGameServer gameServer, Func<Task> reconcile)
    {
        var serverLock = GetServerLock(gameServer.Id);
        if (!await serverLock.WaitAsync(0))
        {
            return;
        }

        try
        {
            await reconcile();
        }
        catch (Exception exception)
        {
            logger.LogError($"Error reconciling status for server '{gameServer.Name}'", exception);
        }
        finally
        {
            serverLock.Release();
        }
    }

    public Task HandleStopEndingAsync(int apiPort) => AdvanceStopPhaseAsync(apiPort, StopPhase.Ending, "shutdown_ending");
    public Task HandleStopSavingAsync(int apiPort) => AdvanceStopPhaseAsync(apiPort, StopPhase.Saving, "shutdown_saving");
    public Task HandleStopStoppingAsync(int apiPort) => AdvanceStopPhaseAsync(apiPort, StopPhase.Stopping, "shutdown_stopping");

    private async Task AdvanceStopPhaseAsync(int apiPort, StopPhase phase, string eventName)
    {
        var gameServer = gameServersContext.GetSingle(x => x.ApiPort == apiPort);
        if (gameServer is null)
        {
            logger.LogWarning($"Received {eventName} but no server matches apiPort {apiPort}");
            return;
        }

        var serverLock = GetServerLock(gameServer.Id);
        await serverLock.WaitAsync();
        try
        {
            if (phase < gameServer.Status.StopPhase)
            {
                return; // never regress a stop (late/duplicated event racing a later phase)
            }

            var now = DateTime.UtcNow;
            gameServer.Status.StopRequestedAt ??= now; // in-game trigger: API never set it on a stop press
            gameServer.Status.StopPhase = phase;
            gameServer.Status.StopPhaseEnteredAt = now; // arms the per-stage watchdog
            gameServer.Status.KillAllowedAt = StopPhaseWatchdog.KillOfferAt(gameServer.Status);
            await gameServersContext.Replace(gameServer);
            await PushServerUpdateAsync(gameServer);
        }
        finally
        {
            serverLock.Release();
        }
    }

    public async Task HandleServerStatusAsync(int apiPort, Dictionary<string, object> data)
    {
        var gameServer = gameServersContext.GetSingle(x => x.ApiPort == apiPort);
        if (gameServer is null)
        {
            logger.LogWarning($"Received server_status but no server matches apiPort {apiPort}");
            return;
        }

        var serverLock = GetServerLock(gameServer.Id);
        await serverLock.WaitAsync();
        try
        {
            var status = StatusCache.GetOrAdd(gameServer.Id, _ => gameServer.Status ?? new GameServerStatus());
            if (gameServer.Status.StopPhase != StopPhase.None)
            {
                return;
            }

            ApplyStatusFields(status, data);

            if (!(data.TryGetValue("players", out var playersRaw) && playersRaw is List<object>))
            {
                logger.LogWarning($"server_status 'players' missing or not a list. Keys: {string.Join(", ", data.Keys)}");
            }

            if (status.StartedAt is null && data.TryGetValue("uptime", out var uptimeRaw) &&
                float.TryParse(uptimeRaw?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var startUptime))
            {
                status.StartedAt = DateTime.UtcNow.AddSeconds(-startUptime);
            }

            status.Running = true;
            status.Launching = false;
            status.MaxPlayers = gameServerHelpers.GetMaxPlayerCountFromConfig(gameServer);
            status.LastEventReceived = DateTime.UtcNow;

            gameServer.Status = status;
            await gameServersContext.Replace(gameServer);

            await PushServerUpdateAsync(gameServer);
        }
        finally
        {
            serverLock.Release();
        }
    }

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
        if (data.TryGetValue("aiCount", out var aiCount) && int.TryParse(aiCount?.ToString(), out var aiCountValue))
            status.AiCount = aiCountValue;
        if (data.TryGetValue("headlessClientCount", out var headlessClientCount) &&
            int.TryParse(headlessClientCount?.ToString(), out var headlessClientCountValue))
            status.HeadlessClientCount = headlessClientCountValue;
    }

    private void ApplyPolledStatus(DomainGameServer gameServer, string sqfBody)
    {
        // Empty body during the boot race (extension up but no server_status seen yet)
        // is expected — treat as "no data, try again next poll" rather than parse failure.
        if (string.IsNullOrWhiteSpace(sqfBody))
        {
            return;
        }

        // Body is engine-native SQF str() of the server_status data hashmap (pair-list).
        // Parse to a Dictionary<string,object> then map fields onto the in-memory status.
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
        UpdateHeadlessClientProcessIds(gameServer, armaProcesses);

        if (matchingProcess is null)
        {
            await HandleProcessGone(gameServer);
            return;
        }

        gameServer.ProcessId = matchingProcess.ProcessId;
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

    private static void UpdateHeadlessClientProcessIds(DomainGameServer gameServer, IReadOnlyList<ProcessCommandLineInfo> armaProcesses)
    {
        gameServer.HeadlessClientProcessIds = armaProcesses.Where(p => MatchesPort(p, gameServer.Port) && p.CommandLine.Contains("-client"))
                                                           .Select(p => p.ProcessId)
                                                           .ToList();
    }

    private static bool MatchesPort(ProcessCommandLineInfo process, int port)
    {
        var portArg = $"-port={port} ";
        var portArgEnd = $"-port={port}";
        return process.CommandLine.Contains(portArg) || process.CommandLine.EndsWith(portArgEnd);
    }
}
