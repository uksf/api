using System.Collections.Generic;
using UKSF.Api.Base.Services.Data;

namespace UKSF.Api.Admin.Services {
    public class DataCacheService {
        private HashSet<ICachedDataService> cachedDataServices;

        public void RegisterCachedDataServices(HashSet<ICachedDataService> newCachedDataServices) {
            cachedDataServices = newCachedDataServices;
        }

        public void InvalidateCachedData() {
            foreach (ICachedDataService cachedDataService in cachedDataServices) {
                cachedDataService.Refresh();
            }
        }
    }
}
