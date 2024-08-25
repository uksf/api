using Microsoft.Extensions.Caching.Memory;
using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models.Domain;

namespace UKSF.Api.Core.Context;

public interface IVariablesContext : IMongoContext<DomainVariableItem>
{
    Task Update(string key, object value);
}

public class VariablesContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus)
    : MongoContext<DomainVariableItem>(mongoCollectionFactory, eventBus, "variables"), IVariablesContext
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private const double CacheLifetimeSeconds = 5;

    public override IEnumerable<DomainVariableItem> Get()
    {
        return base.Get().OrderBy(x => x.Key);
    }

    public override IEnumerable<DomainVariableItem> Get(Func<DomainVariableItem, bool> predicate)
    {
        return base.Get(predicate).OrderBy(x => x.Key);
    }

    public override DomainVariableItem GetSingle(string idOrKey)
    {
        if (_cache.TryGetValue(idOrKey, out var result))
        {
            return result as DomainVariableItem;
        }

        result = base.GetSingle(x => x.Id == idOrKey || x.Key == idOrKey.Keyify());
        _cache.Set(idOrKey, result, TimeSpan.FromSeconds(CacheLifetimeSeconds));

        return (DomainVariableItem)result;
    }

    public async Task Update(string key, object value)
    {
        var variableItem = GetSingle(key);
        if (variableItem == null)
        {
            throw new KeyNotFoundException($"VariableItem with key '{key}' does not exist");
        }

        await base.Update(variableItem.Id, x => x.Item, value);
        _cache.Remove(key);
    }

    public override async Task Delete(string key)
    {
        var variableItem = GetSingle(key);
        if (variableItem == null)
        {
            throw new KeyNotFoundException($"VariableItem with key '{key}' does not exist");
        }

        await base.Delete(variableItem);
        _cache.Remove(key);
    }
}
