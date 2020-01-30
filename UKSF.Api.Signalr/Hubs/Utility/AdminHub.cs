using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Interfaces.Hubs;

namespace UKSF.Api.Signalr.Hubs.Utility {
    [Authorize]
    public class AdminHub : Hub<IAdminClient> {
        public const string END_POINT = "admin";
    }
}
