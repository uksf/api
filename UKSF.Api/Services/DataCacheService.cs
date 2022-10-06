using UKSF.Api.Shared.Context.Base;

namespace UKSF.Api.Services;

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
        var cachedContexts = _serviceProvider.GetRequiredService<IEnumerable<ICachedMongoContext>>();
        foreach (var cachedDataService in cachedContexts)
        {
            cachedDataService.Refresh();
        }
    }
}
