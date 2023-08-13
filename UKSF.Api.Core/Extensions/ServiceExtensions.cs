using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.ScheduledActions;

namespace UKSF.Api.Core.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddContext<TService, TImplementation>(this IServiceCollection collection) where TImplementation : TService
    {
        return collection.AddSingleton(typeof(TService), typeof(TImplementation));
    }

    public static IServiceCollection AddCachedContext<TService, TImplementation>(this IServiceCollection collection)
        where TService : ICachedMongoContext where TImplementation : TService
    {
        return collection.AddContext<TService, TImplementation>().AddSingleton<ICachedMongoContext>(provider => provider.GetRequiredService<TService>());
    }

    public static IServiceCollection AddEventHandler<TService, TImplementation>(this IServiceCollection collection)
        where TService : IEventHandler where TImplementation : TService
    {
        return collection.AddSingleton(typeof(TService), typeof(TImplementation))
                         .AddSingleton<IEventHandler>(provider => provider.GetRequiredService<TService>());
    }

    public static IServiceCollection AddScheduledAction<TService, TImplementation>(this IServiceCollection collection)
        where TService : IScheduledAction where TImplementation : TService
    {
        return collection.AddSingleton(typeof(TService), typeof(TImplementation))
                         .AddSingleton<IScheduledAction>(provider => provider.GetRequiredService<TService>());
    }

    public static IServiceCollection AddSelfCreatingScheduledAction<TService, TImplementation>(this IServiceCollection collection)
        where TService : ISelfCreatingScheduledAction where TImplementation : TService
    {
        return collection.AddScheduledAction<TService, TImplementation>()
                         .AddSingleton<ISelfCreatingScheduledAction>(provider => provider.GetRequiredService<TService>());
    }
}
