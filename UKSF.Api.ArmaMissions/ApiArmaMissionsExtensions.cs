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
            return services.AddSingleton<ISqmDecompiler, SqmDecompiler>()
                           .AddSingleton<IPboTools, PboTools>()
                           .AddTransient<IPboHandler, PboHandler>()
                           .AddTransient<ISqmReader, SqmReader>()
                           .AddTransient<ISqmWriter, SqmWriter>()
                           .AddTransient<ISqmPatcher, SqmPatcher>()
                           .AddTransient<IHeadlessClientPatcher, HeadlessClientPatcher>()
                           .AddTransient<IDescriptionReader, DescriptionReader>()
                           .AddTransient<IDescriptionWriter, DescriptionWriter>()
                           .AddTransient<IDescriptionPatcher, DescriptionPatcher>()
                           .AddTransient<ISettingsReader, SettingsReader>()
                           .AddTransient<IPatchDataBuilder, PatchDataBuilder>()
                           .AddTransient<IMissionPatchingService, MissionPatchingService>();
        }
    }
}
