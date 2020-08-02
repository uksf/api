using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using UKSF.Api.Interfaces.Command;
using UKSF.Api.Interfaces.Game;
using UKSF.Api.Interfaces.Launcher;
using UKSF.Api.Interfaces.Message;
using UKSF.Api.Interfaces.Modpack;
using UKSF.Api.Interfaces.Operations;
using UKSF.Api.Interfaces.Personnel;
using UKSF.Api.Interfaces.Units;
using UKSF.Api.Interfaces.Utility;
using UKSF.Api.Services.Command;
using UKSF.Api.Services.Fake;
using UKSF.Api.Services.Game;
using UKSF.Api.Services.Launcher;
using UKSF.Api.Services.Message;
using UKSF.Api.Services.Modpack;
using UKSF.Api.Services.Operations;
using UKSF.Api.Services.Personnel;
using UKSF.Api.Services.Units;
using UKSF.Api.Services.Utility;

namespace UKSF.Api.AppStart.Services {
    public static class DataBackedServiceExtensions {
        public static void RegisterDataBackedServices(this IServiceCollection services, IHostEnvironment currentEnvironment) {
            // Non-Cached
            services.AddTransient<IConfirmationCodeService, ConfirmationCodeService>();
            services.AddSingleton<ILoggingService, LoggingService>();
            services.AddTransient<ISchedulerService, SchedulerService>();

            // Cached
            services.AddSingleton<IAccountService, AccountService>();
            services.AddTransient<IBuildsService, BuildsService>();
            services.AddTransient<ICommandRequestService, CommandRequestService>();
            services.AddTransient<ICommentThreadService, CommentThreadService>();
            services.AddTransient<IDischargeService, DischargeService>();
            services.AddTransient<IGameServersService, GameServersService>();
            services.AddTransient<ILauncherFileService, LauncherFileService>();
            services.AddTransient<ILoaService, LoaService>();
            services.AddTransient<IOperationOrderService, OperationOrderService>();
            services.AddTransient<IOperationReportService, OperationReportService>();
            services.AddTransient<IRanksService, RanksService>();
            services.AddTransient<IReleaseService, ReleaseService>();
            services.AddTransient<IRolesService, RolesService>();
            services.AddTransient<IUnitsService, UnitsService>();

            if (currentEnvironment.IsDevelopment()) {
                services.AddTransient<INotificationsService, FakeNotificationsService>();
            } else {
                services.AddTransient<INotificationsService, NotificationsService>();
            }
        }
    }
}
