using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Interfaces.Hubs;

namespace UKSF.Api.Signalr.Hubs.Integrations {
    [Authorize]
    public class LauncherHub : Hub<ILauncherClient> {
        public const string END_POINT = "launcher";
    }
}
