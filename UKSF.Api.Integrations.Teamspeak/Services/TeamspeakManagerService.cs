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

public class TeamspeakManagerService(
    IHubContext<TeamspeakHub, ITeamspeakClient> hub,
    IProcessUtilities processUtilities,
    IVariablesService variablesService,
    IUksfLogger logger
) : ITeamspeakManagerService
{
    private volatile bool _runTeamspeak;
    private CancellationTokenSource _token;
    private Task _keepOnlineTask;

    public void Start()
    {
        if (IsTeamspeakDisabled())
        {
            return;
        }

        _runTeamspeak = true;
        _token = new CancellationTokenSource();
        _keepOnlineTask = Task.Factory.StartNew(KeepOnline, TaskCreationOptions.LongRunning).Unwrap();
    }

    public void Stop()
    {
        if (IsTeamspeakDisabled())
        {
            return;
        }

        _runTeamspeak = false;

        if (_token is null)
        {
            return;
        }

        _token.Cancel();

        try
        {
            _keepOnlineTask?.Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException)
        {
            // Expected if KeepOnline had issues during shutdown
        }

        try
        {
            ShutTeamspeak().Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException ex) when (ex.InnerException is OperationCanceledException)
        {
            // Expected during shutdown
        }

        _token.Dispose();
        _token = null;
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

    private async Task KeepOnline()
    {
        await TaskUtilities.Delay(TimeSpan.FromSeconds(2), _token.Token);
        while (_runTeamspeak)
        {
            try
            {
                if (variablesService.GetVariable("TEAMSPEAK_SERVER_RUN").AsBool())
                {
                    if (processUtilities.GetProcessesByName("ts3server").Length == 0)
                    {
                        await LaunchTeamspeakServer();
                    }
                }

                if (variablesService.GetVariable("TEAMSPEAK_RUN").AsBool())
                {
                    if (!TeamspeakHubState.Connected)
                    {
                        if (processUtilities.GetProcessesByName("ts3client_win64").Length == 0)
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
            }
            catch (Exception ex)
            {
                logger.LogError("Teamspeak KeepOnline iteration failed", ex);
            }

            await TaskUtilities.Delay(TimeSpan.FromSeconds(15), _token.Token);
        }
    }

    private async Task LaunchTeamspeakServer()
    {
        var serverPath = variablesService.GetVariable("TEAMSPEAK_SERVER_PATH").AsString();
        var serverDirectory = Path.GetDirectoryName(serverPath);
        await processUtilities.LaunchExternalProcess("TeamspeakServer", $"start \"\" \"{serverPath}\"", serverDirectory);
    }

    private async Task LaunchTeamspeak()
    {
        await processUtilities.LaunchExternalProcess("Teamspeak", $"start \"\" \"{variablesService.GetVariable("TEAMSPEAK_PATH").AsString()}\"");
    }

    private async Task ShutTeamspeak()
    {
        logger.LogInfo("Teamspeak shutdown via process");
        var process = processUtilities.FindProcessByName("ts3client_win64");
        if (process == null)
        {
            logger.LogInfo("Teamspeak process not found");
            return;
        }

        await processUtilities.CloseProcessGracefully(process);
        process.Refresh();
        process.WaitForExit(5000);
        process.Refresh();
        if (!process.HasExited)
        {
            logger.LogInfo("Teamspeak process not shutdown, trying to kill");
            process.Kill();
            await TaskUtilities.Delay(TimeSpan.FromMilliseconds(100), _token.Token);
        }

        logger.LogInfo("Teamspeak process should be closed");
    }

    private bool IsTeamspeakDisabled()
    {
        return !variablesService.GetFeatureState("TEAMSPEAK");
    }
}
