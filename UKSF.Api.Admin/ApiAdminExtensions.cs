using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Admin.EventHandlers;
using UKSF.Api.Admin.Models;
using UKSF.Api.Admin.Services;
using UKSF.Api.Admin.Services.Data;
using UKSF.Api.Base.Events;

namespace UKSF.Api.Admin {
    public static class ApiAdminExtensions {

        public static IServiceCollection AddUksfAdmin(this IServiceCollection services, IConfiguration configuration) {

            services.AddSingleton<DataCacheService>();
            services.AddSingleton<IVariablesDataService, VariablesDataService>();
            services.AddTransient<IVariablesService, VariablesService>();

            services.AddSingleton<IDataEventBus<VariableItem>, DataEventBus<VariableItem>>();

            services.AddSingleton<ILogEventHandler, LogEventHandler>();

            return services;
        }
    }
}
