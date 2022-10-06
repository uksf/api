using UKSF.Api.ArmaMissions.Services;

namespace UKSF.Api.ArmaMissions;

public static class ApiArmaMissionsExtensions
{
    public static IServiceCollection AddUksfArmaMissions(this IServiceCollection services)
    {
        return services.AddContexts().AddEventHandlers().AddServices();
    }

    private static IServiceCollection AddContexts(this IServiceCollection services)
    {
        return services;
    }

    private static IServiceCollection AddEventHandlers(this IServiceCollection services)
    {
        return services;
    }

    private static IServiceCollection AddServices(this IServiceCollection services)
    {
        return services.AddSingleton<MissionService>().AddSingleton<MissionPatchDataService>().AddSingleton<IMissionPatchingService, MissionPatchingService>();
    }
}
