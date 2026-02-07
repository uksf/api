using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.ScheduledActions;

namespace UKSF.Api.Core.Extensions;

public static class ServiceExtensions
{
    extension(IServiceCollection collection)
    {
        public IServiceCollection AddContext<TService, TImplementation>() where TImplementation : TService
        {
            return collection.AddSingleton(typeof(TService), typeof(TImplementation));
        }

        public IServiceCollection AddCachedContext<TService, TImplementation>() where TService : ICachedMongoContext where TImplementation : TService
        {
            return collection.AddContext<TService, TImplementation>().AddSingleton<ICachedMongoContext>(provider => provider.GetRequiredService<TService>());
        }

        // Event handlers are Singletons. Their dependencies (contexts, services) must also be Singletons.
        // If context lifetimes are ever changed to Scoped, event handler lifetimes must change too.
        public IServiceCollection AddEventHandler<TService, TImplementation>() where TService : IEventHandler where TImplementation : TService
        {
            return collection.AddSingleton(typeof(TService), typeof(TImplementation))
                             .AddSingleton<IEventHandler>(provider => provider.GetRequiredService<TService>());
        }

        public IServiceCollection AddScheduledAction<TService, TImplementation>() where TService : IScheduledAction where TImplementation : TService
        {
            return collection.AddSingleton(typeof(TService), typeof(TImplementation))
                             .AddSingleton<IScheduledAction>(provider => provider.GetRequiredService<TService>());
        }

        public IServiceCollection AddSelfCreatingScheduledAction<TService, TImplementation>()
            where TService : ISelfCreatingScheduledAction where TImplementation : TService
        {
            return collection.AddScheduledAction<TService, TImplementation>()
                             .AddSingleton<ISelfCreatingScheduledAction>(provider => provider.GetRequiredService<TService>());
        }
    }
}
