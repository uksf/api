using UKSF.Api.Admin.Context;
using UKSF.Api.Admin.EventHandlers;
using UKSF.Api.Admin.ScheduledActions;
using UKSF.Api.Admin.Services;
using UKSF.Api.Admin.Signalr.Hubs;
using UKSF.Api.Shared.Extensions;

namespace UKSF.Api.Admin;

public static class ApiAdminExtensions
{
    public static IServiceCollection AddUksfAdmin(this IServiceCollection services)
    {
        return services.AddContexts().AddEventHandlers().AddServices().AddActions();
    }

    private static IServiceCollection AddContexts(this IServiceCollection services)
    {
        return services.AddCachedContext<IVariablesContext, VariablesContext>();
    }

    private static IServiceCollection AddEventHandlers(this IServiceCollection services)
    {
        return services.AddEventHandler<ILogDataEventHandler, LogDataEventHandler>();
    }

    private static IServiceCollection AddServices(this IServiceCollection services)
    {
        return services.AddSingleton<IDataCacheService, DataCacheService>().AddSingleton<IVariablesService, VariablesService>();
    }

    private static IServiceCollection AddActions(this IServiceCollection services)
    {
        return services.AddSelfCreatingScheduledAction<IActionPruneLogs, ActionPruneLogs>();
    }

    public static void AddUksfAdminSignalr(this IEndpointRouteBuilder builder)
    {
        builder.MapHub<AdminHub>($"/hub/{AdminHub.EndPoint}");
        builder.MapHub<UtilityHub>($"/hub/{UtilityHub.EndPoint}");
    }
}
