using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Launcher.Signalr.Clients;
using UKSF.Api.Launcher.Signalr.Hubs;

// ReSharper disable NotAccessedField.Local

namespace UKSF.Api.Launcher.Services;

public interface ILauncherService { }

public class LauncherService : ILauncherService
{
    private readonly IHubContext<LauncherHub, ILauncherClient> _launcherHub;

    public LauncherService(IHubContext<LauncherHub, ILauncherClient> launcherHub)
    {
        _launcherHub = launcherHub;
    }
}
