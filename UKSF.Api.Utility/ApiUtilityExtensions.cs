using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Base.Context;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Models;
using UKSF.Api.Base.Services;

namespace UKSF.Api.Utility {
    public static class ApiUtilityExtensions {
        public static IServiceCollection AddUksfUtility(this IServiceCollection services) => services.AddContexts().AddEventBuses().AddEventHandlers().AddServices();

        private static IServiceCollection AddContexts(this IServiceCollection services) => services.AddSingleton<ISchedulerDataService, SchedulerDataService>();

        private static IServiceCollection AddEventBuses(this IServiceCollection services) => services.AddSingleton<IDataEventBus<ScheduledJob>, DataEventBus<ScheduledJob>>();

        private static IServiceCollection AddEventHandlers(this IServiceCollection services) => services;

        private static IServiceCollection AddServices(this IServiceCollection services) =>
            services.AddSingleton<IScheduledActionService, ScheduledActionService>().AddTransient<ISchedulerService, SchedulerService>();
    }
}
