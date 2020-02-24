using System.Collections.Generic;
using UKSF.Api.Interfaces.Data;

namespace UKSF.Api.Services.Utility {
    public class DataCacheService {
        private List<ICachedDataService> cachedDataServices;

        public void RegisterCachedDataServices(List<ICachedDataService> newCachedDataServices) {
            cachedDataServices = newCachedDataServices;
        }

        public void InvalidateCachedData() {
            cachedDataServices.ForEach(x => x.Refresh());
        }
    }
}
