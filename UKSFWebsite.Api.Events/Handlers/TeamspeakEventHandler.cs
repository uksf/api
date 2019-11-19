using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UKSFWebsite.Api.Interfaces.Events;
using UKSFWebsite.Api.Interfaces.Events.Handlers;
using UKSFWebsite.Api.Interfaces.Integrations.Teamspeak;
using UKSFWebsite.Api.Interfaces.Personnel;
using UKSFWebsite.Api.Models.Events;
using UKSFWebsite.Api.Models.Events.Types;
using UKSFWebsite.Api.Models.Integrations;
using UKSFWebsite.Api.Models.Personnel;

namespace UKSFWebsite.Api.Events.Handlers {
    public class TeamspeakEventHandler : ITeamspeakEventHandler {
        private readonly ISignalrEventBus eventBus;
        private readonly ITeamspeakService teamspeakService;
        private readonly IAccountService accountService;
        private readonly ITeamspeakGroupService teamspeakGroupService;
        private readonly Dictionary<double, TeamspeakServerGroupUpdate> serverGroupUpdates = new Dictionary<double, TeamspeakServerGroupUpdate>();

        public TeamspeakEventHandler(ISignalrEventBus eventBus, ITeamspeakService teamspeakService, IAccountService accountService, ITeamspeakGroupService teamspeakGroupService) {
            this.eventBus = eventBus;
            this.teamspeakService = teamspeakService;
            this.accountService = accountService;
            this.teamspeakGroupService = teamspeakGroupService;
        }

        public void Init() {
            eventBus.AsObservable()
                       .Subscribe(
                           async x => { await HandleEvent(x); }
                       );
        }

        private async Task HandleEvent(SignalrEventModel eventModel) {
            string args = eventModel.args.ToString();
            switch (eventModel.procedure) {
                case TeamspeakEventType.CLIENTS:
                    await UpdateClients(args);
                    break;
                case TeamspeakEventType.CLIENT_SERVER_GROUPS:
                    UpdateClientServerGroups(args);
                    break;
                case TeamspeakEventType.EMPTY: break;
                default: throw new ArgumentOutOfRangeException();
            }
        }

        private async Task UpdateClients(string args) {
            Console.Out.WriteLine(args);
            JArray clientsArray = JArray.Parse(args);
            if (clientsArray.Count == 0) return;
            HashSet<TeamspeakClient> clients = clientsArray.ToObject<HashSet<TeamspeakClient>>();
            Console.WriteLine("Updating online clients");
            await teamspeakService.UpdateClients(clients);
        }

        private void UpdateClientServerGroups(string args) {
            JObject updateObject = JObject.Parse(args);
            double clientDbid = double.Parse(updateObject["clientDbid"].ToString());
            double serverGroupId = double.Parse(updateObject["serverGroupId"].ToString());
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
                        await Task.Delay(TimeSpan.FromMilliseconds(500), update.cancellationTokenSource.Token);
                        if (!update.cancellationTokenSource.IsCancellationRequested) {
                            update.cancellationTokenSource.Cancel();
                            ProcessAccountData(clientDbid, update.serverGroups);
                        }
                    },
                    update.cancellationTokenSource.Token
                );
            }
        }

        private void ProcessAccountData(double clientDbId, ICollection<double> serverGroups) {
            Console.WriteLine($"Processing server groups for {clientDbId}");
            Account account = accountService.Data().GetSingle(x => x.teamspeakIdentities != null && x.teamspeakIdentities.Any(y => y.Equals(clientDbId)));
            teamspeakGroupService.UpdateAccountGroups(account, serverGroups, clientDbId);

            lock (serverGroupUpdates) {
                serverGroupUpdates.Remove(clientDbId);
            }
        }
    }
}
