using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

// ReSharper disable once ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator

namespace UKSF.Api.Shared.Extensions {
    public static class ServiceExtensions {
        public static IEnumerable<T> GetInterfaceServices<T>(this IServiceProvider provider) {
            if (provider is ServiceProvider serviceProvider) {
                List<ServiceDescriptor> services = new List<ServiceDescriptor>();

                object engine = serviceProvider.GetFieldValue("_engine");
                object callSiteFactory = engine.GetPropertyValue("CallSiteFactory");
                object descriptorLookup = callSiteFactory.GetFieldValue("_descriptorLookup");
                if (descriptorLookup is IDictionary dictionary) {
                    foreach (DictionaryEntry entry in dictionary) {
                        if (typeof(T).IsAssignableFrom((Type) entry.Key)) {
                            services.Add((ServiceDescriptor) entry.Value.GetPropertyValue("Last"));
                        }
                    }
                }

                return services.Select(x => (T) provider.GetService(x.ServiceType));
            }

            throw new Exception();
        }
    }
}
