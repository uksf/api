using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Base.Events;
using UKSF.Api.Shared.Models;
using UKSF.Api.Teamspeak.EventHandlers;
using UKSF.Api.Teamspeak.ScheduledActions;
using UKSF.Api.Teamspeak.Services;
using UKSF.Api.Teamspeak.Signalr.Hubs;

namespace UKSF.Api.Teamspeak {
    public static class ApiIntegrationTeamspeakExtensions {
        public static IServiceCollection AddUksfIntegrationTeamspeak(this IServiceCollection services) =>
            services.AddContexts().AddEventHandlers().AddServices().AddTransient<IActionTeamspeakSnapshot, ActionTeamspeakSnapshot>();

        private static IServiceCollection AddContexts(this IServiceCollection services) => services;

        private static IServiceCollection AddEventHandlers(this IServiceCollection services) =>
            services.AddSingleton<ITeamspeakEventHandler, TeamspeakEventHandler>()
                    .AddSingleton<ITeamspeakServerEventHandler, TeamspeakServerEventHandler>();

        private static IServiceCollection AddServices(this IServiceCollection services) =>
            services.AddSingleton<ITeamspeakService, TeamspeakService>()
                    .AddTransient<ITeamspeakMetricsService, TeamspeakMetricsService>()
                    .AddTransient<ITeamspeakManagerService, TeamspeakManagerService>()
                    .AddTransient<ITeamspeakGroupService, TeamspeakGroupService>();

        public static void AddUksfIntegrationTeamspeakSignalr(this IEndpointRouteBuilder builder) {
            builder.MapHub<TeamspeakHub>($"/hub/{TeamspeakHub.END_POINT}").RequireHost("localhost");
            builder.MapHub<TeamspeakClientsHub>($"/hub/{TeamspeakClientsHub.END_POINT}");
        }
    }
}
