using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using UKSF.Api.EventHandlers;
using UKSF.Api.Interfaces.Command;
using UKSF.Api.Interfaces.Game;
using UKSF.Api.Interfaces.Launcher;
using UKSF.Api.Interfaces.Modpack;
using UKSF.Api.Interfaces.Operations;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Services.Command;
using UKSF.Api.Services.Fake;
using UKSF.Api.Services.Game;
using UKSF.Api.Services.Launcher;
using UKSF.Api.Services.Modpack;
using UKSF.Api.Services.Operations;

namespace UKSF.Api.AppStart.Services {
    public static class DataBackedServiceExtensions {
        public static void RegisterDataBackedServices(this IServiceCollection services, IHostEnvironment currentEnvironment) {
            // Non-Cached

            // Cached
            services.AddTransient<IBuildsService, BuildsService>();
            services.AddTransient<ICommandRequestService, CommandRequestService>();
            services.AddTransient<IGameServersService, GameServersService>();
            services.AddTransient<ILauncherFileService, LauncherFileService>();
            services.AddTransient<IOperationOrderService, OperationOrderService>();
            services.AddTransient<IOperationReportService, OperationReportService>();
            services.AddTransient<IReleaseService, ReleaseService>();

            services.AddSingleton<ILoggerEventHandler, LoggerEventHandler>();

            // if (currentEnvironment.IsDevelopment()) {
            //     services.AddTransient<INotificationsService, FakeNotificationsService>();
            // } else {
            // }
        }
    }
}
