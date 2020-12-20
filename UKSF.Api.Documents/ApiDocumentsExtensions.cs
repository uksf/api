using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Documents.Context;
using UKSF.Api.Documents.Mappers;
using UKSF.Api.Documents.Queries;

namespace UKSF.Api.Documents {
    public static class ApiDocumentsExtensions {
        public static IServiceCollection AddUksfDocuments(this IServiceCollection services) => services.AddContexts().AddEventHandlers().AddQueries().AddMappers().AddServices();

        private static IServiceCollection AddContexts(this IServiceCollection services) =>
            services.AddSingleton<IDocumentsMetadataContext, DocumentsMetadataContext>().AddSingleton<IArchivedDocumentsMetadataContext, ArchivedDocumentsMetadataContext>();

        private static IServiceCollection AddEventHandlers(this IServiceCollection services) => services;

        private static IServiceCollection AddQueries(this IServiceCollection services) => services.AddSingleton<IUserPermissionsForDocumentQuery, UserPermissionsForDocumentQuery>();

        private static IServiceCollection AddMappers(this IServiceCollection services) => services.AddSingleton<IDocumentMetadataMapper, DocumentMetadataMapper>();

        private static IServiceCollection AddServices(this IServiceCollection services) => services;

        public static void AddUksfDocumentsSignalr(this IEndpointRouteBuilder builder) { }
    }
}
