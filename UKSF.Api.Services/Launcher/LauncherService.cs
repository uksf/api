using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Interfaces.Hubs;
using UKSF.Api.Interfaces.Launcher;
using UKSF.Api.Signalr.Hubs.Integrations;

namespace UKSF.Api.Services.Launcher {
    public class LauncherService : ILauncherService {
        private readonly IHubContext<LauncherHub, ILauncherClient> launcherHub;

        public LauncherService(IHubContext<LauncherHub, ILauncherClient> launcherHub) => this.launcherHub = launcherHub;
    }
}
