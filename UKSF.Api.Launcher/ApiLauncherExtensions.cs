using UKSF.Api.Core.Extensions;
using UKSF.Api.Launcher.Context;
using UKSF.Api.Launcher.Services;
using UKSF.Api.Launcher.Signalr.Hubs;

namespace UKSF.Api.Launcher;

public static class ApiLauncherExtensions
{
    public static IServiceCollection AddUksfLauncher(this IServiceCollection services)
    {
        return services.AddContexts().AddEventHandlers().AddServices();
    }

    private static IServiceCollection AddContexts(this IServiceCollection services)
    {
        return services.AddCachedContext<ILauncherFileContext, LauncherFileContext>();
    }

    private static IServiceCollection AddEventHandlers(this IServiceCollection services)
    {
        return services;
    }

    private static IServiceCollection AddServices(this IServiceCollection services)
    {
        return services.AddSingleton<ILauncherFileService, LauncherFileService>().AddTransient<ILauncherService, LauncherService>();
    }

    public static void AddUksfLauncherSignalr(this IEndpointRouteBuilder builder)
    {
        builder.MapHub<LauncherHub>($"/hub/{LauncherHub.EndPoint}");
    }
}
