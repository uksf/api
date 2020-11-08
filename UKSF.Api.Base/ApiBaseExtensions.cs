using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Base.Database;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Models.Logging;
using UKSF.Api.Base.Services;
using UKSF.Api.Base.Services.Data;

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
                    .AddSingleton<ILauncherLogDataService, LauncherLogDataService>();

        private static IServiceCollection AddEventBuses(this IServiceCollection services) => services.AddSingleton<IDataEventBus<BasicLog>, DataEventBus<BasicLog>>();

        private static IServiceCollection AddEventHandlers(this IServiceCollection services) => services;

        private static IServiceCollection AddServices(this IServiceCollection services) => services.AddSingleton<IHttpContextService, HttpContextService>();
    }
}
