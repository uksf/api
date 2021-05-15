using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using UKSF.Api.Base.Context;
using UKSF.Api.Base.Events;

namespace UKSF.Api.Base
{
    public static class ApiBaseExtensions
    {
        public static IServiceCollection AddUksfBase(this IServiceCollection services, IConfiguration configuration, IHostEnvironment currentEnvironment)
        {
            services.AddContexts()
                    .AddEventHandlers()
                    .AddServices()
                    .AddSingleton(configuration)
                    .AddSingleton(currentEnvironment)
                    .AddSingleton<IHttpContextAccessor, HttpContextAccessor>()
                    .AddSingleton(MongoClientFactory.GetDatabase(configuration.GetConnectionString("database")))
                    .AddSingleton<IEventBus, EventBus>()
                    .AddTransient<IMongoCollectionFactory, MongoCollectionFactory>();
            services.AddSignalR().AddNewtonsoftJsonProtocol();
            return services;
        }

        private static IServiceCollection AddContexts(this IServiceCollection services)
        {
            return services;
        }

        private static IServiceCollection AddEventHandlers(this IServiceCollection services)
        {
            return services;
        }

        private static IServiceCollection AddServices(this IServiceCollection services)
        {
            return services;
        }
    }
}
