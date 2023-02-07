using UKSF.Api.Core.Extensions;
using UKSF.Api.Integrations.Instagram.ScheduledActions;
using UKSF.Api.Integrations.Instagram.Services;

namespace UKSF.Api.Integrations.Instagram;

public static class ApiIntegrationInstagramExtensions
{
    public static IServiceCollection AddUksfIntegrationInstagram(this IServiceCollection services)
    {
        return services.AddContexts().AddEventHandlers().AddServices().AddActions();
    }

    private static IServiceCollection AddContexts(this IServiceCollection services)
    {
        return services;
    }

    private static IServiceCollection AddEventHandlers(this IServiceCollection services)
    {
        return services;
    }

    private static IServiceCollection AddServices(this IServiceCollection services)
    {
        return services.AddSingleton<IInstagramService, InstagramService>();
    }

    private static IServiceCollection AddActions(this IServiceCollection services)
    {
        return services.AddSelfCreatingScheduledAction<IActionInstagramImages, ActionInstagramImages>()
                       .AddSelfCreatingScheduledAction<IActionInstagramToken, ActionInstagramToken>();
    }
}
