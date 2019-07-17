using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using UKSFWebsite.Api.Services.Hubs.Abstraction;

namespace UKSFWebsite.Api.Services.Hubs {
    [Authorize]
    public class AdminHub : Hub<IAdminClient> {
        public const string END_POINT = "admin";
    }
}
