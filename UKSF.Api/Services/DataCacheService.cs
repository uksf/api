using UKSF.Api.Core.Context.Base;

namespace UKSF.Api.Services;

public interface IDataCacheService
{
    void RefreshCachedData();
}

public class DataCacheService(IServiceProvider serviceProvider) : IDataCacheService
{
    public void RefreshCachedData()
    {
        var cachedContexts = serviceProvider.GetRequiredService<IEnumerable<ICachedMongoContext>>();
        foreach (var cachedDataService in cachedContexts)
        {
            cachedDataService.Refresh();
        }
    }
}
