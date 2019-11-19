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
    public class TeamspeakManager : ITeamspeakManager, IDisposable {
        private readonly IHubContext<TeamspeakHub, ITeamspeakClient> hub;
        private bool runTeamspeak;
        private int teamspeakProcessId;
        private CancellationTokenSource token;

        public TeamspeakManager(IHubContext<TeamspeakHub, ITeamspeakClient> hub) => this.hub = hub;

        public void Dispose() {
            runTeamspeak = false;
            token.Cancel();
            if (TeamspeakHubState.Connected) {
                SendProcedure(TeamspeakProcedureType.SHUTDOWN, null);
                Task.Delay(TimeSpan.FromSeconds(5)).Wait();
            }

            while (Process.GetProcesses().Any(x => x.Id == teamspeakProcessId)) {
                ShutTeamspeak();
                Task.Delay(TimeSpan.FromSeconds(1)).Wait();
            }
        }

        public void Start() {
            runTeamspeak = true;
            Task.Run(KeepOnline);
        }

        public void SendProcedure(TeamspeakProcedureType procedure, object args) {
            hub.Clients.All.Receive(procedure, args);
        }

        private async void KeepOnline() {
            token = new CancellationTokenSource();
            while (runTeamspeak) {
                if (VariablesWrapper.VariablesDataService().GetSingle("TEAMSPEAK_RUN").AsBool()) {
                    if (!TeamspeakHubState.Connected) {
                        if (teamspeakProcessId == default) {
                            LaunchTeamspeak();
                        } else {
                            while (Process.GetProcesses().Any(x => x.Id == teamspeakProcessId)) {
                                ShutTeamspeak();
                                await Task.Delay(TimeSpan.FromSeconds(2), token.Token);
                            }

                            teamspeakProcessId = default;
                            continue;
                        }
                    } else {
                        // TODO: Get teamspeakProcessId
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(10), token.Token);
            }
        }

        private void LaunchTeamspeak() {
            teamspeakProcessId = ProcessHelper.LaunchProcess(VariablesWrapper.VariablesDataService().GetSingle("TEAMSPEAK_PATH").AsString(), "");
        }

        private void ShutTeamspeak() {
            ProcessHelper.LaunchProcess("taskkill", $"/f /pid {teamspeakProcessId}");
        }
    }
}
