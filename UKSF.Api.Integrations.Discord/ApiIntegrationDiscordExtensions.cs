using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Discord.EventHandlers;
using UKSF.Api.Discord.Services;

namespace UKSF.Api.Discord {
    public static class ApiIntegrationDiscordExtensions {
        public static IServiceCollection AddUksfIntegrationDiscord(this IServiceCollection services) => services.AddContexts().AddEventHandlers().AddServices();

        private static IServiceCollection AddContexts(this IServiceCollection services) => services;

        private static IServiceCollection AddEventHandlers(this IServiceCollection services) => services.AddSingleton<IDiscordAccountEventHandler, DiscordAccountEventHandler>();

        private static IServiceCollection AddServices(this IServiceCollection services) => services.AddSingleton<IDiscordService, DiscordService>();
    }
}
