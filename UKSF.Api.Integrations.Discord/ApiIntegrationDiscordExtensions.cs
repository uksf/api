﻿using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Discord.Services;

namespace UKSF.Api.Discord {
    public static class ApiIntegrationDiscordExtensions {
        public static IServiceCollection AddUksfPersonnel(this IServiceCollection services) => services.AddContexts().AddEventBuses().AddEventHandlers().AddServices();

        private static IServiceCollection AddContexts(this IServiceCollection services) => services;

        private static IServiceCollection AddEventBuses(this IServiceCollection services) => services;

        private static IServiceCollection AddEventHandlers(this IServiceCollection services) => services;

        private static IServiceCollection AddServices(this IServiceCollection services) => services.AddSingleton<IDiscordService, DiscordService>();
    }
}