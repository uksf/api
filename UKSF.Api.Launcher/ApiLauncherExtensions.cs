using UKSF.Api.Core.Extensions;
using UKSF.Api.Launcher.Context;
using UKSF.Api.Launcher.Services;
using UKSF.Api.Launcher.Signalr.Hubs;

namespace UKSF.Api.Launcher;

public static class ApiLauncherExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddUksfLauncher()
        {
            return services.AddContexts().AddEventHandlers().AddServices();
        }

        private IServiceCollection AddContexts()
        {
            return services.AddCachedContext<ILauncherFileContext, LauncherFileContext>();
        }

        private IServiceCollection AddEventHandlers()
        {
            return services;
        }

        private IServiceCollection AddServices()
        {
            return services.AddSingleton<ILauncherFileService, LauncherFileService>().AddTransient<ILauncherService, LauncherService>();
        }
    }

    extension(IEndpointRouteBuilder builder)
    {
        public void AddUksfLauncherSignalr()
        {
            builder.MapHub<LauncherHub>($"/hub/{LauncherHub.EndPoint}");
        }
    }
}
