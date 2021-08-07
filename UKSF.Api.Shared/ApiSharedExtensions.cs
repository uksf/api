using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Shared.Commands;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Queries;
using UKSF.Api.Shared.Services;
using UKSF.Api.Shared.Signalr.Hubs;

namespace UKSF.Api.Shared
{
    public static class ApiSharedExtensions
    {
        public static IServiceCollection AddUksfShared(this IServiceCollection services)
        {
            return services.AddContexts()
                           .AddEventHandlers()
                           .AddServices()
                           .AddCommands()
                           .AddQueries()
                           .AddTransient<IHttpContextService, HttpContextService>()
                           .AddSingleton<ILogger, Logger>()
                           .AddSingleton<IClock, Clock>();
        }

        private static IServiceCollection AddContexts(this IServiceCollection services)
        {
            return services.AddSingleton<ILogContext, LogContext>()
                           .AddSingleton<IAuditLogContext, AuditLogContext>()
                           .AddSingleton<IErrorLogContext, ErrorLogContext>()
                           .AddSingleton<ILauncherLogContext, LauncherLogContext>()
                           .AddSingleton<IDiscordLogContext, DiscordLogContext>()
                           .AddSingleton<ISchedulerContext, SchedulerContext>()
                           .AddSingleton<ISmtpClientContext, SmtpClientContext>()
                           .AddSingleton<IFileContext, FileContext>();
        }

        private static IServiceCollection AddEventHandlers(this IServiceCollection services)
        {
            return services;
        }

        private static IServiceCollection AddServices(this IServiceCollection services)
        {
            return services.AddSingleton<IScheduledActionFactory, ScheduledActionFactory>().AddTransient<ISchedulerService, SchedulerService>();
        }

        private static IServiceCollection AddCommands(this IServiceCollection services)
        {
            return services.AddSingleton<ISendTemplatedEmailCommand, SendTemplatedEmailCommand>().AddSingleton<ISendBasicEmailCommand, SendBasicEmailCommand>();
        }

        private static IServiceCollection AddQueries(this IServiceCollection services)
        {
            return services.AddSingleton<IGetEmailTemplateQuery, GetEmailTemplateQuery>();
        }

        public static void AddUksfSignalr(this IEndpointRouteBuilder builder)
        {
            builder.MapHub<AllHub>($"/hub/{AllHub.END_POINT}");
            builder.MapHub<AccountGroupedHub>($"/hub/{AccountGroupedHub.END_POINT}");
        }
    }
}
