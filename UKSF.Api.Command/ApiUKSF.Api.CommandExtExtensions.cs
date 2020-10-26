using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace UKSF.Api.Command {
    public static class ApiExtensions {
        public static IServiceCollection AddUksfTemplate(this IServiceCollection services, IConfiguration configuration) {
            return services;
        }
    }
}
