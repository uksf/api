using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Base.Database;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Services;

namespace UKSF.Api.Base {
    public static class ApiBaseExtensions {
        public static IServiceCollection AddUksfBase(this IServiceCollection services, IConfiguration configuration) {
            services.AddSingleton(MongoClientFactory.GetDatabase(configuration.GetConnectionString("database")));
            services.AddTransient<IDataCollectionFactory, DataCollectionFactory>();
            services.AddTransient<IHttpContextService, HttpContextService>();
            services.AddSingleton<ILogger, Logger>();
            services.AddSingleton<IClock, Clock>();

            return services;
        }
    }
}
