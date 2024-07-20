using Discord;
using Discord.WebSocket;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Integrations.Discord.EventHandlers;
using UKSF.Api.Integrations.Discord.Services;

namespace UKSF.Api.Integrations.Discord;

public static class ApiIntegrationDiscordExtensions
{
    public static IServiceCollection AddUksfIntegrationDiscord(this IServiceCollection services)
    {
        return services.AddEventHandlers().AddServices().AddDiscord();
    }

    private static IServiceCollection AddEventHandlers(this IServiceCollection services)
    {
        return services.AddEventHandler<IDiscordAccountEventHandler, DiscordAccountEventHandler>();
    }

    private static IServiceCollection AddServices(this IServiceCollection services)
    {
        return services.AddSingleton<IDiscordActivationService, DiscordActivationService>()
                       .AddSingleton<IDiscordClientService, DiscordClientService>()
                       .AddSingleton<IDiscordTextService, DiscordTextService>();
    }

    private static IServiceCollection AddDiscord(this IServiceCollection services)
    {
        var config = new DiscordSocketConfig
        {
            AlwaysDownloadUsers = true,
            MessageCacheSize = 1000,
            GatewayIntents = GatewayIntents.All & ~(GatewayIntents.GuildInvites | GatewayIntents.GuildScheduledEvents | GatewayIntents.GuildPresences),
            LogLevel = LogSeverity.Warning,
            LogGatewayIntentWarnings = false
        };

        return services.AddSingleton(config)
                       .AddSingleton<DiscordSocketClient>()
                       .AddDiscordService<IDiscordMessageService, DiscordMessageService>()
                       .AddDiscordService<IDiscordMembersService, DiscordMembersService>()
                       .AddDiscordService<IDiscordAdminService, DiscordAdminService>()
                       .AddDiscordService<IDiscordRecruitmentService, DiscordRecruitmentService>()
                       .AddDiscordService<IDiscordGithubService, DiscordGithubService>();
    }

    private static IServiceCollection AddDiscordService<TService, TImplementation>(this IServiceCollection collection) where TService : IDiscordService
    {
        return collection.AddSingleton(typeof(TService), typeof(TImplementation))
                         .AddSingleton<IDiscordService>(provider => provider.GetRequiredService<TService>());
    }
}
