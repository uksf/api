using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using UKSFWebsite.Api.Interfaces.Hubs;

namespace UKSFWebsite.Api.Signalr.Hubs.Utility {
    [Authorize]
    public class AdminHub : Hub<IAdminClient> {
        public const string END_POINT = "admin";
    }
}
