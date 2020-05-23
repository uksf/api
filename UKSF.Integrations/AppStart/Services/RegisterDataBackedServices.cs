using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using UKSF.Api.Interfaces.Message;
using UKSF.Api.Interfaces.Personnel;
using UKSF.Api.Interfaces.Utility;
using UKSF.Api.Services.Message;
using UKSF.Api.Services.Personnel;
using UKSF.Api.Services.Utility;

namespace UKSF.Integrations.AppStart.Services {
    public static class DataBackedServiceExtensions {
        public static void RegisterDataBackedServices(this IServiceCollection services, IHostEnvironment currentEnvironment) {
            // Non-Cached
            services.AddTransient<IConfirmationCodeService, ConfirmationCodeService>();
            services.AddSingleton<ILoggingService, LoggingService>();
            services.AddTransient<ISchedulerService, SchedulerService>();

            // Cached
            services.AddSingleton<IAccountService, AccountService>();
            services.AddTransient<IRanksService, RanksService>();
        }
    }
}
