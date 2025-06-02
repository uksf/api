using System.Diagnostics;
using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Core;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Processes;
using UKSF.Api.Core.Services;
using UKSF.Api.Integrations.Teamspeak.Models;
using UKSF.Api.Integrations.Teamspeak.Signalr.Clients;
using UKSF.Api.Integrations.Teamspeak.Signalr.Hubs;

namespace UKSF.Api.Integrations.Teamspeak.Services;

public interface ITeamspeakManagerService
{
    void Start();
    void Stop();
    Task SendGroupProcedure(TeamspeakProcedureType procedure, TeamspeakGroupProcedure groupProcedure);
    Task SendProcedure(TeamspeakProcedureType procedure, object args);
}

public class TeamspeakManagerService(IHubContext<TeamspeakHub, ITeamspeakClient> hub, IVariablesService variablesService, IUksfLogger logger)
    : ITeamspeakManagerService
{
    private bool _runTeamspeak;
    private CancellationTokenSource _token;

    public void Start()
    {
        if (IsTeamspeakDisabled())
        {
            return;
        }

        _runTeamspeak = true;
        _token = new CancellationTokenSource();
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
        Task.Delay(TimeSpan.FromSeconds(3)).GetAwaiter().GetResult();
        ShutTeamspeak().GetAwaiter().GetResult();
    }

    public async Task SendGroupProcedure(TeamspeakProcedureType procedure, TeamspeakGroupProcedure groupProcedure)
    {
        if (IsTeamspeakDisabled())
        {
            return;
        }

        await hub.Clients.All.Receive(procedure, groupProcedure);
    }

    public async Task SendProcedure(TeamspeakProcedureType procedure, object args)
    {
        if (IsTeamspeakDisabled())
        {
            return;
        }

        await hub.Clients.All.Receive(procedure, args);
    }

    private async void KeepOnline()
    {
        await TaskUtilities.Delay(TimeSpan.FromSeconds(2), _token.Token);
        while (_runTeamspeak)
        {
            if (variablesService.GetVariable("TEAMSPEAK_SERVER_RUN").AsBool())
            {
                if (Process.GetProcessesByName("ts3server").Length == 0)
                {
                    await LaunchTeamspeakServer();
                }
            }

            if (variablesService.GetVariable("TEAMSPEAK_RUN").AsBool())
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

            await TaskUtilities.Delay(TimeSpan.FromSeconds(15), _token.Token);
        }
    }

    private async Task LaunchTeamspeakServer()
    {
        var serverPath = variablesService.GetVariable("TEAMSPEAK_SERVER_PATH").AsString();
        var serverDirectory = Path.GetDirectoryName(serverPath);
        await ProcessUtilities.LaunchExternalProcess("TeamspeakServer", $"start \"\" \"{serverPath}\"", serverDirectory);
    }

    private async Task LaunchTeamspeak()
    {
        await ProcessUtilities.LaunchExternalProcess("Teamspeak", $"start \"\" \"{variablesService.GetVariable("TEAMSPEAK_PATH").AsString()}\"");
    }

    private async Task ShutTeamspeak()
    {
        logger.LogInfo("Teampseak shutdown via process");
        var process = Process.GetProcesses().FirstOrDefault(x => x.ProcessName == "ts3client_win64");
        if (process == null)
        {
            logger.LogInfo("Teampseak process not found");
            return;
        }

        await process.CloseProcessGracefully();
        process.Refresh();
        process.WaitForExit(5000);
        process.Refresh();
        if (!process.HasExited)
        {
            logger.LogInfo("Teamspeak process not shutdown, trying to kill");
            process.Kill();
            await TaskUtilities.Delay(TimeSpan.FromMilliseconds(100), _token.Token);
        }

        logger.LogInfo("Teampseak process should be closed");
    }

    private bool IsTeamspeakDisabled()
    {
        return !variablesService.GetFeatureState("TEAMSPEAK");
    }
}
