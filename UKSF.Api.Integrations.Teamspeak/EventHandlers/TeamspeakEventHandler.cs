using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Extensions;
using UKSF.Api.Base.Models;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Teamspeak.Models;
using UKSF.Api.Teamspeak.Services;

namespace UKSF.Api.Teamspeak.EventHandlers {
    public interface ITeamspeakEventHandler : IEventHandler { }

    public class TeamspeakEventHandler : ITeamspeakEventHandler {
        private readonly IAccountService accountService;
        private readonly ISignalrEventBus eventBus;
        private readonly ConcurrentDictionary<double, TeamspeakServerGroupUpdate> serverGroupUpdates = new ConcurrentDictionary<double, TeamspeakServerGroupUpdate>();
        private readonly ITeamspeakGroupService teamspeakGroupService;
        private readonly ILogger logger;
        private readonly ITeamspeakService teamspeakService;

        public TeamspeakEventHandler(
            ISignalrEventBus eventBus,
            ITeamspeakService teamspeakService,
            IAccountService accountService,
            ITeamspeakGroupService teamspeakGroupService,
            ILogger logger
        ) {
            this.eventBus = eventBus;
            this.teamspeakService = teamspeakService;
            this.accountService = accountService;
            this.teamspeakGroupService = teamspeakGroupService;
            this.logger = logger;
        }

        public void Init() {
            eventBus.AsObservable().SubscribeWithAsyncNext(HandleEvent, exception => logger.LogError(exception));
        }

        private async Task HandleEvent(SignalrEventModel signalrEventModel) {
            switch (signalrEventModel.procedure) {
                case TeamspeakEventType.CLIENTS:
                    await UpdateClients(signalrEventModel.args.ToString());
                    break;
                case TeamspeakEventType.CLIENT_SERVER_GROUPS:
                    await UpdateClientServerGroups(signalrEventModel.args.ToString());
                    break;
                case TeamspeakEventType.EMPTY: break;
                default:                       throw new ArgumentOutOfRangeException(nameof(signalrEventModel));
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
