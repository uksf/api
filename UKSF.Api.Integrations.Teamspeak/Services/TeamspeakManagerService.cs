using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Admin.Extensions;
using UKSF.Api.Admin.Services;
using UKSF.Api.Base.Extensions;
using UKSF.Api.Teamspeak.Models;
using UKSF.Api.Teamspeak.Signalr.Clients;
using UKSF.Api.Teamspeak.Signalr.Hubs;

namespace UKSF.Api.Teamspeak.Services {
    public interface ITeamspeakManagerService {
        void Start();
        void Stop();
        Task SendGroupProcedure(TeamspeakProcedureType procedure, TeamspeakGroupProcedure groupProcedure);
        Task SendProcedure(TeamspeakProcedureType procedure, object args);
    }

    public class TeamspeakManagerService : ITeamspeakManagerService {
        private readonly IHubContext<TeamspeakHub, ITeamspeakClient> hub;
        private readonly IVariablesService variablesService;
        private bool runTeamspeak;
        private CancellationTokenSource token;

        public TeamspeakManagerService(IHubContext<TeamspeakHub, ITeamspeakClient> hub, IVariablesService variablesService) {
            this.hub = hub;
            this.variablesService = variablesService;
        }

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

        public async Task SendGroupProcedure(TeamspeakProcedureType procedure, TeamspeakGroupProcedure groupProcedure) {
            await hub.Clients.All.Receive(procedure, groupProcedure);
        }

        public async Task SendProcedure(TeamspeakProcedureType procedure, object args) {
            await hub.Clients.All.Receive(procedure, args);
        }

        private async void KeepOnline() {
            await TaskUtilities.Delay(TimeSpan.FromSeconds(5), token.Token);
            while (runTeamspeak) {
                if (variablesService.GetVariable("TEAMSPEAK_RUN").AsBool()) {
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

        private async Task LaunchTeamspeak() {
            await ProcessUtilities.LaunchExternalProcess("Teamspeak", $"start \"\" \"{variablesService.GetVariable("TEAMSPEAK_PATH").AsString()}\"");
        }

        private async Task ShutTeamspeak() {
            Process process = Process.GetProcesses().FirstOrDefault(x => x.ProcessName == "ts3client_win64");
            if (process == null) return;
            await process.CloseProcessGracefully();
            process.Refresh();
            process.WaitForExit(5000);
            process.Refresh();
            if (!process.HasExited) {
                process.Kill();
                await TaskUtilities.Delay(TimeSpan.FromMilliseconds(100), token.Token);
            }
        }
    }
}