using System;
using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Base.Services.Data;

namespace UKSF.Api.Admin.Services {
    public interface IDataCacheService {
        void InvalidateCachedData();
    }

    public class DataCacheService : IDataCacheService {
        private readonly IServiceProvider serviceProvider;

        public DataCacheService(IServiceProvider serviceProvider) => this.serviceProvider = serviceProvider;

        public void InvalidateCachedData() {
            foreach (ICachedDataService cachedDataService in serviceProvider.GetServices<ICachedDataService>()) {
                cachedDataService.Refresh();
            }
        }
    }
}
