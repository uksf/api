using Microsoft.AspNetCore.SignalR;
using UKSFWebsite.Api.Interfaces.Hubs;
using UKSFWebsite.Api.Interfaces.Launcher;
using UKSFWebsite.Api.Signalr.Hubs.Integrations;

namespace UKSFWebsite.Api.Services.Launcher {
    public class LauncherService : ILauncherService {
        private readonly IHubContext<LauncherHub, ILauncherClient> launcherHub;

        public LauncherService(IHubContext<LauncherHub, ILauncherClient> launcherHub) => this.launcherHub = launcherHub;
    }
}
