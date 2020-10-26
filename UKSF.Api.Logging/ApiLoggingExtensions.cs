using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Base.Events;
using UKSF.Api.Logging.Models;
using UKSF.Api.Logging.Services;
using UKSF.Api.Logging.Services.Data;

namespace UKSF.Api.Logging {
    public static class ApiLoggingExtensions {

        public static IServiceCollection AddUksfLogging(this IServiceCollection services, IConfiguration configuration) {

            services.AddSingleton<ILogDataService, LogDataService>();
            services.AddSingleton<ILoggingService, LoggingService>();

            services.AddSingleton<IDataEventBus<BasicLogMessage>, DataEventBus<BasicLogMessage>>();

            return services;
        }
    }
}
