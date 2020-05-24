using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Data.Admin;
using UKSF.Api.Data.Message;
using UKSF.Api.Data.Personnel;
using UKSF.Api.Data.Utility;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Data.Cached;

namespace UKSF.Integrations.AppStart.Services {
    public static class DataServiceExtensions {
        public static void RegisterDataServices(this IServiceCollection services) {
            // Non-Cached
            services.AddTransient<IConfirmationCodeDataService, ConfirmationCodeDataService>();
            services.AddSingleton<ILogDataService, LogDataService>();
            services.AddTransient<ISchedulerDataService, SchedulerIntegrationsDataService>();

            // Cached
            services.AddSingleton<IAccountDataService, AccountDataService>();
            services.AddSingleton<IRanksDataService, RanksDataService>();
            services.AddSingleton<IVariablesDataService, VariablesDataService>();
        }
    }
}
