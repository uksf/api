using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Services;

namespace UKSF.Api.Shared
{
    public static class ApiSharedExtensions
    {
        public static IServiceCollection AddUksfShared(this IServiceCollection services)
        {
            return services.AddContexts().AddEventHandlers().AddServices().AddTransient<IHttpContextService, HttpContextService>().AddSingleton<ILogger, Logger>().AddSingleton<IClock, Clock>();
        }

        private static IServiceCollection AddContexts(this IServiceCollection services)
        {
            return services.AddSingleton<ILogContext, LogContext>()
                           .AddSingleton<IAuditLogContext, AuditLogContext>()
                           .AddSingleton<IErrorLogContext, ErrorLogContext>()
                           .AddSingleton<ILauncherLogContext, LauncherLogContext>()
                           .AddSingleton<IDiscordLogContext, DiscordLogContext>()
                           .AddSingleton<ISchedulerContext, SchedulerContext>();
        }

        private static IServiceCollection AddEventHandlers(this IServiceCollection services)
        {
            return services;
        }

        private static IServiceCollection AddServices(this IServiceCollection services)
        {
            return services.AddSingleton<IScheduledActionFactory, ScheduledActionFactory>().AddTransient<ISchedulerService, SchedulerService>();
        }
    }
}
