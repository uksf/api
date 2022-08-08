using UKSF.Api.Base.Configuration;
using UKSF.Api.Base.Context;
using UKSF.Api.Base.Events;

namespace UKSF.Api.Base;

public static class ApiBaseExtensions
{
    public static IServiceCollection AddUksfBase(this IServiceCollection services, IConfiguration configuration, IHostEnvironment currentEnvironment)
    {
        var appSettings = new AppSettings();
        configuration.GetSection(nameof(AppSettings)).Bind(appSettings);
        services.AddContexts()
                .AddEventHandlers()
                .AddServices()
                .AddSingleton(configuration)
                .AddSingleton(currentEnvironment)
                .AddSingleton<IHttpContextAccessor, HttpContextAccessor>()
                .AddSingleton(MongoClientFactory.GetDatabase(appSettings.ConnectionStrings.Database))
                .AddSingleton<IEventBus, EventBus>()
                .AddTransient<IMongoCollectionFactory, MongoCollectionFactory>();
        services.AddSignalR();
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
