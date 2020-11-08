using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Admin.Signalr.Clients;

namespace UKSF.Api.Admin.Signalr.Hubs {
    [Authorize]
    public class AdminHub : Hub<IAdminClient> {
        public const string END_POINT = "admin";
    }
}
