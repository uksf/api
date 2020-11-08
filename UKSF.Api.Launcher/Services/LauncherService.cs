using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Launcher.Signalr.Clients;
using UKSF.Api.Launcher.Signalr.Hubs;

namespace UKSF.Api.Launcher.Services {
    public interface ILauncherService { }

    public class LauncherService : ILauncherService {
        private readonly IHubContext<LauncherHub, ILauncherClient> launcherHub;

        public LauncherService(IHubContext<LauncherHub, ILauncherClient> launcherHub) => this.launcherHub = launcherHub;
    }
}
