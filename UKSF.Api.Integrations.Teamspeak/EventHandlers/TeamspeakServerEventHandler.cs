using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models;
using UKSF.Api.Integrations.Teamspeak.Models;
using UKSF.Api.Integrations.Teamspeak.Services;

namespace UKSF.Api.Integrations.Teamspeak.EventHandlers;

public interface ITeamspeakServerEventHandler : IEventHandler;

public class TeamspeakServerEventHandler : ITeamspeakServerEventHandler
{
    private readonly IAccountContext _accountContext;
    private readonly IEventBus _eventBus;
    private readonly IUksfLogger _logger;
    private readonly ConcurrentDictionary<int, TeamspeakServerGroupUpdate> _serverGroupUpdates = new();
    private readonly ITeamspeakGroupService _teamspeakGroupService;
    private readonly ITeamspeakService _teamspeakService;

    public TeamspeakServerEventHandler(
        IAccountContext accountContext,
        IEventBus eventBus,
        ITeamspeakService teamspeakService,
        ITeamspeakGroupService teamspeakGroupService,
        IUksfLogger logger
    )
    {
        _accountContext = accountContext;
        _eventBus = eventBus;
        _teamspeakService = teamspeakService;
        _teamspeakGroupService = teamspeakGroupService;
        _logger = logger;
    }

    public void EarlyInit() { }

    public void Init()
    {
        _eventBus.AsObservable().SubscribeWithAsyncNext<SignalrEventData>(HandleEvent, _logger.LogError);
    }

    private async Task HandleEvent(EventModel eventModel, SignalrEventData signalrEventData)
    {
        switch (signalrEventData.Procedure)
        {
            case TeamspeakEventType.Clients:
                await UpdateClients(signalrEventData.Args.ToString());
                break;
            case TeamspeakEventType.Client_Server_Groups:
                await UpdateClientServerGroups(signalrEventData.Args.ToString());
                break;
            case TeamspeakEventType.Empty: break;
            default:                       throw new ArgumentException("Invalid teamspeak event type");
        }
    }

    private async Task UpdateClients(string args)
    {
        await Console.Out.WriteLineAsync(args);
        var clients = JsonSerializer.Deserialize<HashSet<TeamspeakClient>>(args, DefaultJsonSerializerOptions.Options);
        if (clients == null || clients.Count == 0)
        {
            return;
        }

        await Console.Out.WriteLineAsync("Updating online clients");
        await _teamspeakService.UpdateClients(clients.ToHashSet());
    }

    private async Task UpdateClientServerGroups(string args)
    {
        var updateObject = JsonNode.Parse(args);
        var clientDbId = int.Parse(updateObject.GetValueFromObject("clientDbid"));
        var serverGroupId = int.Parse(updateObject.GetValueFromObject("serverGroupId"));
        await Console.Out.WriteLineAsync($"Server group for {clientDbId}: {serverGroupId}");

        var update = _serverGroupUpdates.GetOrAdd(clientDbId, _ => new TeamspeakServerGroupUpdate());
        update.ServerGroups.Add(serverGroupId);
        update.CancellationTokenSource?.Cancel();
        update.CancellationTokenSource = new CancellationTokenSource();
        _ = TaskUtilities.DelayWithCallback(
            TimeSpan.FromMilliseconds(500),
            update.CancellationTokenSource.Token,
            async () =>
            {
                update.CancellationTokenSource.Cancel();
                _serverGroupUpdates.TryRemove(clientDbId, out _);

                try
                {
                    await ProcessAccountData(clientDbId, update.ServerGroups);
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception);
                }
            }
        );
    }

    private async Task ProcessAccountData(int clientDbId, ICollection<int> serverGroups)
    {
        await Console.Out.WriteLineAsync($"Processing server groups for {clientDbId}");
        var accounts = _accountContext.Get(x => x.TeamspeakIdentities != null && x.TeamspeakIdentities.Any(y => y.Equals(clientDbId)));
        var account = accounts.MaxBy(x => x.MembershipState);
        await _teamspeakGroupService.UpdateAccountGroups(account, serverGroups, clientDbId);
    }
}
