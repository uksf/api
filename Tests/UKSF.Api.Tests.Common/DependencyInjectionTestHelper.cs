using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace UKSF.Api.Tests.Common;

public class DependencyInjectionTestHelper : IServiceProvider
{
    private readonly IServiceCollection _services = new ServiceCollection();

    private DependencyInjectionTestHelper(Func<IServiceCollection, IServiceCollection> registerServices)
    {
        registerServices(_services);

        var controllers = AppDomain.CurrentDomain.GetAssemblies()
                                   .SelectMany(x => x.GetTypes())
                                   .Where(type => typeof(ControllerBase).IsAssignableFrom(type))
                                   .ToList()
                                   .Where(type => type.FullName?.StartsWith("UKSF") ?? false)
                                   .ToList();
        foreach (var controller in controllers)
        {
            _services.AddTransient(controller);
        }

        ServiceProvider = _services.BuildServiceProvider();
    }

    public IServiceProvider ServiceProvider { get; }

    public IEnumerable<Type> ResolvableTypes
    {
        get
        {
            var serviceGroups = _services.Where(x => x.ServiceType.FullName?.StartsWith("UKSF") ?? false)
                                         .GroupBy((Func<ServiceDescriptor, Type>)(x => x.ServiceType));
            foreach (var source in serviceGroups)
            {
                var type = source.Key;
                var implementations = new HashSet<object>(source.SelectMany(GetImplementations));
                if (!type.IsGenericTypeDefinition)
                {
                    if (implementations.Count == 1)
                    {
                        yield return type;
                    }
                    else
                    {
                        yield return typeof(IEnumerable<>).MakeGenericType(type);
                    }
                }
            }

            yield break;

            static IEnumerable<object> GetImplementations(ServiceDescriptor x)
            {
                if (x.ImplementationType is not null)
                {
                    yield return x.ImplementationType;
                }

                if (x.ImplementationFactory is not null)
                {
                    yield return x.ImplementationFactory;
                }

                if (x.ImplementationInstance is not null)
                {
                    yield return x.ImplementationInstance;
                }
            }
        }
    }

    public object GetService(Type serviceType)
    {
        return ServiceProvider.GetService(serviceType);
    }

    public static DependencyInjectionTestHelper FromServiceCollection(Func<IServiceCollection, IServiceCollection> registerServices)
    {
        return new DependencyInjectionTestHelper(registerServices);
    }
}
