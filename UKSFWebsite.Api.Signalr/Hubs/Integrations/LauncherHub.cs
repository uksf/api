using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using UKSFWebsite.Api.Interfaces.Hubs;

namespace UKSFWebsite.Api.Signalr.Hubs.Integrations {
    [Authorize]
    public class LauncherHub : Hub<ILauncherClient> {
        public const string END_POINT = "launcher";
    }
}
