using UKSF.Api.Core.Extensions;
using UKSF.Api.Teamspeak.EventHandlers;
using UKSF.Api.Teamspeak.ScheduledActions;
using UKSF.Api.Teamspeak.Services;
using UKSF.Api.Teamspeak.Signalr.Hubs;

namespace UKSF.Api.Teamspeak;

public static class ApiIntegrationTeamspeakExtensions
{
    public static IServiceCollection AddUksfIntegrationTeamspeak(this IServiceCollection services)
    {
        return services.AddContexts().AddEventHandlers().AddServices().AddActions();
    }

    private static IServiceCollection AddContexts(this IServiceCollection services)
    {
        return services;
    }

    private static IServiceCollection AddEventHandlers(this IServiceCollection services)
    {
        return services.AddEventHandler<ITeamspeakEventHandler, TeamspeakEventHandler>()
                       .AddEventHandler<ITeamspeakServerEventHandler, TeamspeakServerEventHandler>();
    }

    private static IServiceCollection AddServices(this IServiceCollection services)
    {
        return services.AddSingleton<ITeamspeakService, TeamspeakService>()
                       .AddTransient<ITeamspeakMetricsService, TeamspeakMetricsService>()
                       .AddSingleton<ITeamspeakManagerService, TeamspeakManagerService>()
                       .AddTransient<ITeamspeakGroupService, TeamspeakGroupService>();
    }

    private static IServiceCollection AddActions(this IServiceCollection services)
    {
        return services.AddSelfCreatingScheduledAction<IActionTeamspeakSnapshot, ActionTeamspeakSnapshot>();
    }

    public static void AddUksfIntegrationTeamspeakSignalr(this IEndpointRouteBuilder builder)
    {
        builder.MapHub<TeamspeakHub>($"/hub/{TeamspeakHub.EndPoint}").RequireHost("localhost");
        builder.MapHub<TeamspeakClientsHub>($"/hub/{TeamspeakClientsHub.EndPoint}");
    }
}
