using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UKSFWebsite.Api.Interfaces.Events;
using UKSFWebsite.Api.Interfaces.Events.Handlers;
using UKSFWebsite.Api.Interfaces.Integrations.Teamspeak;
using UKSFWebsite.Api.Interfaces.Personnel;
using UKSFWebsite.Api.Models.Events.Types;
using UKSFWebsite.Api.Models.Integrations;
using UKSFWebsite.Api.Models.Personnel;

namespace UKSFWebsite.Api.Events.Handlers {
    public class TeamspeakEventHandler : ITeamspeakEventHandler {
        private readonly ISocketEventBus eventBus;
        private readonly ITeamspeakService teamspeakService;
        private readonly IAccountService accountService;
        private readonly ITeamspeakGroupService teamspeakGroupService;
        private readonly Dictionary<string, TeamspeakServerGroupUpdate> serverGroupUpdates = new Dictionary<string, TeamspeakServerGroupUpdate>();

        public TeamspeakEventHandler(ISocketEventBus eventBus, ITeamspeakService teamspeakService, IAccountService accountService, ITeamspeakGroupService teamspeakGroupService) {
            this.eventBus = eventBus;
            this.teamspeakService = teamspeakService;
            this.accountService = accountService;
            this.teamspeakGroupService = teamspeakGroupService;
        }

        public void Init() {
            eventBus.AsObservable("0")
                       .Subscribe(
                           async x => { await HandleEvent(x.message); }
                       );
        }

        private async Task HandleEvent(string messageString) {
            if (!Enum.TryParse(messageString.Substring(0, 1), out TeamspeakSocketEventType eventType)) return;
            string message = messageString.Substring(1);
            switch (eventType) {
                case TeamspeakSocketEventType.CLIENTS:
                    await UpdateClients(message);
                    break;
                case TeamspeakSocketEventType.CLIENT_SERVER_GROUPS:
                    UpdateClientServerGroups(message);
                    break;
                default: throw new ArgumentOutOfRangeException();
            }
        }

        private void UpdateClientServerGroups(string message) {
            string[] args = message.Split('|');
            string clientDbid = args[0];
            string serverGroupId = args[1];
            Console.WriteLine($"Server group for {clientDbid}: {serverGroupId}");

            lock (serverGroupUpdates) {
                if (!serverGroupUpdates.ContainsKey(clientDbid)) {
                    serverGroupUpdates.Add(clientDbid, new TeamspeakServerGroupUpdate());
                }

                TeamspeakServerGroupUpdate update = serverGroupUpdates[clientDbid];
                update.serverGroups.Add(serverGroupId);
                update.cancellationTokenSource?.Cancel();
                update.cancellationTokenSource = new CancellationTokenSource();
                Task.Run(
                    async () => {
                        await Task.Delay(TimeSpan.FromMilliseconds(200), update.cancellationTokenSource.Token);
                        if (!update.cancellationTokenSource.IsCancellationRequested) {
                            update.cancellationTokenSource.Cancel();
                            ProcessAccountData(clientDbid, update.serverGroups);
                        }
                    },
                    update.cancellationTokenSource.Token
                );
            }
        }

        private async Task UpdateClients(string clients) {
            if (string.IsNullOrEmpty(clients)) return;
            Console.WriteLine("Updating online clients");
            await teamspeakService.UpdateClients(clients);
        }

        private void ProcessAccountData(string clientDbId, ICollection<string> serverGroups) {
            Console.WriteLine($"Processing server groups for {clientDbId}");
            Account account = accountService.Data().GetSingle(x => x.teamspeakIdentities != null && x.teamspeakIdentities.Any(y => y == clientDbId));
            teamspeakGroupService.UpdateAccountGroups(account, serverGroups, clientDbId);

            lock (serverGroupUpdates) {
                serverGroupUpdates.Remove(clientDbId);
            }
        }
    }
}
