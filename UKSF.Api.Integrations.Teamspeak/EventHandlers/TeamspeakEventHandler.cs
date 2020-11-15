using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UKSF.Api.Base.Events;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Extensions;
using UKSF.Api.Shared.Models;
using UKSF.Api.Teamspeak.Models;
using UKSF.Api.Teamspeak.Services;

namespace UKSF.Api.Teamspeak.EventHandlers {
    public interface ITeamspeakEventHandler : IEventHandler { }

    public class TeamspeakEventHandler : ITeamspeakEventHandler {
        private readonly IAccountService _accountService;
        private readonly ISignalrEventBus _eventBus;
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<double, TeamspeakServerGroupUpdate> _serverGroupUpdates = new ConcurrentDictionary<double, TeamspeakServerGroupUpdate>();
        private readonly ITeamspeakGroupService _teamspeakGroupService;
        private readonly ITeamspeakService _teamspeakService;

        public TeamspeakEventHandler(ISignalrEventBus eventBus, ITeamspeakService teamspeakService, IAccountService accountService, ITeamspeakGroupService teamspeakGroupService, ILogger logger) {
            _eventBus = eventBus;
            _teamspeakService = teamspeakService;
            _accountService = accountService;
            _teamspeakGroupService = teamspeakGroupService;
            _logger = logger;
        }

        public void Init() {
            _eventBus.AsObservable().SubscribeWithAsyncNext(HandleEvent, exception => _logger.LogError(exception));
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
                default:                       throw new ArgumentException($"Invalid teamspeak event type");
            }
        }

        private async Task UpdateClients(string args) {
            await Console.Out.WriteLineAsync(args);
            JArray clientsArray = JArray.Parse(args);
            if (clientsArray.Count == 0) return;

            HashSet<TeamspeakClient> clients = clientsArray.ToObject<HashSet<TeamspeakClient>>();
            await Console.Out.WriteLineAsync("Updating online clients");
            await _teamspeakService.UpdateClients(clients);
        }

        private async Task UpdateClientServerGroups(string args) {
            JObject updateObject = JObject.Parse(args);
            double clientDbid = double.Parse(updateObject["clientDbid"].ToString());
            double serverGroupId = double.Parse(updateObject["serverGroupId"].ToString());
            await Console.Out.WriteLineAsync($"Server group for {clientDbid}: {serverGroupId}");

            TeamspeakServerGroupUpdate update = _serverGroupUpdates.GetOrAdd(clientDbid, _ => new TeamspeakServerGroupUpdate());
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
            Account account = _accountService.Data.GetSingle(x => x.teamspeakIdentities != null && x.teamspeakIdentities.Any(y => y.Equals(clientDbId)));
            Task unused = _teamspeakGroupService.UpdateAccountGroups(account, serverGroups, clientDbId);

            _serverGroupUpdates.TryRemove(clientDbId, out TeamspeakServerGroupUpdate _);
        }
    }
}
