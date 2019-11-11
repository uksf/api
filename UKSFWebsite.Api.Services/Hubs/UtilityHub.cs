using Microsoft.AspNetCore.SignalR;
using UKSFWebsite.Api.Interfaces.Hubs;

namespace UKSFWebsite.Api.Services.Hubs {
    public class UtilityHub : Hub<IUtilityClient> {
        public const string END_POINT = "utility";
    }
}
