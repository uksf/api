using UKSF.Api.ArmaMissions.Services;

namespace UKSF.Api.ArmaMissions;

public static class ApiArmaMissionsExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddUksfArmaMissions()
        {
            return services.AddContexts().AddEventHandlers().AddServices();
        }

        private IServiceCollection AddContexts()
        {
            return services;
        }

        private IServiceCollection AddEventHandlers()
        {
            return services;
        }

        private IServiceCollection AddServices()
        {
            return services.AddSingleton<MissionService>()
                           .AddSingleton<MissionPatchDataService>()
                           .AddSingleton<IMissionPatchingService, MissionPatchingService>();
        }
    }
}
