using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using UKSFWebsite.Api.Interfaces.Integrations;
using UKSFWebsite.Api.Interfaces.Integrations.Teamspeak;
using UKSFWebsite.Api.Services.Admin;
using UKSFWebsite.Api.Services.Utility;

namespace UKSFWebsite.Api.Services.Integrations.Teamspeak {
    public class TeamspeakManager : ITeamspeakManager, IDisposable {
        private readonly ISocket socket;
        private readonly string teamspeakConnectionName;
        private bool runTeamspeak;
        private int teamspeakProcessId;

        public TeamspeakManager(ISocket socket) {
            this.socket = socket;
            teamspeakConnectionName = VariablesWrapper.VariablesDataService().GetSingle("TEAMSPEAK_SOCKET_NAME").AsString();
        }

        public void Start() {
            runTeamspeak = true;
            Task.Run(AssertOnline);
        }

        public void SendProcedure(string procedure) {
            socket.SendMessageToClient(teamspeakConnectionName, procedure);
        }

        private async void AssertOnline() {
            while (runTeamspeak) {
                if (!VariablesWrapper.VariablesDataService().GetSingle("TEAMSPEAK_RUN").AsBool()) continue;
                if (!socket.IsClientOnline(teamspeakConnectionName)) {
                    if (teamspeakProcessId == default) {
                        LaunchTeamspeak();    
                    } else {
                        while (Process.GetProcesses().Any(x => x.Id == teamspeakProcessId)) {
                            ShutTeamspeak();
                            await Task.Delay(TimeSpan.FromSeconds(2));
                        }
                        teamspeakProcessId = default;
                    }
                }
                await Task.Delay(TimeSpan.FromSeconds(10));
            }
        }

        private void LaunchTeamspeak() {
            teamspeakProcessId = ProcessHelper.LaunchProcess(VariablesWrapper.VariablesDataService().GetSingle("TEAMSPEAK_PATH").AsString(), "");
        }

        private void ShutTeamspeak() {
            ProcessHelper.LaunchProcess("taskkill", $"/f /pid {teamspeakProcessId}");
        }

        public void Dispose() {
            runTeamspeak = false;
            socket.Stop();
        }
    }
}
