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
using UKSFWebsite.Api.Interfaces.Integrations;
using UKSFWebsite.Api.Models.Integrations;
using UKSFWebsite.Api.Models.Personnel;
using UKSFWebsite.Api.Services.Hubs;
using UKSFWebsite.Api.Services.Integrations.Procedures;

namespace UKSFWebsite.Api.Services.Integrations {
    public class TeamspeakService : ITeamspeakService {
        private readonly SemaphoreSlim clientStringSemaphore = new SemaphoreSlim(1);
        private readonly IMongoDatabase database;
        private readonly IHubContext<TeamspeakClientsHub, ITeamspeakClientsClient> teamspeakClientsHub;
        private string clientsString = "";

        public TeamspeakService(IMongoDatabase database, IHubContext<TeamspeakClientsHub, ITeamspeakClientsClient> teamspeakClientsHub) {
            this.database = database;
            this.teamspeakClientsHub = teamspeakClientsHub;
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
                PipeQueueManager.QueueMessage($"{ProcedureDefinitons.PROC_UPDATE_SERVER_GROUPS}:{clientDbId}");
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
                PipeQueueManager.QueueMessage($"{ProcedureDefinitons.PROC_SEND_MESSAGE_TO_CLIENT}:{clientDbId}|{message}");
            }
        }

        public async Task StoreTeamspeakServerSnapshot() {
            string clientsJson = GetOnlineTeamspeakClients();
            if (string.IsNullOrEmpty(clientsJson)) {
                Console.WriteLine("No clients online");
                return;
            }

            JObject clientsObject = JObject.Parse(clientsJson);
            HashSet<TeamspeakClientSnapshot> onlineClients = JsonConvert.DeserializeObject<HashSet<TeamspeakClientSnapshot>>(clientsObject["clients"].ToString());
            TeamspeakServerSnapshot teamspeakServerSnapshot = new TeamspeakServerSnapshot {timestamp = DateTime.UtcNow, users = onlineClients};
            Console.WriteLine("Uploading snapshot");
            await database.GetCollection<TeamspeakServerSnapshot>("teamspeakSnapshots").InsertOneAsync(teamspeakServerSnapshot);
        }

        public void Shutdown() {
            PipeQueueManager.QueueMessage($"{ProcedureDefinitons.PROC_SHUTDOWN}:");
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

        private static string FormatTeamspeakMessage(string message) {
            StringBuilder messageBuilder = new StringBuilder();
            messageBuilder.AppendLine("\n========== UKSF Server Message ==========");
            messageBuilder.AppendLine(message);
            messageBuilder.AppendLine("==================================");
            return messageBuilder.ToString();
        }
    }
}
