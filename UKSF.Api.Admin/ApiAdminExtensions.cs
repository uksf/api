using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Admin.EventHandlers;
using UKSF.Api.Admin.Models;
using UKSF.Api.Admin.Services;
using UKSF.Api.Admin.Services.Data;
using UKSF.Api.Admin.SignalrHubs.Hubs;
using UKSF.Api.Base.Events;

namespace UKSF.Api.Admin {
    public static class ApiAdminExtensions {
        public static IServiceCollection AddUksfAdmin(this IServiceCollection services) {
            services.AddSingleton<IDataCacheService, DataCacheService>();
            services.AddSingleton<IVariablesDataService, VariablesDataService>();
            services.AddTransient<IVariablesService, VariablesService>();

            services.AddSingleton<IDataEventBus<VariableItem>, DataEventBus<VariableItem>>();

            services.AddSingleton<ILogDataEventHandler, LogDataEventHandler>();

            return services;
        }

        public static void AddUksfAdminSignalr(this IEndpointRouteBuilder builder) {
            builder.MapHub<AdminHub>($"/hub/{AdminHub.END_POINT}");
            builder.MapHub<UtilityHub>($"/hub/{UtilityHub.END_POINT}");
        }
    }
}
