﻿using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Teamspeak.EventHandlers;
using UKSF.Api.Teamspeak.ScheduledActions;
using UKSF.Api.Teamspeak.Services;

namespace UKSF.Api.Teamspeak {
    public static class ApiIntegrationTeamspeakExtensions {
        public static IServiceCollection AddUksfIntegrationTeamspeak(this IServiceCollection services) =>
            services.AddContexts().AddEventBuses().AddEventHandlers().AddServices().AddTransient<ITeamspeakSnapshotAction, TeamspeakSnapshotAction>();

        private static IServiceCollection AddContexts(this IServiceCollection services) => services;

        private static IServiceCollection AddEventBuses(this IServiceCollection services) => services;

        private static IServiceCollection AddEventHandlers(this IServiceCollection services) => services.AddSingleton<ITeamspeakEventHandler, TeamspeakEventHandler>();

        private static IServiceCollection AddServices(this IServiceCollection services) =>
            services.AddSingleton<ITeamspeakService, TeamspeakService>()
                    .AddTransient<ITeamspeakMetricsService, TeamspeakMetricsService>()
                    .AddTransient<ITeamspeakManagerService, TeamspeakManagerService>()
                    .AddTransient<ITeamspeakGroupService, TeamspeakGroupService>();
    }
}