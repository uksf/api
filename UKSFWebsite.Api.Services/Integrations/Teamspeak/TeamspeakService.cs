using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using MongoDB.Driver;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UKSFWebsite.Api.Interfaces.Hubs;
using UKSFWebsite.Api.Interfaces.Integrations.Teamspeak;
using UKSFWebsite.Api.Models.Integrations;
using UKSFWebsite.Api.Models.Personnel;
using UKSFWebsite.Api.Services.Hubs;

namespace UKSFWebsite.Api.Services.Integrations.Teamspeak {
    public class TeamspeakService : ITeamspeakService {
        private readonly SemaphoreSlim clientStringSemaphore = new SemaphoreSlim(1);
        private readonly IMongoDatabase database;
        private readonly IHubContext<TeamspeakClientsHub, ITeamspeakClientsClient> teamspeakClientsHub;
        private readonly ITeamspeakManager teamspeakManager;
        private string clientsString = "";

        public TeamspeakService(IMongoDatabase database, IHubContext<TeamspeakClientsHub, ITeamspeakClientsClient> teamspeakClientsHub, ITeamspeakManager teamspeakManager) {
            this.database = database;
            this.teamspeakClientsHub = teamspeakClientsHub;
            this.teamspeakManager = teamspeakManager;
        }

        public string GetOnlineTeamspeakClients() => clientsString;

        public async Task UpdateClients(string newClientsString) {
            await clientStringSemaphore.WaitAsync();
            clientsString = newClientsString;
            Console.WriteLine(clientsString);
            clientStringSemaphore.Release();
            await teamspeakClientsHub.Clients.All.ReceiveClients(GetFormattedClients());
        }

        public void UpdateAccountTeamspeakGroups(Account account) {
            if (account?.teamspeakIdentities == null) return;
            foreach (string clientDbId in account.teamspeakIdentities) {
                teamspeakManager.SendProcedure($"{TeamspeakSocketProcedureType.GROUPS}:{clientDbId}");
            }
        }

        public void SendTeamspeakMessageToClient(Account account, string message) {
            if (account.teamspeakIdentities == null) return;
            if (account.teamspeakIdentities.Count == 0) return;
            SendTeamspeakMessageToClient(account.teamspeakIdentities, message);
        }

        public void SendTeamspeakMessageToClient(IEnumerable<string> clientDbIds, string message) {
            message = FormatTeamspeakMessage(message);
            foreach (string clientDbId in clientDbIds) {
                teamspeakManager.SendProcedure($"{TeamspeakSocketProcedureType.MESSAGE}:{clientDbId}|{message}");
            }
        }

        public async Task StoreTeamspeakServerSnapshot() {
            string clientsJson = GetOnlineTeamspeakClients();
            if (string.IsNullOrEmpty(clientsJson)) {
                Console.WriteLine("No client data for snapshot");
                return;
            }

            JObject clientsObject = JObject.Parse(clientsJson);
            HashSet<TeamspeakClientSnapshot> onlineClients = JsonConvert.DeserializeObject<HashSet<TeamspeakClientSnapshot>>(clientsObject["clients"].ToString());
            TeamspeakServerSnapshot teamspeakServerSnapshot = new TeamspeakServerSnapshot {timestamp = DateTime.UtcNow, users = onlineClients};
            await database.GetCollection<TeamspeakServerSnapshot>("teamspeakSnapshots").InsertOneAsync(teamspeakServerSnapshot);
        }

        public void Shutdown() {
            teamspeakManager.SendProcedure($"{TeamspeakSocketProcedureType.SHUTDOWN}:");
        }

        public object GetFormattedClients() {
            if (string.IsNullOrEmpty(clientsString)) return null;
            JObject clientsObject = JObject.Parse(clientsString);
            HashSet<TeamspeakClientSnapshot> onlineClients = JsonConvert.DeserializeObject<HashSet<TeamspeakClientSnapshot>>(clientsObject["clients"].ToString());
            return onlineClients.Where(x => x != null).Select(x => new {name = $"{x.clientName}", x.clientDbId}).ToList();
        }

        public (bool online, string nickname) GetOnlineUserDetails(Account account) {
            if (account.teamspeakIdentities == null) return (false, "");
            if (string.IsNullOrEmpty(clientsString)) return (false, "");

            JObject clientsObject = JObject.Parse(clientsString);
            HashSet<TeamspeakClientSnapshot> onlineClients = JsonConvert.DeserializeObject<HashSet<TeamspeakClientSnapshot>>(clientsObject["clients"].ToString());
            foreach (TeamspeakClientSnapshot client in onlineClients.Where(x => x != null)) {
                if (account.teamspeakIdentities.Any(y => y == client.clientDbId)) {
                    return (true, client.clientName);
                }
            }

            return (false, "");
        }

        private static string FormatTeamspeakMessage(string message) => $"\n========== UKSF Server Message ==========\n{message}\n==================================";
    }
}
