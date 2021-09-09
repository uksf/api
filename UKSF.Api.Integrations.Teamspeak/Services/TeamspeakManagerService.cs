using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Admin.Extensions;
using UKSF.Api.Admin.Services;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Extensions;
using UKSF.Api.Teamspeak.Models;
using UKSF.Api.Teamspeak.Signalr.Clients;
using UKSF.Api.Teamspeak.Signalr.Hubs;

namespace UKSF.Api.Teamspeak.Services
{
    public interface ITeamspeakManagerService
    {
        void Start();
        void Stop();
        Task SendGroupProcedure(TeamspeakProcedureType procedure, TeamspeakGroupProcedure groupProcedure);
        Task SendProcedure(TeamspeakProcedureType procedure, object args);
    }

    public class TeamspeakManagerService : ITeamspeakManagerService
    {
        private readonly IHubContext<TeamspeakHub, ITeamspeakClient> _hub;
        private readonly ILogger _logger;
        private readonly IVariablesService _variablesService;
        private bool _runTeamspeak;
        private CancellationTokenSource _token;

        public TeamspeakManagerService(IHubContext<TeamspeakHub, ITeamspeakClient> hub, IVariablesService variablesService, ILogger logger)
        {
            _hub = hub;
            _variablesService = variablesService;
            _logger = logger;
        }

        public void Start()
        {
            if (IsTeamspeakDisabled())
            {
                return;
            }

            _runTeamspeak = true;
            _token = new();
            Task.Run(KeepOnline);
        }

        public void Stop()
        {
            if (IsTeamspeakDisabled())
            {
                return;
            }

            _runTeamspeak = false;
            _token.Cancel();
            Task.Delay(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            ShutTeamspeak().GetAwaiter().GetResult();
        }

        public async Task SendGroupProcedure(TeamspeakProcedureType procedure, TeamspeakGroupProcedure groupProcedure)
        {
            if (IsTeamspeakDisabled())
            {
                return;
            }

            await _hub.Clients.All.Receive(procedure, groupProcedure);
        }

        public async Task SendProcedure(TeamspeakProcedureType procedure, object args)
        {
            if (IsTeamspeakDisabled())
            {
                return;
            }

            await _hub.Clients.All.Receive(procedure, args);
        }

        private async void KeepOnline()
        {
            await TaskUtilities.Delay(TimeSpan.FromSeconds(5), _token.Token);
            while (_runTeamspeak)
            {
                if (Process.GetProcessesByName("ts3server").Length == 0)
                {
                    await LaunchTeamspeakServer();
                }

                if (_variablesService.GetVariable("TEAMSPEAK_RUN").AsBool())
                {
                    if (!TeamspeakHubState.Connected)
                    {
                        if (Process.GetProcessesByName("ts3client_win64").Length == 0)
                        {
                            await LaunchTeamspeak();
                        }
                        else
                        {
                            await ShutTeamspeak();
                            continue;
                        }
                    }
                }

                await TaskUtilities.Delay(TimeSpan.FromSeconds(30), _token.Token);
            }
        }

        private async Task LaunchTeamspeakServer()
        {
            await ProcessUtilities.LaunchExternalProcess(
                "TeamspeakServer",
                $"start \"\" \"{_variablesService.GetVariable("TEAMSPEAK_SERVER_PATH").AsString()}\""
            );
        }

        private async Task LaunchTeamspeak()
        {
            await ProcessUtilities.LaunchExternalProcess("Teamspeak", $"start \"\" \"{_variablesService.GetVariable("TEAMSPEAK_PATH").AsString()}\"");
        }

        private async Task ShutTeamspeak()
        {
            _logger.LogInfo("Teampseak shutdown via process");
            var process = Process.GetProcesses().FirstOrDefault(x => x.ProcessName == "ts3client_win64");
            if (process == null)
            {
                _logger.LogInfo("Teampseak process not found");
                return;
            }

            await process.CloseProcessGracefully();
            process.Refresh();
            process.WaitForExit(5000);
            process.Refresh();
            if (!process.HasExited)
            {
                _logger.LogInfo("Teamspeak process not shutdown, trying to kill");
                process.Kill();
                await TaskUtilities.Delay(TimeSpan.FromMilliseconds(100), _token.Token);
            }

            _logger.LogInfo("Teampseak process should be closed");
        }

        private bool IsTeamspeakDisabled()
        {
            return !_variablesService.GetFeatureState("TEAMSPEAK");
        }
    }
}
