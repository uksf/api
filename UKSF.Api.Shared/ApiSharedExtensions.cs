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
                    .AddTransient<IHttpContextService, HttpContextService>()
                    .AddSingleton<ILogger, Logger>()
                    .AddSingleton<IClock, Clock>();

        private static IServiceCollection AddContexts(this IServiceCollection services) =>
            services.AddSingleton<ILogContext, LogContext>()
                    .AddSingleton<IAuditLogContext, AuditLogContext>()
                    .AddSingleton<IHttpErrorLogContext, HttpErrorLogContext>()
                    .AddSingleton<ILauncherLogContext, LauncherLogContext>()
                    .AddSingleton<IDiscordLogContext, DiscordLogContext>()
                    .AddSingleton<ISchedulerContext, SchedulerContext>();

        private static IServiceCollection AddEventBuses(this IServiceCollection services) =>
            services.AddSingleton<IDataEventBus<BasicLog>, DataEventBus<BasicLog>>()
                    .AddSingleton<IDataEventBus<AuditLog>, DataEventBus<AuditLog>>()
                    .AddSingleton<IDataEventBus<HttpErrorLog>, DataEventBus<HttpErrorLog>>()
                    .AddSingleton<IDataEventBus<LauncherLog>, DataEventBus<LauncherLog>>()
                    .AddSingleton<IDataEventBus<DiscordLog>, DataEventBus<DiscordLog>>()
                    .AddSingleton<IDataEventBus<ScheduledJob>, DataEventBus<ScheduledJob>>();

        private static IServiceCollection AddEventHandlers(this IServiceCollection services) => services;

        private static IServiceCollection AddServices(this IServiceCollection services) =>
            services.AddSingleton<IScheduledActionFactory, ScheduledActionFactory>().AddTransient<ISchedulerService, SchedulerService>();
    }
}
