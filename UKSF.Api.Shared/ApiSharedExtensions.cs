using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Base.Context;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Models;
using UKSF.Api.Shared.Services;

namespace UKSF.Api.Shared {
    public static class ApiSharedExtensions {
        public static IServiceCollection AddUksfShared(this IServiceCollection services) =>
            services.AddContexts()
                    .AddEventBuses()
                    .AddEventHandlers()
                    .AddServices()
                    .AddTransient<IDataCollectionFactory, DataCollectionFactory>()
                    .AddTransient<IHttpContextService, HttpContextService>()
                    .AddSingleton<ILogger, Logger>()
                    .AddSingleton<IClock, Clock>();

        private static IServiceCollection AddContexts(this IServiceCollection services) =>
            services.AddSingleton<ILogDataService, LogDataService>()
                    .AddSingleton<IAuditLogDataService, AuditLogDataService>()
                    .AddSingleton<IHttpErrorLogDataService, HttpErrorLogDataService>()
                    .AddSingleton<ILauncherLogDataService, LauncherLogDataService>()
                    .AddSingleton<ISchedulerDataService, SchedulerDataService>();

        private static IServiceCollection AddEventBuses(this IServiceCollection services) =>
            services.AddSingleton<IDataEventBus<BasicLog>, DataEventBus<BasicLog>>().AddSingleton<IDataEventBus<ScheduledJob>, DataEventBus<ScheduledJob>>();

        private static IServiceCollection AddEventHandlers(this IServiceCollection services) => services;

        private static IServiceCollection AddServices(this IServiceCollection services) =>
            services.AddSingleton<IScheduledActionFactory, ScheduledActionFactory>().AddTransient<ISchedulerService, SchedulerService>();
    }
}
