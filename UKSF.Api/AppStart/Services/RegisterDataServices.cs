using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using UKSF.Api.Data.Command;
using UKSF.Api.Data.Fake;
using UKSF.Api.Data.Game;
using UKSF.Api.Data.Launcher;
using UKSF.Api.Data.Modpack;
using UKSF.Api.Data.Operations;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Personnel.Services.Data;
using UKSF.Api.Services.Data;

namespace UKSF.Api.AppStart.Services {
    public static class DataServiceExtensions {
        public static void RegisterDataServices(this IServiceCollection services, IHostEnvironment currentEnvironment) {
            // Non-Cached
            services.AddSingleton<ICommandRequestArchiveDataService, CommandRequestArchiveDataService>();

            // Cached
            services.AddSingleton<IBuildsDataService, BuildsDataService>();
            services.AddSingleton<ICommandRequestDataService, CommandRequestDataService>();
            services.AddSingleton<IGameServersDataService, GameServersDataService>();
            services.AddSingleton<ILauncherFileDataService, LauncherFileDataService>();
            services.AddSingleton<IOperationOrderDataService, OperationOrderDataService>();
            services.AddSingleton<IOperationReportDataService, OperationReportDataService>();
            services.AddSingleton<IReleasesDataService, ReleasesDataService>();

            services.AddSingleton<ILogDataService, LogDataService>();
            services.AddSingleton<IAuditLogDataService, AuditLogDataService>();
            services.AddSingleton<IHttpErrorLogDataService, HttpErrorLogDataService>();
            services.AddSingleton<ILauncherLogDataService, LauncherLogDataService>();

            // if (currentEnvironment.IsDevelopment()) {
            //     services.AddSingleton<INotificationsDataService, FakeNotificationsDataService>();
            // } else {
            // }
        }
    }
}
