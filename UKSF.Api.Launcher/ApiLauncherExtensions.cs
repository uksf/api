using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Launcher.Context;
using UKSF.Api.Launcher.Services;
using UKSF.Api.Launcher.Signalr.Hubs;
using UKSF.Api.Personnel.ScheduledActions;

namespace UKSF.Api.Launcher {
    public static class ApiLauncherExtensions {
        public static IServiceCollection AddUksfLauncher(this IServiceCollection services) =>
            services.AddContexts().AddEventBuses().AddEventHandlers().AddServices().AddTransient<IActionDeleteExpiredConfirmationCode, ActionDeleteExpiredConfirmationCode>();

        private static IServiceCollection AddContexts(this IServiceCollection services) => services.AddSingleton<ILauncherFileContext, LauncherFileContext>();

        private static IServiceCollection AddEventBuses(this IServiceCollection services) => services;

        private static IServiceCollection AddEventHandlers(this IServiceCollection services) => services;

        private static IServiceCollection AddServices(this IServiceCollection services) =>
            services.AddSingleton<ILauncherFileService, LauncherFileService>().AddTransient<ILauncherService, LauncherService>();

        public static void AddUksfLauncherSignalr(this IEndpointRouteBuilder builder) {
            builder.MapHub<LauncherHub>($"/hub/{LauncherHub.END_POINT}");
        }
    }
}
