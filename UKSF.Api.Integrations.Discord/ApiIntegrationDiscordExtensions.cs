using Discord;
using Discord.WebSocket;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Integrations.Discord.EventHandlers;
using UKSF.Api.Integrations.Discord.Services;

namespace UKSF.Api.Integrations.Discord;

public static class ApiIntegrationDiscordExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddUksfIntegrationDiscord()
        {
            return services.AddEventHandlers().AddServices().AddDiscord();
        }

        private IServiceCollection AddEventHandlers()
        {
            return services.AddEventHandler<IDiscordAccountEventHandler, DiscordAccountEventHandler>();
        }

        private IServiceCollection AddServices()
        {
            return services.AddSingleton<IDiscordActivationService, DiscordActivationService>()
                           .AddSingleton<IDiscordClientService, DiscordClientService>()
                           .AddSingleton<IDiscordTextService, DiscordTextService>();
        }

        private IServiceCollection AddDiscord()
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

        private IServiceCollection AddDiscordService<TService, TImplementation>() where TService : IDiscordService
        {
            return services.AddSingleton(typeof(TService), typeof(TImplementation))
                           .AddSingleton<IDiscordService>(provider => provider.GetRequiredService<TService>());
        }
    }
}
