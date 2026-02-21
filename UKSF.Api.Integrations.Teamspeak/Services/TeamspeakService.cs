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

public class TeamspeakService(
    IAccountContext accountContext,
    IMongoDatabase database,
    IHubContext<TeamspeakClientsHub, ITeamspeakClientsClient> teamspeakClientsHub,
    ITeamspeakManagerService teamspeakManagerService,
    IHostEnvironment environment
) : ITeamspeakService
{
    private volatile HashSet<TeamspeakClient> _clients = [];

    public IEnumerable<TeamspeakClient> GetOnlineTeamspeakClients()
    {
        return _clients;
    }

    public async Task UpdateClients(HashSet<TeamspeakClient> newClients)
    {
        _clients = newClients;
        await teamspeakClientsHub.Clients.All.ReceiveClients(GetFormattedClients());
    }

    public async Task UpdateAccountTeamspeakGroups(DomainAccount account)
    {
        if (account?.TeamspeakIdentities == null)
        {
            return;
        }

        foreach (var clientDbId in account.TeamspeakIdentities)
        {
            await teamspeakManagerService.SendProcedure(TeamspeakProcedureType.Groups, new { clientDbId });
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
            await teamspeakManagerService.SendProcedure(TeamspeakProcedureType.Message, new { clientDbId, message });
        }
    }

    public async Task StoreTeamspeakServerSnapshot()
    {
        var clients = _clients;
        if (clients.Count == 0)
        {
            await Console.Out.WriteLineAsync("No client data for snapshot");
            return;
        }

        // TODO: Remove direct db call
        TeamspeakServerSnapshot teamspeakServerSnapshot = new() { Timestamp = DateTime.UtcNow, Users = clients };
        await database.GetCollection<TeamspeakServerSnapshot>("teamspeakSnapshots").InsertOneAsync(teamspeakServerSnapshot);
    }

    public async Task Reload()
    {
        await teamspeakManagerService.SendProcedure(TeamspeakProcedureType.Reload, new { });
    }

    public async Task Shutdown()
    {
        await teamspeakManagerService.SendProcedure(TeamspeakProcedureType.Shutdown, new { });
    }

    public List<TeamspeakConnectClient> GetFormattedClients()
    {
        var clients = _clients;
        if (environment.IsDevelopment())
        {
            clients =
            [
                new TeamspeakClient { ClientName = "SqnLdr.Beswick.T", ClientDbId = 2 },
                new TeamspeakClient { ClientName = "Dummy Client", ClientDbId = 999999 }
            ];
        }

        return clients.Where(x => x is not null)
                      .Select(client =>
                          {
                              var account = accountContext.GetSingle(account => account.TeamspeakIdentities is not null &&
                                                                                account.TeamspeakIdentities.Contains(client.ClientDbId)
                              );
                              return new TeamspeakConnectClient
                              {
                                  ClientName = client.ClientName,
                                  ClientDbId = client.ClientDbId,
                                  Connected = account is not null
                              };
                          }
                      )
                      .OrderBy(x => x.Connected)
                      .ToList();
    }

    // TODO: Change to use signalr (or hook into existing _teamspeakClientsHub)
    public OnlineState GetOnlineUserDetails(string accountId)
    {
        var account = accountContext.GetSingle(accountId);
        if (account?.TeamspeakIdentities == null)
        {
            return null;
        }

        var clients = _clients;
        if (environment.IsDevelopment())
        {
            var devClient = new TeamspeakClient { ClientName = "SqnLdr.Beswick.T", ClientDbId = account.TeamspeakIdentities.FirstOrDefault() };
            clients = [devClient];
        }

        if (clients.Count == 0)
        {
            return null;
        }

        return clients.Where(client => client is not null && account.TeamspeakIdentities.Any(clientDbId => clientDbId.Equals(client.ClientDbId)))
                      .Select(client => new OnlineState { Online = true, Nickname = client.ClientName })
                      .FirstOrDefault();
    }

    private static string FormatTeamspeakMessage(string message)
    {
        return $"\n========== UKSF Server Message ==========\n{message}\n==================================";
    }
}
