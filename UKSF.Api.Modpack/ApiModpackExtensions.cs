using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Services;
using UKSF.Api.Modpack.BuildProcess;
using UKSF.Api.Modpack.Context;
using UKSF.Api.Modpack.EventHandlers;
using UKSF.Api.Modpack.ScheduledActions;
using UKSF.Api.Modpack.Services;
using UKSF.Api.Modpack.Signalr.Hubs;

namespace UKSF.Api.Modpack;

public static class ApiModpackExtensions
{
    public static IServiceCollection AddUksfModpack(this IServiceCollection services)
    {
        return services.AddContexts().AddEventHandlers().AddServices().AddActions().AddTransient<IBuildsEventHandler, BuildsEventHandler>();
    }

    private static IServiceCollection AddContexts(this IServiceCollection services)
    {
        return services.AddCachedContext<IBuildsContext, BuildsContext>().AddCachedContext<IReleasesContext, ReleasesContext>();
    }

    private static IServiceCollection AddEventHandlers(this IServiceCollection services)
    {
        return services.AddEventHandler<IBuildsEventHandler, BuildsEventHandler>();
    }

    private static IServiceCollection AddServices(this IServiceCollection services)
    {
        return services.AddSingleton<IBuildsService, BuildsService>()
                       .AddSingleton<IGithubClientService, GithubClientService>()
                       .AddTransient<IGithubService, GithubService>()
                       .AddSingleton<IGithubIssuesService, GithubIssuesService>()
                       .AddTransient<IModpackService, ModpackService>()
                       .AddTransient<IReleaseService, ReleaseService>()
                       .AddSingleton<IBuildStepService, BuildStepService>()
                       .AddSingleton<IBuildProcessorService, BuildProcessorService>()
                       .AddSingleton<IBuildQueueService, BuildQueueService>()
                       .AddSingleton<IVersionService, VersionService>()
                       .AddSingleton<IBuildProcessTracker, BuildProcessTracker>();
    }

    private static IServiceCollection AddActions(this IServiceCollection services)
    {
        return services.AddSelfCreatingScheduledAction<IActionPruneBuilds, ActionPruneBuilds>();
    }

    public static void AddUksfModpackSignalr(this IEndpointRouteBuilder builder)
    {
        builder.MapHub<ModpackHub>($"/hub/{ModpackHub.EndPoint}");
    }
}
