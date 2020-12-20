using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace UKSF.Api.Documents {
    public static class ApiDocumentsExtensions {
        public static IServiceCollection AddUksfDocuments(this IServiceCollection services) => services.AddContexts().AddEventHandlers().AddServices();

        private static IServiceCollection AddContexts(this IServiceCollection services) => services;

        private static IServiceCollection AddEventHandlers(this IServiceCollection services) => services;

        private static IServiceCollection AddServices(this IServiceCollection services) => services;

        public static void AddUksfDocumentsSignalr(this IEndpointRouteBuilder builder) { }
    }
}
