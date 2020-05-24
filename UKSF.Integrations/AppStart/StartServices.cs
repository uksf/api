using System;
using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Events;
using UKSF.Api.Interfaces.Integrations;
using UKSF.Api.Interfaces.Integrations.Teamspeak;
using UKSF.Api.Interfaces.Utility;
using UKSF.Api.Services.Admin;

namespace UKSF.Integrations.AppStart {
    public static class StartServices {
        public static void Start() {
            IServiceProvider serviceProvider = Global.ServiceProvider;

            // Warm cached data services
            RegisterAndWarmCachedData.Warm();

            // Register scheduled actions
            RegisterScheduledActions.Register();

            // Start scheduler
            serviceProvider.GetService<ISchedulerService>().LoadIntegrations();
        }
    }
}
