using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using UKSFWebsite.Api.Interfaces.Hubs;
using UKSFWebsite.Api.Interfaces.Integrations.Teamspeak;
using UKSFWebsite.Api.Models.Integrations;
using UKSFWebsite.Api.Services.Admin;
using UKSFWebsite.Api.Services.Utility;
using UKSFWebsite.Api.Signalr.Hubs.Integrations;

namespace UKSFWebsite.Api.Services.Integrations.Teamspeak {
    public class TeamspeakManagerService : ITeamspeakManagerService {
        private readonly IHubContext<TeamspeakHub, ITeamspeakClient> hub;
        private bool runTeamspeak;
        private CancellationTokenSource token;

        public TeamspeakManagerService(IHubContext<TeamspeakHub, ITeamspeakClient> hub) => this.hub = hub;

        public void Start() {
            runTeamspeak = true;
            token = new CancellationTokenSource();
            Task.Run(KeepOnline);
        }

        public void Stop() {
            runTeamspeak = false;
            token.Cancel();
            Task.Delay(TimeSpan.FromSeconds(5)).Wait();
            ShutTeamspeak().Wait();
        }

        public async Task SendProcedure(TeamspeakProcedureType procedure, object args) {
            await hub.Clients.All.Receive(procedure, args);
        }

        private async void KeepOnline() {
            await TaskUtilities.Delay(TimeSpan.FromSeconds(5), token.Token);
            while (runTeamspeak) {
                if (VariablesWrapper.VariablesDataService().GetSingle("TEAMSPEAK_RUN").AsBool()) {
                    if (!TeamspeakHubState.Connected) {
                        if (Process.GetProcessesByName("ts3client_win64").Length == 0) {
                            await LaunchTeamspeak();
                        } else {
                            await ShutTeamspeak();
                            continue;
                        }
                    }
                }

                await TaskUtilities.Delay(TimeSpan.FromSeconds(30), token.Token);
            }
        }

        private static async Task LaunchTeamspeak() {
            await ProcessHelper.LaunchExternalProcess("Teamspeak", $"start \"\" \"{VariablesWrapper.VariablesDataService().GetSingle("TEAMSPEAK_PATH").AsString()}\"");
        }

        private async Task ShutTeamspeak() {
            while (Process.GetProcessesByName("ts3client_win64").Length > 0) {
                foreach (Process processToKill in Process.GetProcesses().Where(x => x.ProcessName == "ts3client_win64")) {
                    await processToKill.CloseProcessGracefully();
                    processToKill.WaitForExit(5000);
                    processToKill.Refresh();
                    if (!processToKill.HasExited) {
                        processToKill.Kill();
                        await TaskUtilities.Delay(TimeSpan.FromMilliseconds(100), token.Token);
                    }
                }
            }
        }
    }
}
