using Microsoft.AspNetCore.SignalR;
using UKSFWebsite.Api.Services.Hubs.Abstraction;

namespace UKSFWebsite.Api.Services.Hubs {
    public class UtilityHub : Hub<IUtilityClient> {
        public const string END_POINT = "utility";
    }
}
