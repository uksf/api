using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using UKSF.Api.Data;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Personnel;
using UKSF.Api.Interfaces.Utility;
using UKSF.Api.Services;
using UKSF.Api.Services.Personnel;
using UKSF.Api.Services.Utility;

namespace UKSF.Integrations.AppStart.Services {
    public static class ServiceExtensions {
        public static void RegisterServices(this IServiceCollection services, IConfiguration configuration, IHostEnvironment currentEnvironment) {
            // Base
            services.AddSingleton(configuration);
            services.AddSingleton(currentEnvironment);
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddSingleton<ExceptionHandler>();

            // Data common
            services.AddSingleton(MongoClientFactory.GetDatabase(configuration.GetConnectionString("database")));
            services.AddTransient<IDataCollectionFactory, DataCollectionFactory>();
            services.AddSingleton<DataCacheService>();

            // Events & Data
            services.RegisterEventServices();
            services.RegisterDataServices();
            services.RegisterDataBackedServices(currentEnvironment);

            // Scheduled action services
            services.AddSingleton<IScheduledActionService, ScheduledActionService>();
            services.RegisterScheduledActionServices();

            // Services
            services.AddTransient<IDisplayNameService, DisplayNameService>();

            services.AddSingleton<ISessionService, SessionService>();
        }
    }
}
