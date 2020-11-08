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
        private readonly SemaphoreSlim clientsSemaphore = new SemaphoreSlim(1);
        private readonly IMongoDatabase database;
        private readonly IHubContext<TeamspeakClientsHub, ITeamspeakClientsClient> teamspeakClientsHub;
        private readonly ITeamspeakManagerService teamspeakManagerService;
        private readonly IHostEnvironment environment;
        private HashSet<TeamspeakClient> clients = new HashSet<TeamspeakClient>();

        public TeamspeakService(IMongoDatabase database, IHubContext<TeamspeakClientsHub, ITeamspeakClientsClient> teamspeakClientsHub, ITeamspeakManagerService teamspeakManagerService, IHostEnvironment environment) {
            this.database = database;
            this.teamspeakClientsHub = teamspeakClientsHub;
            this.teamspeakManagerService = teamspeakManagerService;
            this.environment = environment;
        }

        public IEnumerable<TeamspeakClient> GetOnlineTeamspeakClients() => clients;

        public async Task UpdateClients(HashSet<TeamspeakClient> newClients) {
            await clientsSemaphore.WaitAsync();
            clients = newClients;
            clientsSemaphore.Release();
            await teamspeakClientsHub.Clients.All.ReceiveClients(GetFormattedClients());
        }

        public async Task UpdateAccountTeamspeakGroups(Account account) {
            if (account?.teamspeakIdentities == null) return;
            foreach (double clientDbId in account.teamspeakIdentities) {
                await teamspeakManagerService.SendProcedure(TeamspeakProcedureType.GROUPS, new {clientDbId});
            }
        }

        public async Task SendTeamspeakMessageToClient(Account account, string message) {
            if (account.teamspeakIdentities == null) return;
            if (account.teamspeakIdentities.Count == 0) return;
            await SendTeamspeakMessageToClient(account.teamspeakIdentities, message);
        }

        public async Task SendTeamspeakMessageToClient(IEnumerable<double> clientDbIds, string message) {
            message = FormatTeamspeakMessage(message);
            foreach (double clientDbId in clientDbIds) {
                await teamspeakManagerService.SendProcedure(TeamspeakProcedureType.MESSAGE, new {clientDbId, message});
            }
        }

        public async Task StoreTeamspeakServerSnapshot() {
            if (clients.Count == 0) {
                await Console.Out.WriteLineAsync("No client data for snapshot");
                return;
            }

            TeamspeakServerSnapshot teamspeakServerSnapshot = new TeamspeakServerSnapshot {timestamp = DateTime.UtcNow, users = clients};
            await database.GetCollection<TeamspeakServerSnapshot>("teamspeakSnapshots").InsertOneAsync(teamspeakServerSnapshot);
        }

        public async Task Shutdown() {
            await teamspeakManagerService.SendProcedure(TeamspeakProcedureType.SHUTDOWN, new {});
        }

        public IEnumerable<object> GetFormattedClients() {
            if (environment.IsDevelopment()) return new List<object> {new {name = "SqnLdr.Beswick.T", clientDbId = (double) 2}};
            return clients.Where(x => x != null).Select(x => new {name = $"{x.clientName}", x.clientDbId});
        }

        public (bool online, string nickname) GetOnlineUserDetails(Account account) {
            if (account.teamspeakIdentities == null) return (false, "");
            if (clients.Count == 0) return (false, "");

            foreach (TeamspeakClient client in clients.Where(x => x != null)) {
                if (account.teamspeakIdentities.Any(y => y.Equals(client.clientDbId))) {
                    return (true, client.clientName);
                }
            }

            return (false, "");
        }

        private static string FormatTeamspeakMessage(string message) => $"\n========== UKSF Server Message ==========\n{message}\n==================================";
    }
}
