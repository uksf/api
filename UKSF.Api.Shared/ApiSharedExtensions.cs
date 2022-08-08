using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Shared.Commands;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Extensions;
using UKSF.Api.Shared.Queries;
using UKSF.Api.Shared.Services;
using UKSF.Api.Shared.Signalr.Hubs;

namespace UKSF.Api.Shared;

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
                       .AddSingleton<IUksfLogger, UksfLogger>()
                       .AddSingleton<IClock, Clock>();
    }

    private static IServiceCollection AddContexts(this IServiceCollection services)
    {
        return services.AddContext<ILogContext, LogContext>()
                       .AddContext<IAuditLogContext, AuditLogContext>()
                       .AddContext<IErrorLogContext, ErrorLogContext>()
                       .AddContext<ILauncherLogContext, LauncherLogContext>()
                       .AddContext<IDiscordLogContext, DiscordLogContext>()
                       .AddContext<ISchedulerContext, SchedulerContext>()
                       .AddContext<IMigrationContext, MigrationContext>()
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
        builder.MapHub<AllHub>($"/hub/{AllHub.EndPoint}");
        builder.MapHub<AccountGroupedHub>($"/hub/{AccountGroupedHub.EndPoint}");
    }
}
