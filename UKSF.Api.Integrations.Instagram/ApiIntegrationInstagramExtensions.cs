using UKSF.Api.Integrations.Instagram.Services;

namespace UKSF.Api.Integrations.Instagram;

public static class ApiIntegrationInstagramExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddUksfIntegrationInstagram()
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
            return services.AddSingleton<IInstagramService, InstagramService>();
        }
    }
}
