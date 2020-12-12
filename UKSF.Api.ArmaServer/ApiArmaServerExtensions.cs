using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.ArmaServer.Signalr.Hubs;

namespace UKSF.Api.ArmaServer {
    public static class ApiArmaServerExtensions {
        public static IServiceCollection AddUksfArmaServer(this IServiceCollection services) => services.AddContexts().AddEventHandlers().AddServices();

        private static IServiceCollection AddContexts(this IServiceCollection services) => services.AddSingleton<IGameServersContext, GameServersContext>();

        private static IServiceCollection AddEventHandlers(this IServiceCollection services) => services;

        private static IServiceCollection AddServices(this IServiceCollection services) => services.AddSingleton<IGameServersService, GameServersService>().AddSingleton<IGameServerHelpers, GameServerHelpers>();

        public static void AddUksfArmaServerSignalr(this IEndpointRouteBuilder builder) {
            builder.MapHub<ServersHub>($"/hub/{ServersHub.END_POINT}");
        }
    }
}
