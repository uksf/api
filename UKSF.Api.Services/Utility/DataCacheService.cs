using System.Collections.Generic;
using UKSF.Api.Interfaces.Data;

namespace UKSF.Api.Services.Utility {
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
