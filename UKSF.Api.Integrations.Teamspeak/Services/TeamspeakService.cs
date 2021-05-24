using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Shared.Models;
using UKSF.Api.Teamspeak.Models;
using UKSF.Api.Teamspeak.Signalr.Clients;
using UKSF.Api.Teamspeak.Signalr.Hubs;

namespace UKSF.Api.Teamspeak.Services
{
    public interface ITeamspeakService
    {
        IEnumerable<TeamspeakClient> GetOnlineTeamspeakClients();
        OnlineState GetOnlineUserDetails(string accountId);
        IEnumerable<object> GetFormattedClients();
        Task UpdateClients(HashSet<TeamspeakClient> newClients);
        Task UpdateAccountTeamspeakGroups(DomainAccount domainAccount);
        Task SendTeamspeakMessageToClient(DomainAccount domainAccount, string message);
        Task SendTeamspeakMessageToClient(IEnumerable<int> clientDbIds, string message);
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
        private HashSet<TeamspeakClient> _clients = new();

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

        public async Task UpdateAccountTeamspeakGroups(DomainAccount domainAccount)
        {
            if (domainAccount?.TeamspeakIdentities == null)
            {
                return;
            }

            foreach (int clientDbId in domainAccount.TeamspeakIdentities)
            {
                await _teamspeakManagerService.SendProcedure(TeamspeakProcedureType.GROUPS, new { clientDbId });
            }
        }

        public async Task SendTeamspeakMessageToClient(DomainAccount domainAccount, string message)
        {
            await SendTeamspeakMessageToClient(domainAccount.TeamspeakIdentities, message);
        }

        public async Task SendTeamspeakMessageToClient(IEnumerable<int> clientDbIds, string message)
        {
            message = FormatTeamspeakMessage(message);
            foreach (int clientDbId in clientDbIds)
            {
                await _teamspeakManagerService.SendProcedure(TeamspeakProcedureType.MESSAGE, new { clientDbId, message });
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

        public async Task Shutdown()
        {
            await _teamspeakManagerService.SendProcedure(TeamspeakProcedureType.SHUTDOWN, new { });
        }

        public IEnumerable<object> GetFormattedClients()
        {
            if (_environment.IsDevelopment())
            {
                return new List<object> { new { name = "SqnLdr.Beswick.T", clientDbId = 2 } };
            }

            return _clients.Where(x => x != null).Select(x => new { name = $"{x.ClientName}", clientDbId = x.ClientDbId });
        }

        // TODO: Change to use signalr (or hook into existing _teamspeakClientsHub)
        public OnlineState GetOnlineUserDetails(string accountId)
        {
            if (_environment.IsDevelopment())
            {
                _clients = new() { new() { ClientName = "SqnLdr.Beswick.T", ClientDbId = 2 } };
            }

            if (_clients.Count == 0)
            {
                return null;
            }

            DomainAccount domainAccount = _accountContext.GetSingle(accountId);
            if (domainAccount?.TeamspeakIdentities == null)
            {
                return null;
            }

            if (_environment.IsDevelopment())
            {
                _clients.First().ClientDbId = domainAccount.TeamspeakIdentities.First();
            }

            return _clients.Where(client => client != null && domainAccount.TeamspeakIdentities.Any(clientDbId => clientDbId.Equals(client.ClientDbId)))
                           .Select(client => new OnlineState { Online = true, Nickname = client.ClientName })
                           .FirstOrDefault();
        }

        private static string FormatTeamspeakMessage(string message)
        {
            return $"\n========== UKSF Server Message ==========\n{message}\n==================================";
        }
    }
}
