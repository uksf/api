﻿using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.ScheduledActions;

namespace UKSF.Api.Core.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddContext<TService, TImplementation>(this IServiceCollection collection)
    {
        return collection.AddSingleton(typeof(TService), typeof(TImplementation));
    }

    public static IServiceCollection AddCachedContext<TService, TImplementation>(this IServiceCollection collection)
    {
        return collection.AddContext<TService, TImplementation>().AddSingleton(typeof(ICachedMongoContext), typeof(TImplementation));
    }

    public static IServiceCollection AddEventHandler<TService, TImplementation>(this IServiceCollection collection)
    {
        return collection.AddSingleton(typeof(TService), typeof(TImplementation)).AddSingleton(typeof(IEventHandler), typeof(TImplementation));
    }

    public static IServiceCollection AddScheduledAction<TService, TImplementation>(this IServiceCollection collection)
    {
        return collection.AddSingleton(typeof(TService), typeof(TImplementation)).AddSingleton(typeof(IScheduledAction), typeof(TImplementation));
    }

    public static IServiceCollection AddSelfCreatingScheduledAction<TService, TImplementation>(this IServiceCollection collection)
    {
        return collection.AddScheduledAction<TService, TImplementation>().AddSingleton(typeof(ISelfCreatingScheduledAction), typeof(TImplementation));
    }
}