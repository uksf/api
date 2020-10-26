using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Admin.SignalrHubs.Clients;

namespace UKSF.Api.Admin.SignalrHubs.Hubs {
    [Authorize]
    public class AdminHub : Hub<IAdminClient> {
        public const string END_POINT = "admin";
    }
}
