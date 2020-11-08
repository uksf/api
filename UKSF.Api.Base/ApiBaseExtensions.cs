using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Base.Context;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Models;
using UKSF.Api.Base.Models.Logging;
using UKSF.Api.Base.Services;

namespace UKSF.Api.Base {
    public static class ApiBaseExtensions {
        public static IServiceCollection AddUksfBase(this IServiceCollection services, IConfiguration configuration) =>
            services.AddContexts()
                    .AddEventBuses()
                    .AddEventHandlers()
                    .AddServices()
                    .AddSingleton(MongoClientFactory.GetDatabase(configuration.GetConnectionString("database")))
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
            services.AddSingleton<IHttpContextService, HttpContextService>().AddSingleton<IScheduledActionFactory, ScheduledActionFactory>().AddTransient<ISchedulerService, SchedulerService>();
    }
}
