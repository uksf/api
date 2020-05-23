using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Events.Data;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;

namespace UKSF.Integrations.AppStart.Services {
    public static class EventServiceExtensions {
        public static void RegisterEventServices(this IServiceCollection services) {
            // Event Buses
            services.AddSingleton<IDataEventBus<IAccountDataService>, DataEventBus<IAccountDataService>>();
            services.AddSingleton<IDataEventBus<IConfirmationCodeDataService>, DataEventBus<IConfirmationCodeDataService>>();
            services.AddSingleton<IDataEventBus<ILogDataService>, DataEventBus<ILogDataService>>();
            services.AddSingleton<IDataEventBus<ISchedulerDataService>, DataEventBus<ISchedulerDataService>>();
            services.AddSingleton<IDataEventBus<IRanksDataService>, DataEventBus<IRanksDataService>>();
            services.AddSingleton<IDataEventBus<IVariablesDataService>, DataEventBus<IVariablesDataService>>();

            // Event Handlers
        }
    }
}
