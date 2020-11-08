using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Admin.EventHandlers;
using UKSF.Api.Admin.Models;
using UKSF.Api.Admin.Services;
using UKSF.Api.Admin.Services.Data;
using UKSF.Api.Admin.Signalr.Hubs;
using UKSF.Api.Base.Events;

namespace UKSF.Api.Admin {
    public static class ApiAdminExtensions {
        public static IServiceCollection AddUksfAdmin(this IServiceCollection services) => services.AddContexts().AddEventBuses().AddEventHandlers().AddServices();

        private static IServiceCollection AddContexts(this IServiceCollection services) => services;

        private static IServiceCollection AddEventBuses(this IServiceCollection services) => services.AddSingleton<IDataEventBus<VariableItem>, DataEventBus<VariableItem>>();

        private static IServiceCollection AddEventHandlers(this IServiceCollection services) => services.AddSingleton<ILogDataEventHandler, LogDataEventHandler>();

        private static IServiceCollection AddServices(this IServiceCollection services) =>
            services.AddSingleton<IDataCacheService, DataCacheService>().AddTransient<IVariablesDataService, VariablesDataService>().AddTransient<IVariablesService, VariablesService>();

        public static void AddUksfAdminSignalr(this IEndpointRouteBuilder builder) {
            builder.MapHub<AdminHub>($"/hub/{AdminHub.END_POINT}");
            builder.MapHub<UtilityHub>($"/hub/{UtilityHub.END_POINT}");
        }
    }
}
