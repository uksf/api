using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.ArmaServer.Signalr.Hubs;
using UKSF.Api.Base.Events;

namespace UKSF.Api.ArmaServer {
    public static class ApiArmaServerExtensions {
        public static IServiceCollection AddUksfArmaServer(this IServiceCollection services) => services.AddContexts().AddEventBuses().AddEventHandlers().AddServices();

        private static IServiceCollection AddContexts(this IServiceCollection services) => services.AddSingleton<IGameServersDataService, GameServersDataService>();

        private static IServiceCollection AddEventBuses(this IServiceCollection services) => services.AddSingleton<IDataEventBus<GameServer>, DataEventBus<GameServer>>();

        private static IServiceCollection AddEventHandlers(this IServiceCollection services) => services;

        private static IServiceCollection AddServices(this IServiceCollection services) => services.AddSingleton<IGameServersService, GameServersService>();

        public static void AddUksfArmaServerSignalr(this IEndpointRouteBuilder builder) {
            builder.MapHub<ServersHub>($"/hub/{ServersHub.END_POINT}");
        }
    }
}
