using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Services.Utility;

namespace UKSF.Integrations.AppStart {
    public static class RegisterAndWarmCachedData {
        public static void Warm() {
            IServiceProvider serviceProvider = Global.ServiceProvider;

            IAccountDataService accountDataService = serviceProvider.GetService<IAccountDataService>();
            IRanksDataService ranksDataService = serviceProvider.GetService<IRanksDataService>();
            IVariablesDataService variablesDataService = serviceProvider.GetService<IVariablesDataService>();

            DataCacheService dataCacheService = serviceProvider.GetService<DataCacheService>();
            dataCacheService.RegisterCachedDataServices(new HashSet<ICachedDataService> { accountDataService, ranksDataService, variablesDataService });
            dataCacheService.InvalidateCachedData();
        }
    }
}
