using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Integration.Instagram.ScheduledActions;
using UKSF.Api.Integration.Instagram.Services;

namespace UKSF.Api.Integration.Instagram {
    public static class ApiIntegrationInstagramExtensions {
        public static IServiceCollection AddUksfIntegrationInstagram(this IServiceCollection services) =>
            services.AddContexts()
                    .AddEventBuses()
                    .AddEventHandlers()
                    .AddServices()
                    .AddTransient<IActionInstagramImages, ActionInstagramImages>()
                    .AddTransient<IActionInstagramToken, ActionInstagramToken>();

        private static IServiceCollection AddContexts(this IServiceCollection services) => services;

        private static IServiceCollection AddEventBuses(this IServiceCollection services) => services;

        private static IServiceCollection AddEventHandlers(this IServiceCollection services) => services;

        private static IServiceCollection AddServices(this IServiceCollection services) => services.AddSingleton<IInstagramService, InstagramService>();
    }
}
