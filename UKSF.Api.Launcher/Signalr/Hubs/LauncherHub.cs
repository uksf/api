using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Launcher.Signalr.Clients;

namespace UKSF.Api.Launcher.Signalr.Hubs
{
    [Authorize]
    public class LauncherHub : Hub<ILauncherClient>
    {
        public const string END_POINT = "launcher";
    }
}
