using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using UKSF.Api.Data.Admin;
using UKSF.Api.Data.Command;
using UKSF.Api.Data.Fake;
using UKSF.Api.Data.Game;
using UKSF.Api.Data.Launcher;
using UKSF.Api.Data.Message;
using UKSF.Api.Data.Modpack;
using UKSF.Api.Data.Operations;
using UKSF.Api.Data.Personnel;
using UKSF.Api.Data.Units;
using UKSF.Api.Data.Utility;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Data.Cached;

namespace UKSF.Api.AppStart.Services {
    public static class DataServiceExtensions {
        public static void RegisterDataServices(this IServiceCollection services, IHostEnvironment currentEnvironment) {
            // Non-Cached
            services.AddSingleton<ICommandRequestArchiveDataService, CommandRequestArchiveDataService>();
            services.AddTransient<IConfirmationCodeDataService, ConfirmationCodeDataService>();
            services.AddSingleton<ILogDataService, LogDataService>();
            services.AddTransient<ISchedulerDataService, SchedulerDataService>();

            // Cached
            services.AddSingleton<IAccountDataService, AccountDataService>();
            services.AddSingleton<IBuildsDataService, BuildsDataService>();
            services.AddSingleton<ICommandRequestDataService, CommandRequestDataService>();
            services.AddSingleton<ICommentThreadDataService, CommentThreadDataService>();
            services.AddSingleton<IDischargeDataService, DischargeDataService>();
            services.AddSingleton<IGameServersDataService, GameServersDataService>();
            services.AddSingleton<ILauncherFileDataService, LauncherFileDataService>();
            services.AddSingleton<ILoaDataService, LoaDataService>();
            services.AddSingleton<IOperationOrderDataService, OperationOrderDataService>();
            services.AddSingleton<IOperationReportDataService, OperationReportDataService>();
            services.AddSingleton<IRanksDataService, RanksDataService>();
            services.AddSingleton<IReleasesDataService, ReleasesDataService>();
            services.AddSingleton<IRolesDataService, RolesDataService>();
            services.AddSingleton<IUnitsDataService, UnitsDataService>();
            services.AddSingleton<IVariablesDataService, VariablesDataService>();

            if (currentEnvironment.IsDevelopment()) {
                services.AddSingleton<INotificationsDataService, FakeNotificationsDataService>();
            } else {
                services.AddSingleton<INotificationsDataService, NotificationsDataService>();
            }
        }
    }
}
