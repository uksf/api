using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using UKSF.Api.EventHandlers;
using UKSF.Api.Services;

namespace UKSF.Api.AppStart {
    public static class ServiceExtensions {
        public static IServiceCollection AddUksf(this IServiceCollection services, IConfiguration configuration, IHostEnvironment currentEnvironment) =>
            services.AddContexts()
                    .AddEventBuses()
                    .AddEventHandlers()
                    .AddServices()
                    .AddSingleton(configuration)
                    .AddSingleton(currentEnvironment)
                    .AddSingleton<IHttpContextAccessor, HttpContextAccessor>()
                    .AddSingleton<ExceptionHandler>()
                    .AddSingleton<MigrationUtility>();

        private static IServiceCollection AddContexts(this IServiceCollection services) => services;

        private static IServiceCollection AddEventBuses(this IServiceCollection services) => services;

        private static IServiceCollection AddEventHandlers(this IServiceCollection services) => services.AddSingleton<ILoggerEventHandler, LoggerEventHandler>();

        private static IServiceCollection AddServices(this IServiceCollection services) => services;
    }
}
