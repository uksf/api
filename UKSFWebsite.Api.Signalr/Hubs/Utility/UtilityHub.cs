using Microsoft.AspNetCore.SignalR;
using UKSFWebsite.Api.Interfaces.Hubs;

namespace UKSFWebsite.Api.Signalr.Hubs.Utility {
    public class UtilityHub : Hub<IUtilityClient> {
        public const string END_POINT = "utility";
    }
}
