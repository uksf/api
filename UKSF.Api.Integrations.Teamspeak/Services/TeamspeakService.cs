using Microsoft.AspNetCore.SignalR;
using MongoDB.Driver;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Integrations.Teamspeak.Models;
using UKSF.Api.Integrations.Teamspeak.Signalr.Clients;
using UKSF.Api.Integrations.Teamspeak.Signalr.Hubs;

namespace UKSF.Api.Integrations.Teamspeak.Services;

public interface ITeamspeakService
{
    IEnumerable<TeamspeakClient> GetOnlineTeamspeakClients();
    OnlineState GetOnlineUserDetails(string accountId);
    List<TeamspeakConnectClient> GetFormattedClients();
    Task UpdateClients(HashSet<TeamspeakClient> newClients);
    Task UpdateAccountTeamspeakGroups(DomainAccount account);
    Task SendTeamspeakMessageToClient(DomainAccount account, string message);
    Task SendTeamspeakMessageToClient(IEnumerable<int> clientDbIds, string message);
    Task Reload();
    Task Shutdown();
    Task StoreTeamspeakServerSnapshot();
}

public class TeamspeakService : ITeamspeakService
{
    private readonly IAccountContext _accountContext;
    private readonly SemaphoreSlim _clientsSemaphore = new(1);
    private readonly IMongoDatabase _database;
    private readonly IHostEnvironment _environment;
    private readonly IHubContext<TeamspeakClientsHub, ITeamspeakClientsClient> _teamspeakClientsHub;
    private readonly ITeamspeakManagerService _teamspeakManagerService;
    private HashSet<TeamspeakClient> _clients = [];

    public TeamspeakService(
        IAccountContext accountContext,
        IMongoDatabase database,
        IHubContext<TeamspeakClientsHub, ITeamspeakClientsClient> teamspeakClientsHub,
        ITeamspeakManagerService teamspeakManagerService,
        IHostEnvironment environment
    )
    {
        _accountContext = accountContext;
        _database = database;
        _teamspeakClientsHub = teamspeakClientsHub;
        _teamspeakManagerService = teamspeakManagerService;
        _environment = environment;
    }

    public IEnumerable<TeamspeakClient> GetOnlineTeamspeakClients()
    {
        return _clients;
    }

    public async Task UpdateClients(HashSet<TeamspeakClient> newClients)
    {
        await _clientsSemaphore.WaitAsync();
        _clients = newClients;
        _clientsSemaphore.Release();
        await _teamspeakClientsHub.Clients.All.ReceiveClients(GetFormattedClients());
    }

    public async Task UpdateAccountTeamspeakGroups(DomainAccount account)
    {
        if (account?.TeamspeakIdentities == null)
        {
            return;
        }

        foreach (var clientDbId in account.TeamspeakIdentities)
        {
            await _teamspeakManagerService.SendProcedure(TeamspeakProcedureType.Groups, new { clientDbId });
        }
    }

    public async Task SendTeamspeakMessageToClient(DomainAccount account, string message)
    {
        await SendTeamspeakMessageToClient(account.TeamspeakIdentities, message);
    }

    public async Task SendTeamspeakMessageToClient(IEnumerable<int> clientDbIds, string message)
    {
        message = FormatTeamspeakMessage(message);
        foreach (var clientDbId in clientDbIds)
        {
            await _teamspeakManagerService.SendProcedure(TeamspeakProcedureType.Message, new { clientDbId, message });
        }
    }

    public async Task StoreTeamspeakServerSnapshot()
    {
        if (_clients.Count == 0)
        {
            await Console.Out.WriteLineAsync("No client data for snapshot");
            return;
        }

        // TODO: Remove direct db call
        TeamspeakServerSnapshot teamspeakServerSnapshot = new() { Timestamp = DateTime.UtcNow, Users = _clients };
        await _database.GetCollection<TeamspeakServerSnapshot>("teamspeakSnapshots").InsertOneAsync(teamspeakServerSnapshot);
    }

    public async Task Reload()
    {
        await _teamspeakManagerService.SendProcedure(TeamspeakProcedureType.Reload, new { });
    }

    public async Task Shutdown()
    {
        await _teamspeakManagerService.SendProcedure(TeamspeakProcedureType.Shutdown, new { });
    }

    public List<TeamspeakConnectClient> GetFormattedClients()
    {
        var clients = _clients;
        if (_environment.IsDevelopment())
        {
            clients =
            [
                new TeamspeakClient { ClientName = "SqnLdr.Beswick.T", ClientDbId = 2 },
                new TeamspeakClient { ClientName = "Dummy Client", ClientDbId = 999999 }
            ];
        }

        return clients.Where(x => x != null)
                      .Select(
                          client =>
                          {
                              var account = _accountContext.GetSingle(
                                  account => account.TeamspeakIdentities != null && account.TeamspeakIdentities.Contains(client.ClientDbId)
                              );
                              return new TeamspeakConnectClient
                              {
                                  ClientName = client.ClientName,
                                  ClientDbId = client.ClientDbId,
                                  Connected = account != null,
                              };
                          }
                      )
                      .OrderBy(x => x.Connected)
                      .ToList();
    }

    // TODO: Change to use signalr (or hook into existing _teamspeakClientsHub)
    public OnlineState GetOnlineUserDetails(string accountId)
    {
        if (_environment.IsDevelopment())
        {
            _clients = [new TeamspeakClient { ClientName = "SqnLdr.Beswick.T", ClientDbId = 2 }];
        }

        if (_clients.Count == 0)
        {
            return null;
        }

        var account = _accountContext.GetSingle(accountId);
        if (account?.TeamspeakIdentities == null)
        {
            return null;
        }

        if (_environment.IsDevelopment())
        {
            _clients.First().ClientDbId = account.TeamspeakIdentities.First();
        }

        return _clients.Where(client => client != null && account.TeamspeakIdentities.Any(clientDbId => clientDbId.Equals(client.ClientDbId)))
                       .Select(client => new OnlineState { Online = true, Nickname = client.ClientName })
                       .FirstOrDefault();
    }

    private static string FormatTeamspeakMessage(string message)
    {
        return $"\n========== UKSF Server Message ==========\n{message}\n==================================";
    }
}
