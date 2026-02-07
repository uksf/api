using UKSF.Api.Core.Extensions;
using UKSF.Api.Integrations.Teamspeak.EventHandlers;
using UKSF.Api.Integrations.Teamspeak.ScheduledActions;
using UKSF.Api.Integrations.Teamspeak.Services;
using UKSF.Api.Integrations.Teamspeak.Signalr.Hubs;

namespace UKSF.Api.Integrations.Teamspeak;

public static class ApiIntegrationTeamspeakExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddUksfIntegrationTeamspeak()
        {
            return services.AddContexts().AddEventHandlers().AddServices().AddActions();
        }

        private IServiceCollection AddContexts()
        {
            return services;
        }

        private IServiceCollection AddEventHandlers()
        {
            return services.AddEventHandler<ITeamspeakEventHandler, TeamspeakEventHandler>()
                           .AddEventHandler<ITeamspeakServerEventHandler, TeamspeakServerEventHandler>();
        }

        private IServiceCollection AddServices()
        {
            return services.AddSingleton<ITeamspeakService, TeamspeakService>()
                           .AddTransient<ITeamspeakMetricsService, TeamspeakMetricsService>()
                           .AddSingleton<ITeamspeakManagerService, TeamspeakManagerService>()
                           .AddTransient<ITeamspeakGroupService, TeamspeakGroupService>();
        }

        private IServiceCollection AddActions()
        {
            return services.AddSelfCreatingScheduledAction<IActionTeamspeakSnapshot, ActionTeamspeakSnapshot>();
        }
    }

    extension(IEndpointRouteBuilder builder)
    {
        public void AddUksfIntegrationTeamspeakSignalr()
        {
            builder.MapHub<TeamspeakHub>($"/hub/{TeamspeakHub.EndPoint}").RequireHost("localhost");
            builder.MapHub<TeamspeakClientsHub>($"/hub/{TeamspeakClientsHub.EndPoint}");
        }
    }
}
