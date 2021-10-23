using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

// ReSharper disable once ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator

namespace UKSF.Api.Shared.Extensions
{
    public static class ServiceExtensions
    {
        public static IEnumerable<T> GetInterfaceServices<T>(this IServiceProvider provider)
        {
            List<ServiceDescriptor> services = new();

            object engine;
            var fieldInfo = provider.GetType().GetFieldInfo("_engine");
            if (fieldInfo == null)
            {
                var propertyInfo = provider.GetType().GetPropertyInfo("Engine");
                if (propertyInfo == null)
                {
                    throw new($"Could not find Field '_engine' or Property 'Engine' on {provider.GetType()}");
                }

                engine = propertyInfo.GetValue(provider);
            }
            else
            {
                engine = fieldInfo.GetValue(provider);
            }

            var callSiteFactory = engine.GetPropertyValue("CallSiteFactory");
            var descriptorLookup = callSiteFactory.GetFieldValue("_descriptorLookup");
            if (descriptorLookup is IDictionary dictionary)
            {
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (typeof(T).IsAssignableFrom((Type) entry.Key))
                    {
                        services.Add((ServiceDescriptor) entry.Value.GetPropertyValue("Last"));
                    }
                }
            }

            return services.Select(x => (T) provider.GetService(x.ServiceType));
        }
    }
}
