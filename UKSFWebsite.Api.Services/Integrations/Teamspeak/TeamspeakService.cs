using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using MongoDB.Driver;
using UKSFWebsite.Api.Interfaces.Hubs;
using UKSFWebsite.Api.Interfaces.Integrations.Teamspeak;
using UKSFWebsite.Api.Models.Integrations;
using UKSFWebsite.Api.Models.Personnel;
using UKSFWebsite.Api.Signalr.Hubs.Integrations;

namespace UKSFWebsite.Api.Services.Integrations.Teamspeak {
    public class TeamspeakService : ITeamspeakService {
        private readonly SemaphoreSlim clientsSemaphore = new SemaphoreSlim(1);
        private readonly IMongoDatabase database;
        private readonly IHubContext<TeamspeakClientsHub, ITeamspeakClientsClient> teamspeakClientsHub;
        private readonly ITeamspeakManager teamspeakManager;
        private HashSet<TeamspeakClient> clients = new HashSet<TeamspeakClient>();

        public TeamspeakService(IMongoDatabase database, IHubContext<TeamspeakClientsHub, ITeamspeakClientsClient> teamspeakClientsHub, ITeamspeakManager teamspeakManager) {
            this.database = database;
            this.teamspeakClientsHub = teamspeakClientsHub;
            this.teamspeakManager = teamspeakManager;
        }

        public HashSet<TeamspeakClient> GetOnlineTeamspeakClients() => clients;

        public async Task UpdateClients(HashSet<TeamspeakClient> newClients) {
            await clientsSemaphore.WaitAsync();
            clients = newClients;
            clientsSemaphore.Release();
            await teamspeakClientsHub.Clients.All.ReceiveClients(GetFormattedClients());
        }

        public void UpdateAccountTeamspeakGroups(Account account) {
            if (account?.teamspeakIdentities == null) return;
            foreach (double clientDbId in account.teamspeakIdentities) {
                teamspeakManager.SendProcedure(TeamspeakProcedureType.GROUPS, new {clientDbId});
            }
        }

        public void SendTeamspeakMessageToClient(Account account, string message) {
            if (account.teamspeakIdentities == null) return;
            if (account.teamspeakIdentities.Count == 0) return;
            SendTeamspeakMessageToClient(account.teamspeakIdentities, message);
        }

        public void SendTeamspeakMessageToClient(IEnumerable<double> clientDbIds, string message) {
            message = FormatTeamspeakMessage(message);
            foreach (double clientDbId in clientDbIds) {
                teamspeakManager.SendProcedure(TeamspeakProcedureType.MESSAGE, new {clientDbId, message});
            }
        }

        public async Task StoreTeamspeakServerSnapshot() {
            if (clients.Count == 0) {
                Console.WriteLine("No client data for snapshot");
                return;
            }

            TeamspeakServerSnapshot teamspeakServerSnapshot = new TeamspeakServerSnapshot {timestamp = DateTime.UtcNow, users = clients};
            await database.GetCollection<TeamspeakServerSnapshot>("teamspeakSnapshots").InsertOneAsync(teamspeakServerSnapshot);
        }

        public void Shutdown() {
            teamspeakManager.SendProcedure(TeamspeakProcedureType.SHUTDOWN, new {});
        }

        public object GetFormattedClients() {
            return clients.Count == 0 ? null : clients.Where(x => x != null).Select(x => new {name = $"{x.clientName}", x.clientDbId}).ToList();
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