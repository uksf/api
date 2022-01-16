using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Admin.Context;
using UKSF.Api.Admin.EventHandlers;
using UKSF.Api.Admin.ScheduledActions;
using UKSF.Api.Admin.Services;
using UKSF.Api.Admin.Signalr.Hubs;

namespace UKSF.Api.Admin
{
    public static class ApiAdminExtensions
    {
        public static IServiceCollection AddUksfAdmin(this IServiceCollection services)
        {
            return services.AddContexts().AddEventHandlers().AddServices().AddActions();
        }

        private static IServiceCollection AddContexts(this IServiceCollection services)
        {
            return services.AddSingleton<IVariablesContext, VariablesContext>();
        }

        private static IServiceCollection AddEventHandlers(this IServiceCollection services)
        {
            return services.AddSingleton<ILogDataEventHandler, LogDataEventHandler>();
        }

        private static IServiceCollection AddServices(this IServiceCollection services)
        {
            return services.AddSingleton<IDataCacheService, DataCacheService>().AddSingleton<IVariablesService, VariablesService>();
        }

        private static IServiceCollection AddActions(this IServiceCollection services)
        {
            return services.AddSingleton<IActionPruneLogs, ActionPruneLogs>();
        }

        public static void AddUksfAdminSignalr(this IEndpointRouteBuilder builder)
        {
            builder.MapHub<AdminHub>($"/hub/{AdminHub.END_POINT}");
            builder.MapHub<UtilityHub>($"/hub/{UtilityHub.END_POINT}");
        }
    }
}
