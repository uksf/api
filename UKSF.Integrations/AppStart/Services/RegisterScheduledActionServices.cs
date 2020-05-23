using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Interfaces.Utility.ScheduledActions;
using UKSF.Api.Services.Utility.ScheduledActions;

namespace UKSF.Integrations.AppStart.Services {
    public static class ScheduledActionServiceExtensions {
        public static void RegisterScheduledActionServices(this IServiceCollection services) {
            services.AddTransient<IDeleteExpiredConfirmationCodeAction, DeleteExpiredConfirmationCodeAction>();
        }
    }
}
