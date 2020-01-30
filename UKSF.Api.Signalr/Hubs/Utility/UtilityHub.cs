using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Interfaces.Hubs;

namespace UKSF.Api.Signalr.Hubs.Utility {
    public class UtilityHub : Hub<IUtilityClient> {
        public const string END_POINT = "utility";
    }
}
