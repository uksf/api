using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using UKSFWebsite.Api.Services.Hubs.Abstraction;

namespace UKSFWebsite.Api.Services.Hubs {
    [Authorize]
    public class LauncherHub : Hub<ILauncherClient> {
        public const string END_POINT = "launcher";
    }
}
