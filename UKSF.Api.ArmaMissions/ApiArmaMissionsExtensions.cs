using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.ArmaMissions.Services;

namespace UKSF.Api.ArmaMissions {
    public static class ApiArmaMissionsExtensions {
        public static IServiceCollection AddUksfArmaMissions(this IServiceCollection services) => services.AddContexts().AddEventBuses().AddEventHandlers().AddServices();

        private static IServiceCollection AddContexts(this IServiceCollection services) => services;

        private static IServiceCollection AddEventBuses(this IServiceCollection services) => services;

        private static IServiceCollection AddEventHandlers(this IServiceCollection services) => services;

        private static IServiceCollection AddServices(this IServiceCollection services) => services.AddSingleton<MissionService>();
    }
}
