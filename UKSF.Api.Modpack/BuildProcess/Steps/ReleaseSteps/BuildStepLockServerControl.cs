﻿using Microsoft.AspNetCore.SignalR;
using UKSF.Api.ArmaServer.Signalr.Clients;
using UKSF.Api.ArmaServer.Signalr.Hubs;
using UKSF.Api.Core.Context;

namespace UKSF.Api.Modpack.BuildProcess.Steps.ReleaseSteps;

[BuildStep(Name)]
public class BuildStepLockServerControl : BuildStep
{
    public const string Name = "Lock Servers";
    private IHubContext<ServersHub, IServersClient> _serversHub;
    private IVariablesContext _variablesContext;

    protected override Task SetupExecute()
    {
        _variablesContext = ServiceProvider.GetService<IVariablesContext>();
        _serversHub = ServiceProvider.GetService<IHubContext<ServersHub, IServersClient>>();
        StepLogger.Log("Retrieved services");
        return Task.CompletedTask;
    }

    protected override async Task ProcessExecute()
    {
        await _variablesContext.Update("SERVER_CONTROL_DISABLED", true);
        await _serversHub.Clients.All.ReceiveDisabledState(true);

        StepLogger.Log("Locked server control");
    }
}
