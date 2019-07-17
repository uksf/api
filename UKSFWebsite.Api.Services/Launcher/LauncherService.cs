using Microsoft.AspNetCore.SignalR;
using UKSFWebsite.Api.Services.Abstraction;
using UKSFWebsite.Api.Services.Hubs;
using UKSFWebsite.Api.Services.Hubs.Abstraction;

namespace UKSFWebsite.Api.Services.Launcher {
    public class LauncherService : ILauncherService {
        private readonly IHubContext<LauncherHub, ILauncherClient> launcherHub;

        public LauncherService(IHubContext<LauncherHub, ILauncherClient> launcherHub) => this.launcherHub = launcherHub;
    }
}
