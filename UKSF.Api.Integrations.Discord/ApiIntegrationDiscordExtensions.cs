using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Discord.EventHandlers;
using UKSF.Api.Discord.Services;

namespace UKSF.Api.Discord;

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
        return services.AddSingleton<IDiscordActivationService, DiscordActivationService>().AddSingleton<IDiscordClientService, DiscordClientService>();
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
                       .AddSingleton<InteractionService>()
                       .AddDiscordService<IDiscordMessageService, DiscordMessageService>()
                       .AddDiscordService<IDiscordMembersService, DiscordMembersService>()
                       .AddDiscordService<IDiscordAdminService, DiscordAdminService>()
                       .AddDiscordService<IDiscordRecruitmentService, DiscordRecruitmentService>();
    }

    private static IServiceCollection AddDiscordService<TService, TImplementation>(this IServiceCollection collection)
    {
        return collection.AddSingleton(typeof(TService), typeof(TImplementation)).AddSingleton(typeof(IDiscordService), typeof(TImplementation));
    }
}
