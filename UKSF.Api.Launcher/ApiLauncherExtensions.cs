using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Launcher.Context;
using UKSF.Api.Launcher.Models;
using UKSF.Api.Launcher.Services;
using UKSF.Api.Launcher.Signalr.Hubs;
using UKSF.Api.Personnel.ScheduledActions;
using UKSF.Api.Shared.Events;

namespace UKSF.Api.Launcher {
    public static class ApiLauncherExtensions {
        public static IServiceCollection AddUksfLauncher(this IServiceCollection services) =>
            services.AddContexts().AddEventHandlers().AddServices().AddTransient<IActionDeleteExpiredConfirmationCode, ActionDeleteExpiredConfirmationCode>();

        private static IServiceCollection AddContexts(this IServiceCollection services) => services.AddSingleton<ILauncherFileContext, LauncherFileContext>();

        private static IServiceCollection AddEventHandlers(this IServiceCollection services) => services;

        private static IServiceCollection AddServices(this IServiceCollection services) =>
            services.AddSingleton<ILauncherFileService, LauncherFileService>().AddTransient<ILauncherService, LauncherService>();

        public static void AddUksfLauncherSignalr(this IEndpointRouteBuilder builder) {
            builder.MapHub<LauncherHub>($"/hub/{LauncherHub.END_POINT}");
        }
    }
}
