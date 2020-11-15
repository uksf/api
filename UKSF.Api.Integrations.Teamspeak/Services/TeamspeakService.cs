using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Teamspeak.Models;
using UKSF.Api.Teamspeak.Signalr.Clients;
using UKSF.Api.Teamspeak.Signalr.Hubs;

namespace UKSF.Api.Teamspeak.Services {
    public interface ITeamspeakService {
        IEnumerable<TeamspeakClient> GetOnlineTeamspeakClients();
        (bool online, string nickname) GetOnlineUserDetails(Account account);
        IEnumerable<object> GetFormattedClients();
        Task UpdateClients(HashSet<TeamspeakClient> newClients);
        Task UpdateAccountTeamspeakGroups(Account account);
        Task SendTeamspeakMessageToClient(Account account, string message);
        Task SendTeamspeakMessageToClient(IEnumerable<double> clientDbIds, string message);
        Task Shutdown();
        Task StoreTeamspeakServerSnapshot();
    }

    public class TeamspeakService : ITeamspeakService {
        private readonly SemaphoreSlim _clientsSemaphore = new SemaphoreSlim(1);
        private readonly IMongoDatabase _database;
        private readonly IHubContext<TeamspeakClientsHub, ITeamspeakClientsClient> _teamspeakClientsHub;
        private readonly ITeamspeakManagerService _teamspeakManagerService;
        private readonly IHostEnvironment _environment;
        private HashSet<TeamspeakClient> _clients = new HashSet<TeamspeakClient>();

        public TeamspeakService(IMongoDatabase database, IHubContext<TeamspeakClientsHub, ITeamspeakClientsClient> teamspeakClientsHub, ITeamspeakManagerService teamspeakManagerService, IHostEnvironment environment) {
            _database = database;
            _teamspeakClientsHub = teamspeakClientsHub;
            _teamspeakManagerService = teamspeakManagerService;
            _environment = environment;
        }

        public IEnumerable<TeamspeakClient> GetOnlineTeamspeakClients() => _clients;

        public async Task UpdateClients(HashSet<TeamspeakClient> newClients) {
            await _clientsSemaphore.WaitAsync();
            _clients = newClients;
            _clientsSemaphore.Release();
            await _teamspeakClientsHub.Clients.All.ReceiveClients(GetFormattedClients());
        }

        public async Task UpdateAccountTeamspeakGroups(Account account) {
            if (account?.teamspeakIdentities == null) return;
            foreach (double clientDbId in account.teamspeakIdentities) {
                await _teamspeakManagerService.SendProcedure(TeamspeakProcedureType.GROUPS, new {clientDbId});
            }
        }

        public async Task SendTeamspeakMessageToClient(Account account, string message) {
            await SendTeamspeakMessageToClient(account.teamspeakIdentities, message);
        }

        public async Task SendTeamspeakMessageToClient(IEnumerable<double> clientDbIds, string message) {
            message = FormatTeamspeakMessage(message);
            foreach (double clientDbId in clientDbIds) {
                await _teamspeakManagerService.SendProcedure(TeamspeakProcedureType.MESSAGE, new {clientDbId, message});
            }
        }

        public async Task StoreTeamspeakServerSnapshot() {
            if (_clients.Count == 0) {
                await Console.Out.WriteLineAsync("No client data for snapshot");
                return;
            }

            TeamspeakServerSnapshot teamspeakServerSnapshot = new TeamspeakServerSnapshot {timestamp = DateTime.UtcNow, users = _clients};
            await _database.GetCollection<TeamspeakServerSnapshot>("teamspeakSnapshots").InsertOneAsync(teamspeakServerSnapshot);
        }

        public async Task Shutdown() {
            await _teamspeakManagerService.SendProcedure(TeamspeakProcedureType.SHUTDOWN, new {});
        }

        public IEnumerable<object> GetFormattedClients() {
            if (_environment.IsDevelopment()) return new List<object> {new {name = "SqnLdr.Beswick.T", clientDbId = (double) 2}};
            return _clients.Where(x => x != null).Select(x => new {name = $"{x.clientName}", x.clientDbId});
        }

        public (bool online, string nickname) GetOnlineUserDetails(Account account) {
            if (account.teamspeakIdentities == null) return (false, "");
            if (_clients.Count == 0) return (false, "");

            foreach (TeamspeakClient client in _clients.Where(x => x != null)) {
                if (account.teamspeakIdentities.Any(y => y.Equals(client.clientDbId))) {
                    return (true, client.clientName);
                }
            }

            return (false, "");
        }

        private static string FormatTeamspeakMessage(string message) => $"\n========== UKSF Server Message ==========\n{message}\n==================================";
    }
}
