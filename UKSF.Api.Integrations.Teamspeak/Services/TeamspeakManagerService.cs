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
    Task Sync();
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
    private Dictionary<int, string> _checkedServerGroups = [];
    private string _publishedClients;
    private bool _queryFailing;
    private CancellationTokenSource _token;
    private Task _loopTask;

    public void Start()
    {
        if (!variablesService.GetFeatureState("TEAMSPEAK"))
        {
            return;
        }

        _token = new CancellationTokenSource();
        _loopTask = Task.Factory.StartNew(Loop, TaskCreationOptions.LongRunning).Unwrap();
    }

    public void Stop()
    {
        if (_token is null)
        {
            return;
        }

        _token.Cancel();
        try
        {
            _loopTask?.Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException exception) when (exception.InnerExceptions.All(x => x is OperationCanceledException)) { }

        teamspeakQuery.Disconnect();
        _token.Dispose();
        _token = null;
    }

    public void Reload()
    {
        _checkedServerGroups = [];
        _publishedClients = null;
    }

    public async Task Sync()
    {
        var clients = await teamspeakQuery.GetClients();
        var channelNames = await teamspeakQuery.GetChannelNames();
        await PublishClients(clients, channelNames);
        await CheckChangedServerGroups(clients);
    }

    private async Task Loop()
    {
        while (!_token.IsCancellationRequested)
        {
            try
            {
                await EnsureServerRunning();
                if (teamspeakService.Enabled)
                {
                    await Sync();
                    _queryFailing = false;
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !_token.IsCancellationRequested)
            {
                if (!_queryFailing)
                {
                    logger.LogError("Teamspeak sync failed", exception);
                }

                _queryFailing = true;
            }

            await TaskUtilities.Delay(SyncInterval, _token.Token);
        }
    }

    private async Task PublishClients(IReadOnlyList<TeamspeakQueryClient> clients, IReadOnlyDictionary<int, string> channelNames)
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
        _publishedClients = published;
    }

    private async Task CheckChangedServerGroups(IReadOnlyList<TeamspeakQueryClient> clients)
    {
        Dictionary<int, string> checkedServerGroups = [];
        foreach (var client in clients)
        {
            var serverGroups = string.Join(',', client.ServerGroups.Order());
            checkedServerGroups[client.ClientId] = serverGroups;
            if (_checkedServerGroups.GetValueOrDefault(client.ClientId) == serverGroups)
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
                checkedServerGroups.Remove(client.ClientId);
            }
        }

        _checkedServerGroups = checkedServerGroups;
    }

    private async Task EnsureServerRunning()
    {
        if (!variablesService.GetVariable("TEAMSPEAK_SERVER_RUN").AsBool() || processUtilities.GetProcessesByName("ts3server").Length > 0)
        {
            return;
        }

        var serverPath = variablesService.GetVariable("TEAMSPEAK_SERVER_PATH").AsString();
        await processUtilities.LaunchExternalProcess("TeamspeakServer", $"start \"\" \"{serverPath}\"", Path.GetDirectoryName(serverPath));
    }
}
