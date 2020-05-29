using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Interfaces.Events.Handlers;
using UKSF.Api.Interfaces.Integrations.Teamspeak;
using UKSF.Api.Interfaces.Message;
using UKSF.Api.Interfaces.Personnel;
using UKSF.Api.Models.Events;
using UKSF.Api.Models.Events.Types;
using UKSF.Api.Models.Integrations;
using UKSF.Api.Models.Personnel;
using UKSF.Common;

namespace UKSF.Api.Events.Handlers {
    public class TeamspeakEventHandler : ITeamspeakEventHandler {
        private readonly IAccountService accountService;
        private readonly ISignalrEventBus eventBus;
        private readonly ILoggingService loggingService;
        private readonly AsyncLock mutex = new AsyncLock();
        private readonly ConcurrentDictionary<double, TeamspeakServerGroupUpdate> serverGroupUpdates = new ConcurrentDictionary<double, TeamspeakServerGroupUpdate>();
        private readonly ITeamspeakGroupService teamspeakGroupService;
        private readonly ITeamspeakService teamspeakService;

        public TeamspeakEventHandler(
            ISignalrEventBus eventBus,
            ITeamspeakService teamspeakService,
            IAccountService accountService,
            ITeamspeakGroupService teamspeakGroupService,
            ILoggingService loggingService
        ) {
            this.eventBus = eventBus;
            this.teamspeakService = teamspeakService;
            this.accountService = accountService;
            this.teamspeakGroupService = teamspeakGroupService;
            this.loggingService = loggingService;
        }

        public void Init() {
            eventBus.AsObservable().SubscribeAsync(HandleEvent, exception => loggingService.Log(exception));
        }

        private async Task HandleEvent(SignalrEventModel x) {
            switch (x.procedure) {
                case TeamspeakEventType.CLIENTS:
                    await UpdateClients(x.args.ToString());
                    break;
                case TeamspeakEventType.CLIENT_SERVER_GROUPS:
                    await UpdateClientServerGroups(x.args.ToString());
                    break;
                case TeamspeakEventType.EMPTY: break;
                default: throw new ArgumentOutOfRangeException();
            }
        }

        private async Task UpdateClients(string args) {
            await Console.Out.WriteLineAsync(args);
            JArray clientsArray = JArray.Parse(args);
            if (clientsArray.Count == 0) return;

            HashSet<TeamspeakClient> clients = clientsArray.ToObject<HashSet<TeamspeakClient>>();
            await Console.Out.WriteLineAsync("Updating online clients");
            await teamspeakService.UpdateClients(clients);
        }

        private async Task UpdateClientServerGroups(string args) {
            JObject updateObject = JObject.Parse(args);
            double clientDbid = double.Parse(updateObject["clientDbid"].ToString());
            double serverGroupId = double.Parse(updateObject["serverGroupId"].ToString());
            await Console.Out.WriteLineAsync($"Server group for {clientDbid}: {serverGroupId}");

            TeamspeakServerGroupUpdate update = serverGroupUpdates.GetOrAdd(clientDbid, x => new TeamspeakServerGroupUpdate());
            update.serverGroups.Add(serverGroupId);
            update.cancellationTokenSource?.Cancel();
            update.cancellationTokenSource = new CancellationTokenSource();
            Task unused = TaskUtilities.DelayWithCallback(
                TimeSpan.FromMilliseconds(500),
                update.cancellationTokenSource.Token,
                async () => {
                    update.cancellationTokenSource.Cancel();
                    await ProcessAccountData(clientDbid, update.serverGroups);
                }
            );
        }

        private async Task ProcessAccountData(double clientDbId, ICollection<double> serverGroups) {
            await Console.Out.WriteLineAsync($"Processing server groups for {clientDbId}");
            Account account = accountService.Data.GetSingle(x => x.teamspeakIdentities != null && x.teamspeakIdentities.Any(y => y.Equals(clientDbId)));
            Task unused = teamspeakGroupService.UpdateAccountGroups(account, serverGroups, clientDbId);

            serverGroupUpdates.TryRemove(clientDbId, out TeamspeakServerGroupUpdate _);
        }
    }
}
