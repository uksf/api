using System.Diagnostics;
using UKSF.Api.Core;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Processes;
using UKSF.Api.Core.Services;
using UKSF.Api.Integrations.Teamspeak.Models;
using UKSF.Api.Integrations.Teamspeak.Query;

namespace UKSF.Api.Integrations.Teamspeak.Services;

public interface ITeamspeakManagerService
{
    void Start();
    void Stop();
    void Reload();
    Task Sync(CancellationToken cancellationToken = default);
}

public class TeamspeakManagerService(
    ITeamspeakQuery teamspeakQuery,
    ITeamspeakService teamspeakService,
    ITeamspeakGroupService teamspeakGroupService,
    IProcessUtilities processUtilities,
    IVariablesService variablesService,
    IUksfLogger logger
) : ITeamspeakManagerService
{
    private static readonly TimeSpan SyncInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(30);
    private readonly Lock _lifecycle = new();
    private Dictionary<(int ClientId, int ClientDbId), string> _checkedServerGroups = [];
    private string _publishedClients;
    private int _reloadGeneration;
    private bool _queryFailing;
    private CancellationTokenSource _token;
    private Task _loopTask;

    public void Start()
    {
        if (!variablesService.GetFeatureState("TEAMSPEAK"))
        {
            return;
        }

        lock (_lifecycle)
        {
            if (_loopTask is not null)
            {
                return;
            }

            _token = new CancellationTokenSource();
            var token = _token.Token;
            _loopTask = Task.Factory.StartNew(() => Loop(token), TaskCreationOptions.LongRunning).Unwrap();
        }
    }

    public void Stop()
    {
        lock (_lifecycle)
        {
            if (_loopTask is null)
            {
                return;
            }

            _token.Cancel();
            teamspeakQuery.Disconnect();
            try
            {
                if (!_loopTask.Wait(StopTimeout))
                {
                    logger.LogWarning("Teamspeak sync loop did not stop in time");
                }
            }
            catch (AggregateException exception)
            {
                logger.LogError("Teamspeak sync loop faulted while stopping", exception);
            }

            teamspeakQuery.Disconnect();
            _token.Dispose();
            _token = null;
            _loopTask = null;
        }
    }

    public void Reload()
    {
        Interlocked.Increment(ref _reloadGeneration);
        _checkedServerGroups = [];
        _publishedClients = null;
    }

    public async Task Sync(CancellationToken cancellationToken = default)
    {
        var generation = Volatile.Read(ref _reloadGeneration);
        var clients = await teamspeakQuery.GetClients();
        var channelNames = await teamspeakQuery.GetChannelNames();
        cancellationToken.ThrowIfCancellationRequested();
        await PublishClients(clients, channelNames, generation);
        await CheckChangedServerGroups(clients, generation, cancellationToken);
    }

    private bool StillCurrent(int generation)
    {
        return generation == Volatile.Read(ref _reloadGeneration);
    }

    private async Task Loop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await EnsureServerRunning();
                if (teamspeakService.Enabled)
                {
                    await Sync(token);
                    _queryFailing = false;
                }
            }
            catch (Exception) when (token.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                if (!_queryFailing)
                {
                    logger.LogError("Teamspeak sync failed", exception);
                }

                _queryFailing = true;
            }

            await TaskUtilities.Delay(SyncInterval, token);
        }
    }

    private async Task PublishClients(IReadOnlyList<TeamspeakQueryClient> clients, IReadOnlyDictionary<int, string> channelNames, int generation)
    {
        var teamspeakClients = clients.Select(x => new TeamspeakClient
                                          {
                                              ClientDbId = x.ClientDbId,
                                              ClientName = x.Nickname,
                                              ChannelId = x.ChannelId,
                                              ChannelName = channelNames.GetValueOrDefault(x.ChannelId)
                                          }
                                      )
                                      .ToHashSet();
        var published = string.Join(
            '\n',
            teamspeakClients.Select(x => $"{x.ClientDbId}|{x.ClientName}|{x.ChannelId}|{x.ChannelName}").Order(StringComparer.Ordinal)
        );
        if (published == _publishedClients)
        {
            return;
        }

        await teamspeakService.UpdateClients(teamspeakClients);
        if (StillCurrent(generation))
        {
            _publishedClients = published;
        }
    }

    private async Task CheckChangedServerGroups(IReadOnlyList<TeamspeakQueryClient> clients, int generation, CancellationToken cancellationToken)
    {
        var previous = _checkedServerGroups;
        Dictionary<(int ClientId, int ClientDbId), string> checkedServerGroups = [];
        foreach (var client in clients)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = (client.ClientId, client.ClientDbId);
            var serverGroups = string.Join(',', client.ServerGroups.Order());
            checkedServerGroups[key] = serverGroups;
            if (previous.GetValueOrDefault(key) == serverGroups)
            {
                continue;
            }

            try
            {
                await teamspeakGroupService.CheckClientGroups(client.ClientDbId, client.ServerGroups.ToList());
            }
            catch (Exception exception)
            {
                logger.LogError($"Teamspeak server group check failed for client {client.ClientDbId}", exception);
                checkedServerGroups.Remove(key);
            }
        }

        if (StillCurrent(generation))
        {
            _checkedServerGroups = checkedServerGroups;
        }
    }

    private async Task EnsureServerRunning()
    {
        if (!variablesService.GetVariable("TEAMSPEAK_SERVER_RUN").AsBool() || processUtilities.GetProcessesByName("ts3server").Length > 0)
        {
            return;
        }

        var serverPath = variablesService.GetVariable("TEAMSPEAK_SERVER_PATH").AsString();
        if (!OperatingSystem.IsWindows())
        {
            processUtilities.LaunchManagedProcess(serverPath, "license_accepted=1", ProcessPriorityClass.AboveNormal);
            return;
        }

        await processUtilities.LaunchExternalProcess("TeamspeakServer", $"start \"\" /abovenormal \"{serverPath}\"", Path.GetDirectoryName(serverPath));
    }
}
