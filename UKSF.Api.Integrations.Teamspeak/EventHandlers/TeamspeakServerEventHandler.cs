using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using UKSF.Api.Shared;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Extensions;
using UKSF.Api.Shared.Models;
using UKSF.Api.Teamspeak.Models;
using UKSF.Api.Teamspeak.Services;

namespace UKSF.Api.Teamspeak.EventHandlers;

public interface ITeamspeakServerEventHandler : IEventHandler { }

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
            case TeamspeakEventType.CLIENTS:
                await UpdateClients(signalrEventData.Args.ToString());
                break;
            case TeamspeakEventType.CLIENT_SERVER_GROUPS:
                await UpdateClientServerGroups(signalrEventData.Args.ToString());
                break;
            case TeamspeakEventType.EMPTY: break;
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

        var update = _serverGroupUpdates.GetOrAdd(clientDbId, _ => new());
        update.ServerGroups.Add(serverGroupId);
        update.CancellationTokenSource?.Cancel();
        update.CancellationTokenSource = new();
        var unused = TaskUtilities.DelayWithCallback(
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
        var domainAccounts = _accountContext.Get(x => x.TeamspeakIdentities != null && x.TeamspeakIdentities.Any(y => y.Equals(clientDbId)));
        var domainAccount = domainAccounts.OrderByDescending(x => x.MembershipState).FirstOrDefault();
        await _teamspeakGroupService.UpdateAccountGroups(domainAccount, serverGroups, clientDbId);
    }
}
