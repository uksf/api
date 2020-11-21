using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Base.Context;

namespace UKSF.Api.Base {
    public static class ApiBaseExtensions {
        public static IServiceCollection AddUksfBase(this IServiceCollection services, IConfiguration configuration) =>
            services.AddContexts()
                    .AddEventBuses()
                    .AddEventHandlers()
                    .AddServices()
                    .AddSingleton(MongoClientFactory.GetDatabase(configuration.GetConnectionString("database")))
                    .AddTransient<IMongoCollectionFactory, MongoCollectionFactory>();

        private static IServiceCollection AddContexts(this IServiceCollection services) => services;

        private static IServiceCollection AddEventBuses(this IServiceCollection services) => services;

        private static IServiceCollection AddEventHandlers(this IServiceCollection services) => services;

        private static IServiceCollection AddServices(this IServiceCollection services) => services;
    }
}
