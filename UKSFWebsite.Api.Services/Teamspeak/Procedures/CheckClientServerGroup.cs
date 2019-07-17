using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UKSFWebsite.Api.Models.Accounts;
using UKSFWebsite.Api.Services.Abstraction;

namespace UKSFWebsite.Api.Services.Teamspeak.Procedures {

    public class CheckClientServerGroup : ITeamspeakProcedure {
        private static readonly Dictionary<string, ServerGroupUpdate> SERVER_GROUP_UPDATES = new Dictionary<string, ServerGroupUpdate>();

        private readonly IAccountService accountService;
        private readonly ITeamspeakGroupService teamspeakGroupService;

        public CheckClientServerGroup(IAccountService accountService, ITeamspeakGroupService teamspeakGroupService) {
            this.accountService = accountService;
            this.teamspeakGroupService = teamspeakGroupService;
        }

        public void Run(string[] args) {
            string clientDbid = args[0];
            string serverGroupId = args[1];
            Console.WriteLine($"Server group for {clientDbid}: {serverGroupId}");

            lock (SERVER_GROUP_UPDATES) {
                if (!SERVER_GROUP_UPDATES.ContainsKey(clientDbid)) {
                    SERVER_GROUP_UPDATES.Add(clientDbid, new ServerGroupUpdate());
                }

                ServerGroupUpdate update = SERVER_GROUP_UPDATES[clientDbid];
                update.ServerGroups.Add(serverGroupId);
                update.CancellationTokenSource?.Cancel();
                update.CancellationTokenSource = new CancellationTokenSource();
                Task.Run(
                    async () => {
                        await Task.Delay(TimeSpan.FromMilliseconds(200), update.CancellationTokenSource.Token);
                        if (!update.CancellationTokenSource.IsCancellationRequested) {
                            update.CancellationTokenSource.Cancel();
                            ProcessAccountData(clientDbid, update.ServerGroups);
                        }
                    },
                    update.CancellationTokenSource.Token
                );
            }
        }

        private void ProcessAccountData(string clientDbId, ICollection<string> serverGroups) {
            Console.WriteLine($"Processing server groups for {clientDbId}");
            Account account = accountService.GetSingle(x => x.teamspeakIdentities != null && x.teamspeakIdentities.Any(y => y == clientDbId));
            teamspeakGroupService.UpdateAccountGroups(account, serverGroups, clientDbId);

            lock (SERVER_GROUP_UPDATES) {
                SERVER_GROUP_UPDATES.Remove(clientDbId);
            }
        }
    }

    internal class ServerGroupUpdate {
        public readonly List<string> ServerGroups = new List<string>();
        public CancellationTokenSource CancellationTokenSource;
    }
}
