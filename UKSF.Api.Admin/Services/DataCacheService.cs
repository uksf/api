using System;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Extensions;

namespace UKSF.Api.Admin.Services
{
    public interface IDataCacheService
    {
        void RefreshCachedData();
    }

    public class DataCacheService : IDataCacheService
    {
        private readonly IServiceProvider _serviceProvider;

        public DataCacheService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public void RefreshCachedData()
        {
            foreach (ICachedMongoContext cachedDataService in _serviceProvider.GetInterfaceServices<ICachedMongoContext>())
            {
                cachedDataService.Refresh();
            }
        }
    }
}
